namespace Hst.Amiga.FileSystems.SmartFileSystem.Doctor
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Converters;
    using RigidDiskBlocks;
    using BlockHelper = Blocks.BlockHelper;

    /// <summary>
    /// Smart file system doctor to check, repair and salvage smart file system volumes (SFS\0).
    /// The class is split in partial files: SmartFileSystemDoctor.cs (volume, block access and helpers),
    /// SmartFileSystemDoctor.Root.cs (root blocks and transactions), SmartFileSystemDoctor.AdminSpaces.cs
    /// (admin space containers and admin block allocation), SmartFileSystemDoctor.Nodes.cs (object node tree),
    /// SmartFileSystemDoctor.Objects.cs (directory tree, object containers, hash tables and soft links),
    /// SmartFileSystemDoctor.Extents.cs (extent B-tree and file data), SmartFileSystemDoctor.Bitmap.cs (bitmap and
    /// root info) and SmartFileSystemDoctor.Salvage.cs (salvage).
    /// </summary>
    public partial class SmartFileSystemDoctor
    {
        /// <summary>
        /// Maximum number of block numbers listed in messages.
        /// </summary>
        private const int MaxListedBlocks = 10;

        /// <summary>
        /// Maximum directory depth, guards against corrupt directory structures.
        /// </summary>
        private const int MaxDepth = 256;

        private readonly DoctorVolume volume;
        private readonly Mode mode;
        private readonly CancellationToken cancellationToken;

        /// <summary>
        /// Blocks changed by repair or replayed from a pending transaction, written when scan is done.
        /// </summary>
        private readonly Dictionary<uint, byte[]> changedBlocks;

        private readonly List<Issue> issues;
        private readonly Stats stats;

        /// <summary>
        /// Valid root block used for the scan.
        /// </summary>
        private RootBlock rootBlock;

        private bool caseSensitive;
        private int shiftsBlock32;
        private int bitmapBlocks;

        /// <summary>
        /// Administration blocks used by the volume structures: admin space containers, object containers, hash
        /// tables, soft links, node containers, B-tree node containers and transaction block.
        /// </summary>
        private readonly HashSet<uint> adminUsed;

        private SmartFileSystemDoctor(DoctorVolume volume, Mode mode, CancellationToken cancellationToken)
        {
            this.volume = volume;
            this.mode = mode;
            this.cancellationToken = cancellationToken;
            changedBlocks = new Dictionary<uint, byte[]>();
            issues = new List<Issue>();
            stats = new Stats();
            adminUsed = new HashSet<uint>();
            adminContainers = new List<AdminContainerModel>();
            objectContainers = new List<ContainerModel>();
            oldNodes = new Dictionary<uint, ObjectNode>();
            nodeContainers = new List<NodeContainer>();
            oldExtents = new Dictionary<uint, ExtentBNode>();
            bNodeContainers = new List<BNodeContainer>();
            files = new List<ObjModel>();
            droppedExtents = new HashSet<uint>();
            changedExtents = new HashSet<uint>();
            newNodes = new HashSet<uint>();
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
        /// Volume could not be checked, e.g. root blocks are invalid or volume is not a smart file system volume.
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
        /// Run doctor on volume. Mode check only reports errors and doesn't write any changes, mode repair
        /// fixes errors found.
        /// </summary>
        public static async Task<SmartFileSystemDoctor> Run(DoctorVolume volume, Mode mode,
            CancellationToken cancellationToken = default)
        {
            if (volume == null) throw new ArgumentNullException(nameof(volume));

            var doctor = new SmartFileSystemDoctor(volume, mode, cancellationToken);
            await doctor.Scan();
            return doctor;
        }

        private bool Repair => mode == Mode.Repair;

        private void ShowMsg(string message) => volume.ShowMsg?.Invoke(message + "\n");

        private async Task Scan()
        {
            ShowMsg($"Checking smart file system volume{(string.IsNullOrEmpty(volume.DeviceName) ? string.Empty : $" '{volume.DeviceName}'")}, block size {volume.BlockSize}, blocks {volume.Blocks}");

            if (!await CheckRootBlocks())
            {
                Aborted = true;
                return;
            }

            ClaimRootStructures();
            await CheckTransaction();
            await ReadAdminSpaces();
            await ReadNodeTree();
            await ReadExtentTree();

            if (!await CheckDirectoryTree())
            {
                Aborted = true;
                return;
            }

            AllocateObjectNodes();
            CheckFiles();
            FinalizeDirectories();
            await FinalizeHashTables();
            await FinalizeSoftLinks();
            await FinalizeObjectNodes();
            await FinalizeExtents();
            await FinalizeAdminSpaces();
            await FinalizeBitmap();
            WriteObjectContainers();

            ShowMsg($"{stats.Directories} directories, {stats.Files} files, {stats.SoftLinks} soft links, {stats.UsedBlocks} blocks used, {stats.FreeBlocks} blocks free");
            ShowMsg($"{stats.Errors} errors, {stats.Warnings} warnings, {stats.Fixed} fixed");

            await WriteChangedBlocks();
        }

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

            if (!salvaging)
            {
                ShowMsg(issue.ToString());
            }
            return issue;
        }

        private async Task<byte[]> ReadRawBlocks(uint block, int count)
        {
            var size = count * volume.BlockSize;
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
        /// Read block. Blocks changed by repair are returned from changed blocks. Returns a copy of the block or
        /// null, if block is outside volume.
        /// </summary>
        private async Task<byte[]> ReadBlock(uint block)
        {
            if (block >= volume.Blocks)
            {
                return null;
            }

            if (changedBlocks.TryGetValue(block, out var changed))
            {
                return (byte[])changed.Clone();
            }

            return await ReadRawBlocks(block, 1);
        }

        /// <summary>
        /// Mark block as changed and update checksum. Changed blocks are only written in repair mode.
        /// </summary>
        private void ChangeBlock(uint block, byte[] data)
        {
            ChecksumHelper.UpdateChecksum(data);
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
        /// Read all blocks in volume in chunks and call action for each block.
        /// </summary>
        private async Task ScanBlocks(Action<uint, byte[]> action)
        {
            const int blocksPerChunk = 256;

            for (uint block = 0; block < volume.Blocks; block += blocksPerChunk)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = (int)Math.Min(blocksPerChunk, volume.Blocks - block);
                var chunk = await ReadRawBlocks(block, count);
                for (var i = 0; i < count; i++)
                {
                    var current = block + (uint)i;
                    if (!changedBlocks.TryGetValue(current, out var data))
                    {
                        data = new byte[volume.BlockSize];
                        Array.Copy(chunk, i * volume.BlockSize, data, 0, volume.BlockSize);
                    }

                    action(current, data);
                }
            }
        }

        private bool InVolume(uint block) => block < volume.Blocks;

        /// <summary>
        /// Claim an administration block used by a volume structure. Returns false, if block is already used by
        /// another structure.
        /// </summary>
        private bool ClaimAdmin(uint block) => adminUsed.Add(block);

        /// <summary>
        /// Get problem with block header: id, checksum and own block. Returns null, if block header is valid.
        /// </summary>
        private static string GetBlockProblem(byte[] data, uint block, uint id)
        {
            if (data == null)
            {
                return $"block {block} is outside partition";
            }

            var blockId = GetUInt32(data, 0);
            if (blockId != id)
            {
                return $"block {block} has id '{BlockHelper.FormatId(blockId)}', expected '{BlockHelper.FormatId(id)}'";
            }

            if (!ChecksumHelper.IsValid(data))
            {
                return $"block {block} has invalid checksum";
            }

            var ownBlock = GetUInt32(data, 8);
            if (ownBlock != block)
            {
                return $"block {block} has own block {ownBlock}";
            }

            return null;
        }

        private static bool IsValidBlock(byte[] data, uint block, uint id) => GetBlockProblem(data, block, id) == null;

        /// <summary>
        /// Block has expected id and own block, but invalid checksum.
        /// </summary>
        private static bool IsChecksumOnlyProblem(byte[] data, uint block, uint id) =>
            data != null && GetUInt32(data, 0) == id && GetUInt32(data, 8) == block && !ChecksumHelper.IsValid(data);

        private static uint GetUInt32(byte[] data, int offset) =>
            BigEndianConverter.ConvertBytesToUInt32(data, offset);

        private static string ListBlocks(IReadOnlyCollection<uint> blocks)
        {
            var listed = string.Join(", ", blocks.Take(MaxListedBlocks));
            return blocks.Count > MaxListedBlocks ? $"{listed}, ..." : listed;
        }

        private string UpperName(string name)
        {
            if (caseSensitive)
            {
                return name;
            }

            var bytes = AmigaTextHelper.GetBytes(name);
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = HashHelper.UpperChar(bytes[i]);
            }

            return AmigaTextHelper.GetString(bytes);
        }

        private BitArray CreateBlockMap() => new BitArray((int)Math.Min(volume.Blocks, int.MaxValue));

        private static void MarkRange(BitArray map, uint start, uint count)
        {
            for (long block = start; block < (long)start + count && block < map.Length; block++)
            {
                map[(int)block] = true;
            }
        }

        private static bool AnyInRange(BitArray map, uint start, uint count)
        {
            for (long block = start; block < (long)start + count && block < map.Length; block++)
            {
                if (map[(int)block])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Object container in directory tree.
        /// </summary>
        private class ContainerModel
        {
            public uint Block;
            public ObjectContainer Container;
            public bool Dirty;
            public bool IsRoot;

            /// <summary>
            /// Object container was empty when read.
            /// </summary>
            public bool WasEmpty;
        }

        /// <summary>
        /// Object (file, directory or soft link) in directory tree.
        /// </summary>
        private class ObjModel
        {
            public SfsObject Object;
            public ContainerModel Container;
            public ObjModel Parent;
            public string Path;

            /// <summary>
            /// Object node number read from disk.
            /// </summary>
            public uint DiskNode;

            public bool Removed;
            public bool NeedsNewNode;

            /* directory */
            public readonly List<ContainerModel> DirContainers = new List<ContainerModel>();
            public readonly List<ObjModel> Children = new List<ObjModel>();
            public HashTableBlock HashTable;
            public bool HashTableRebuild;

            /// <summary>
            /// Objects in directory were removed by repair.
            /// </summary>
            public bool ChildrenRemoved;

            /* file */
            public List<ExtentBNode> Extents = new List<ExtentBNode>();

            /* soft link */
            public SoftLinkBlock SoftLink;
            public bool SoftLinkDirty;

            /* desired hash chain values for object node */
            public uint HashNext;
            public ushort Hash16;

            public bool IsRootObject => Parent == null;
        }
    }
}
