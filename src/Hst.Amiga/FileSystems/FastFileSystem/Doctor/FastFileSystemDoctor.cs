namespace Hst.Amiga.FileSystems.FastFileSystem.Doctor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Converters;
    using Amiga.Extensions;
    using RigidDiskBlocks;
    using Constants = FastFileSystem.Constants;

    /// <summary>
    /// Fast File System doctor to check, repair and salvage Fast File System volumes (DOS\0 - DOS\7).
    /// The class is split in partial files: FastFileSystemDoctor.cs (volume, block access and helpers),
    /// FastFileSystemDoctor.Scan.cs (root block and directory tree), FastFileSystemDoctor.File.cs (files and
    /// links), FastFileSystemDoctor.DirCache.cs (directory cache), FastFileSystemDoctor.Bitmap.cs (bitmap) and
    /// FastFileSystemDoctor.Salvage.cs (salvage).
    /// </summary>
    public partial class FastFileSystemDoctor
    {
        /* block offsets common for header, file extension, data and dir cache blocks */
        private const int OffsetType = 0x0;
        private const int OffsetHeaderKey = 0x4;
        private const int OffsetHighSeq = 0x8;
        private const int OffsetHashTableSize = 0xc;
        private const int OffsetDataSize = 0xc;
        private const int OffsetFirstData = 0x10;
        private const int OffsetNextData = 0x10;
        private const int OffsetChecksum = 0x14;
        private const int OffsetTable = 0x18;

        /// <summary>
        /// Maximum number of block numbers listed in bitmap messages.
        /// </summary>
        private const int MaxListedBlocks = 10;

        private readonly DoctorVolume volume;
        private readonly Mode mode;
        private readonly CancellationToken cancellationToken;

        /// <summary>
        /// Blocks changed by repair, written when scan is done.
        /// </summary>
        private readonly Dictionary<uint, byte[]> changedBlocks;

        private readonly List<Issue> issues;
        private readonly Stats stats;

        private bool useOfs;
        private bool useIntl;
        private bool useDirCache;
        private bool useLnfs;

        /// <summary>
        /// Size of hash table and data block pointer table in header and file extension blocks.
        /// </summary>
        private int tableSize;

        /// <summary>
        /// Size of data in data blocks, block size for FFS and block size - 24 for OFS.
        /// </summary>
        private int dataBlockSize;

        private FastFileSystemDoctor(DoctorVolume volume, Mode mode, CancellationToken cancellationToken)
        {
            this.volume = volume;
            this.mode = mode;
            this.cancellationToken = cancellationToken;
            changedBlocks = new Dictionary<uint, byte[]>();
            issues = new List<Issue>();
            stats = new Stats();
        }

        /// <summary>
        /// Issues found by last run.
        /// </summary>
        public IReadOnlyList<Issue> Issues => issues;

        /// <summary>
        /// Statistics from last run.
        /// </summary>
        public Stats Stats => stats;

        /// <summary>
        /// Volume could not be checked, e.g. dos type is not a Fast File System dos type or root block is
        /// invalid in check mode.
        /// </summary>
        public bool Aborted { get; private set; }

        /// <summary>
        /// Volume has errors, which are not fixed.
        /// </summary>
        public bool HasErrors => Aborted || issues.Any(x => !x.Warning && !x.Fixed);

        /// <summary>
        /// Open volume from stream and partition block.
        /// </summary>
        public static DoctorVolume OpenVolume(Stream stream, PartitionBlock partitionBlock) =>
            DoctorVolume.Open(stream, partitionBlock);

        /// <summary>
        /// Open volume from adf stream.
        /// </summary>
        public static DoctorVolume OpenAdf(Stream stream) => DoctorVolume.OpenAdf(stream);

        /// <summary>
        /// Run doctor on volume. Mode check only reports errors and doesn't write any changes, mode repair
        /// fixes errors found.
        /// </summary>
        public static async Task<FastFileSystemDoctor> Run(DoctorVolume volume, Mode mode,
            CancellationToken cancellationToken = default)
        {
            if (volume == null) throw new ArgumentNullException(nameof(volume));

            var doctor = new FastFileSystemDoctor(volume, mode, cancellationToken);
            await doctor.Scan();
            return doctor;
        }

        private bool Repair => mode == Mode.Repair;

        /// <summary>
        /// Block is inside the area of the volume managed by bitmap.
        /// </summary>
        private bool InRange(uint block) => block >= volume.Reserved && block < volume.Blocks;

        private void ShowMsg(string message) => volume.ShowMsg?.Invoke(message + "\n");

        /// <summary>
        /// Add issue and show it. Issue is fixed in repair mode, if fixable.
        /// </summary>
        private Issue AddIssue(IssueType type, uint block, string path, string message, bool warning = false,
            bool fixable = true)
        {
            var issue = new Issue
            {
                Type = type,
                Block = block,
                Path = path ?? string.Empty,
                Message = message,
                Warning = warning,
                Fixed = Repair && fixable
            };
            issues.Add(issue);

            if (warning)
            {
                stats.Warnings++;
            }
            else
            {
                stats.Errors++;
            }

            if (issue.Fixed)
            {
                stats.Fixed++;
            }

            ShowMsg(issue.ToString());
            return issue;
        }

        private async Task<byte[]> ReadRawBlock(uint block, int size)
        {
            var buffer = new byte[size];
            volume.Stream.Seek(volume.PartitionStartOffset + (long)block * volume.BlockSize, SeekOrigin.Begin);
            var offset = 0;
            while (offset < size)
            {
                var bytesRead = await volume.Stream.ReadAsync(buffer, offset, size - offset, cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                offset += bytesRead;
            }

            return buffer;
        }

        /// <summary>
        /// Read block. Blocks changed by repair are returned from changed blocks. Returns null, if block is
        /// outside volume.
        /// </summary>
        private async Task<byte[]> ReadBlock(uint block)
        {
            if (block >= volume.Blocks)
            {
                return null;
            }

            if (changedBlocks.TryGetValue(block, out var changed))
            {
                return changed;
            }

            return await ReadRawBlock(block, volume.BlockSize);
        }

        /// <summary>
        /// Mark block as changed and update checksum. Changed blocks are only written in repair mode.
        /// </summary>
        /// <param name="block">Block number.</param>
        /// <param name="data">Block data.</param>
        /// <param name="checksumOffset">Offset of checksum, -1 for blocks without checksum.</param>
        private void ChangeBlock(uint block, byte[] data, int checksumOffset = OffsetChecksum)
        {
            if (checksumOffset >= 0)
            {
                ChecksumHelper.UpdateChecksum(data, checksumOffset);
            }

            changedBlocks[block] = data;
        }

        /// <summary>
        /// Write changed blocks to volume in repair mode.
        /// </summary>
        private async Task WriteChangedBlocks()
        {
            if (!Repair || changedBlocks.Count == 0)
            {
                return;
            }

            foreach (var block in changedBlocks.Keys.OrderBy(x => x))
            {
                var data = changedBlocks[block];
                volume.Stream.Seek(volume.PartitionStartOffset + (long)block * volume.BlockSize,
                    SeekOrigin.Begin);
                await volume.Stream.WriteAsync(data, 0, data.Length, cancellationToken);
            }

            await volume.Stream.FlushAsync(cancellationToken);
            ShowMsg($"Wrote {changedBlocks.Count} changed blocks");
            changedBlocks.Clear();
        }

        /// <summary>
        /// Set dos type flags from dos type.
        /// </summary>
        private void SetDosType(uint dosType)
        {
            var mode = (int)(dosType & 0xff);
            useOfs = Macro.UseOfs(mode);
            useIntl = Macro.UseIntl(mode) || Macro.UseDirCache(mode);
            useDirCache = Macro.UseDirCache(mode);
            useLnfs = Macro.UseLnfs(mode);
            tableSize = (int)FastFileSystemHelper.CalculateHashtableSize((uint)volume.BlockSize);
            dataBlockSize = useOfs ? volume.BlockSize - 24 : volume.BlockSize;
        }

        private static bool IsFastFileSystemDosType(uint dosType) =>
            (dosType & 0xffffff00) == 0x444f5300 && (dosType & 0xff) <= 7;

        private static string FormatDosType(uint dosType) =>
            (dosType & 0xffffff00) == 0x444f5300 ? $"DOS\\{dosType & 0xff}" : $"0x{dosType:x8}";

        /* field access */

        private static uint GetUInt32(byte[] data, int offset) =>
            BigEndianConverter.ConvertBytesToUInt32(data, offset);

        private static int GetInt32(byte[] data, int offset) =>
            BigEndianConverter.ConvertBytesToInt32(data, offset);

        private static void SetUInt32(byte[] data, int offset, uint value) =>
            BigEndianConverter.ConvertUInt32ToBytes(value, data, offset);

        private int OffsetHashChain => volume.BlockSize - 0x10;
        private int OffsetParent => volume.BlockSize - 0x0c;
        private int OffsetExtension => volume.BlockSize - 0x08;
        private int OffsetSecType => volume.BlockSize - 0x04;
        private int OffsetAccess => volume.BlockSize - 0xc0;
        private int OffsetByteSize => volume.BlockSize - 0xbc;
        private int OffsetComment => volume.BlockSize - 0xb8;
        private int OffsetName => volume.BlockSize - 0x50;
        private int OffsetDate => useLnfs ? volume.BlockSize - 0x3c : volume.BlockSize - 0x5c;
        private int OffsetRealEntry => volume.BlockSize - 0x2c;
        private int OffsetNextLink => volume.BlockSize - 0x28;
        private int OffsetLnfsCommentBlock => volume.BlockSize - 0x48;
        private int OffsetBitmapFlag => volume.BlockSize - 0xc8;
        private int OffsetBitmapPages => volume.BlockSize - 0xc4;
        private int OffsetBitmapExtension => volume.BlockSize - 0x60;
        private int OffsetRootDate => volume.BlockSize - 0x5c;

        /// <summary>
        /// Size of merged name and comment area in long name file system entry blocks.
        /// </summary>
        private const int LnfsNameCommentSize = Constants.LNFSNAMECMMTLEN;

        private static int TableOffset(int index) => OffsetTable + index * 4;

        /// <summary>
        /// Offset of data block pointer for sequence index in header or file extension block. Data block pointers
        /// are stored in reverse order from the end of the table.
        /// </summary>
        private int DataPointerOffset(int index) => TableOffset(tableSize - 1 - index);

        private static bool ChecksumValid(byte[] data, int checksumOffset = OffsetChecksum) =>
            GetInt32(data, checksumOffset) == ChecksumHelper.CalculateChecksum(data, checksumOffset);

        private static bool IsEntrySecType(int secType) =>
            secType == Constants.ST_FILE || secType == Constants.ST_DIR || secType == Constants.ST_LFILE ||
            secType == Constants.ST_LDIR || secType == Constants.ST_LSOFT;

        /// <summary>
        /// Block has the structure of an entry block (file, directory or link) with given block number. Checksum is
        /// not checked.
        /// </summary>
        private bool IsEntryBlockStructure(uint block, byte[] data) =>
            data != null &&
            GetInt32(data, OffsetType) == Constants.T_HEADER &&
            GetUInt32(data, OffsetHeaderKey) == block &&
            IsEntrySecType(GetInt32(data, OffsetSecType));

        /// <summary>
        /// Block is a valid entry block (file, directory or link) with given block number and valid checksum.
        /// </summary>
        private bool IsValidEntryBlock(uint block, byte[] data) =>
            IsEntryBlockStructure(block, data) && ChecksumValid(data);

        /// <summary>
        /// Block has the structure of a root block. Checksum is not checked.
        /// </summary>
        private bool IsRootBlockStructure(byte[] data) =>
            data != null &&
            GetInt32(data, OffsetType) == Constants.T_HEADER &&
            GetUInt32(data, OffsetHeaderKey) == 0 &&
            GetUInt32(data, OffsetHighSeq) == 0 &&
            GetUInt32(data, OffsetHashTableSize) == tableSize &&
            GetInt32(data, OffsetSecType) == Constants.ST_ROOT;

        /// <summary>
        /// Read name of entry or root block. Name is read without length limit, so too long names can be detected.
        /// </summary>
        private string ReadName(byte[] data, bool root = false)
        {
            if (useLnfs && !root)
            {
                var length = Math.Min((int)data[OffsetComment], LnfsNameCommentSize - 1);
                return AmigaTextHelper.GetString(data, OffsetComment + 1, length);
            }

            return AmigaTextHelper.GetString(data, OffsetName + 1, Math.Min((int)data[OffsetName], 0x50 - 0x10 - 1));
        }

        /// <summary>
        /// Read comment of entry block. For long name file system, the comment is read from the merged name and
        /// comment area, comment blocks are not read.
        /// </summary>
        private string ReadComment(byte[] data)
        {
            if (useLnfs)
            {
                var nameLength = Math.Min((int)data[OffsetComment], LnfsNameCommentSize - 1);
                var commentOffset = OffsetComment + 1 + nameLength;
                var maxLength = LnfsNameCommentSize - 2 - nameLength;
                if (maxLength <= 0)
                {
                    return string.Empty;
                }

                return AmigaTextHelper.GetString(data, commentOffset + 1, Math.Min((int)data[commentOffset], maxLength));
            }

            return data.ReadStringWithLength(OffsetComment, Constants.MAXCMMTLEN);
        }

        /// <summary>
        /// Name is valid: not empty, not too long and doesn't contain ':' or '/'.
        /// </summary>
        private bool IsNameValid(byte[] data)
        {
            int length;
            int offset;
            if (useLnfs)
            {
                length = data[OffsetComment];
                offset = OffsetComment + 1;
                if (length == 0 || length > Constants.LNFSMAXNAMELEN - 1 || length + 2 > LnfsNameCommentSize)
                {
                    return false;
                }

                var commentLength = data[offset + length];
                if (length + 2 + commentLength > LnfsNameCommentSize)
                {
                    return false;
                }
            }
            else
            {
                length = data[OffsetName];
                offset = OffsetName + 1;
                if (length == 0 || length > Constants.MAXNAMELEN)
                {
                    return false;
                }
            }

            for (var i = 0; i < length; i++)
            {
                var c = data[offset + i];
                if (c == ':' || c == '/')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Write name to entry block. For long name file system, the comment is kept after the name.
        /// </summary>
        private void WriteName(byte[] data, string name)
        {
            if (useLnfs)
            {
                var comment = IsNameValid(data) ? ReadComment(data) : string.Empty;
                var nameBytes = AmigaTextHelper.GetBytes(name);
                var commentBytes = AmigaTextHelper.GetBytes(comment);
                if (nameBytes.Length + commentBytes.Length + 2 > LnfsNameCommentSize)
                {
                    commentBytes = Array.Empty<byte>();
                }

                Array.Clear(data, OffsetComment, LnfsNameCommentSize);
                data[OffsetComment] = (byte)nameBytes.Length;
                Array.Copy(nameBytes, 0, data, OffsetComment + 1, nameBytes.Length);
                data[OffsetComment + 1 + nameBytes.Length] = (byte)commentBytes.Length;
                Array.Copy(commentBytes, 0, data, OffsetComment + 2 + nameBytes.Length, commentBytes.Length);
                return;
            }

            var bytes = AmigaTextHelper.GetBytes(name);
            Array.Clear(data, OffsetName, Constants.MAXNAMELEN + 1);
            data[OffsetName] = (byte)bytes.Length;
            Array.Copy(bytes, 0, data, OffsetName + 1, bytes.Length);
        }

        private int MaxNameLength => useLnfs ? Constants.LNFSMAXNAMELEN - 1 : Constants.MAXNAMELEN;

        /// <summary>
        /// Create name for entry by appending suffix to name, truncating name to fit maximum name length.
        /// </summary>
        private string CreateName(string name, string suffix)
        {
            name = (name ?? string.Empty).Replace(":", "_").Replace("/", "_");
            if (name.Length + suffix.Length > MaxNameLength)
            {
                name = name.Substring(0, Math.Max(0, MaxNameLength - suffix.Length));
            }

            return name + suffix;
        }

        private int HashName(string name) => FastFileSystem.Directory.GetHashValue(tableSize, name, useIntl);

        private string UpperName(string name) => FastFileSystem.Directory.MyToUpper(name, useIntl);

        private DateTime ReadDate(byte[] data, int offset)
        {
            try
            {
                return DateHelper.ReadDate(data, offset);
            }
            catch (ArgumentOutOfRangeException)
            {
                return AmigaDate.AmigaEpocDate;
            }
        }

        /// <summary>
        /// Date stamp is valid: minutes are less than a day and ticks are less than a minute.
        /// </summary>
        private static bool IsDateValid(byte[] data, int offset)
        {
            var days = GetUInt32(data, offset);
            var minutes = GetUInt32(data, offset + 4);
            var ticks = GetUInt32(data, offset + 8);
            return days < 0x7fffffff && minutes < 24 * 60 && ticks < 60 * 50;
        }
    }
}
