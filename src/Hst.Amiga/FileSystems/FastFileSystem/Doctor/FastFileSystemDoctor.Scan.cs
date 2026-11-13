namespace Hst.Amiga.FileSystems.FastFileSystem.Doctor
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Converters;

    /// <summary>
    /// Scan of boot block, root block and directory tree.
    /// </summary>
    public partial class FastFileSystemDoctor
    {
        /// <summary>
        /// Reference to a block pointer in a hash table slot or a hash chain field.
        /// </summary>
        private struct PointerRef
        {
            public uint Block;
            public int Offset;
        }

        /// <summary>
        /// Directory to scan.
        /// </summary>
        private class ScanDirectory
        {
            public uint Block;
            public string Path;
        }

        /// <summary>
        /// Hard link found in directory tree, validated when directory tree is scanned.
        /// </summary>
        private class ScanLink
        {
            public uint Block;
            public uint Directory;
            public uint RealEntry;
            public string Path;
        }

        /// <summary>
        /// Blocks used by root block, entries, file data, dir cache and bitmap.
        /// </summary>
        private BitArray used;

        /// <summary>
        /// Entry blocks found in directory tree.
        /// </summary>
        private HashSet<uint> entryBlocks;

        /// <summary>
        /// Entry blocks in each directory (root block for root directory), used for dir cache check.
        /// </summary>
        private Dictionary<uint, List<uint>> directoryEntries;

        /// <summary>
        /// Path of each directory, used for messages.
        /// </summary>
        private Dictionary<uint, string> directoryPaths;

        private List<ScanLink> links;

        private async Task Scan()
        {
            used = new BitArray((int)volume.Blocks);
            entryBlocks = new HashSet<uint>();
            directoryEntries = new Dictionary<uint, List<uint>>();
            directoryPaths = new Dictionary<uint, string>();
            links = new List<ScanLink>();

            ShowMsg($"{(Repair ? "Repairing" : "Checking")} volume{(string.IsNullOrEmpty(volume.DeviceName) ? string.Empty : $" {volume.DeviceName}")}");

            if (!await CheckBootBlock())
            {
                Aborted = true;
                return;
            }

            if (!await CheckRootBlock())
            {
                Aborted = true;
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await CheckDirectoryTree();

            cancellationToken.ThrowIfCancellationRequested();
            await CheckLinks();

            /* dir cache blocks are checked before bitmap pointers, so valid dir cache blocks are marked used */
            cancellationToken.ThrowIfCancellationRequested();
            if (useDirCache)
            {
                await CheckDirCaches();
            }

            /* bitmap blocks are marked used before new blocks are allocated for dir caches */
            cancellationToken.ThrowIfCancellationRequested();
            await CheckBitmapPointers();
            await RebuildDirCaches();

            cancellationToken.ThrowIfCancellationRequested();
            await CheckBitmapContent();

            await WriteChangedBlocks();

            ShowMsg($"Directories: {stats.Directories}, files: {stats.Files}, hard links: {stats.HardLinks}, soft links: {stats.SoftLinks}");
            ShowMsg($"Blocks used: {stats.UsedBlocks}, blocks free: {stats.FreeBlocks}");
            ShowMsg($"Errors: {stats.Errors}, warnings: {stats.Warnings}, fixed: {stats.Fixed}");
        }

        /// <summary>
        /// Check boot block dos type. Dos type of volume (from partition) is used, if set. Otherwise dos type is
        /// read from boot block.
        /// </summary>
        private async Task<bool> CheckBootBlock()
        {
            var bootBlock = await ReadRawBlock(0, 1024);
            var bootDosType = BigEndianConverter.ConvertBytesToUInt32(bootBlock);
            var dosType = volume.DosType;

            if (dosType == 0)
            {
                if (!IsFastFileSystemDosType(bootDosType))
                {
                    AddIssue(IssueType.NotDosDisk, 0, null,
                        $"Boot block dos type {FormatDosType(bootDosType)} is not a Fast File System dos type", fixable: false);
                    return false;
                }

                dosType = bootDosType;
            }
            else if (!IsFastFileSystemDosType(dosType))
            {
                AddIssue(IssueType.NotDosDisk, 0, null,
                    $"Dos type {FormatDosType(dosType)} is not a Fast File System dos type", fixable: false);
                return false;
            }
            else if (bootDosType != dosType)
            {
                AddIssue(IssueType.BootBlockDosType, 0, null,
                    $"Boot block dos type {FormatDosType(bootDosType)} doesn't match dos type {FormatDosType(dosType)}");
                if (Repair)
                {
                    /* keep boot block checksum valid, if boot block was bootable */
                    var bootable = BigEndianConverter.ConvertBytesToInt32(bootBlock, 4) ==
                                   BootBlockChecksumHelper.CalculateChecksum(bootBlock);
                    BigEndianConverter.ConvertUInt32ToBytes(dosType, bootBlock, 0);
                    if (bootable)
                    {
                        BootBlockChecksumHelper.UpdateChecksum(bootBlock, 4);
                    }

                    /* boot block spans two 512 byte blocks, written as raw bytes */
                    await WriteBootBlock(bootBlock);
                }
            }

            SetDosType(dosType);
            ShowMsg($"Dos type {FormatDosType(dosType)}, block size {volume.BlockSize}, blocks {volume.Blocks}, root block {volume.RootBlock}");
            return true;
        }

        private async Task WriteBootBlock(byte[] bootBlock)
        {
            /* split boot block in file system blocks, so it's written together with other changed blocks */
            for (var offset = 0; offset < bootBlock.Length; offset += volume.BlockSize)
            {
                var block = (uint)(offset / volume.BlockSize);
                var data = await ReadBlock(block) ?? new byte[volume.BlockSize];
                Array.Copy(bootBlock, offset, data, 0, Math.Min(volume.BlockSize, bootBlock.Length - offset));
                ChangeBlock(block, data, -1);
            }
        }

        /// <summary>
        /// Check root block. Root block with checksum error, but valid structure, gets checksum updated. Root block
        /// with invalid structure is rebuilt from entry blocks with root block as parent in repair mode.
        /// </summary>
        private async Task<bool> CheckRootBlock()
        {
            var root = await ReadBlock(volume.RootBlock);
            if (root == null)
            {
                AddIssue(IssueType.RootBlockInvalid, volume.RootBlock, null, "Root block is outside volume",
                    fixable: false);
                return false;
            }

            if (IsRootBlockStructure(root))
            {
                if (!ChecksumValid(root))
                {
                    AddIssue(IssueType.RootBlockChecksum, volume.RootBlock, null, "Root block has checksum error");
                    if (Repair)
                    {
                        ChangeBlock(volume.RootBlock, root);
                    }
                }

                if (!IsDateValid(root, OffsetRootDate))
                {
                    AddIssue(IssueType.InvalidDate, volume.RootBlock, null, "Root block has invalid date", true);
                    if (Repair)
                    {
                        FixDate(root, OffsetRootDate);
                        ChangeBlock(volume.RootBlock, root);
                    }
                }

                used[(int)volume.RootBlock] = true;
                ShowMsg($"Volume name '{ReadName(root, true)}'");
                return true;
            }

            if (!Repair)
            {
                AddIssue(IssueType.RootBlockInvalid, volume.RootBlock, null,
                    "Root block is invalid, repair can rebuild root block from entries found or use salvage to recover files",
                    fixable: false);
                return false;
            }

            AddIssue(IssueType.RootBlockInvalid, volume.RootBlock, null,
                "Root block is invalid, rebuilding root block from entries found");
            await RebuildRootBlock(root);
            used[(int)volume.RootBlock] = true;
            return true;
        }

        /// <summary>
        /// Rebuild root block. All blocks in volume are scanned for entry blocks with root block as parent, which
        /// are inserted in hash table of new root block. Bitmap blocks are allocated when bitmap is checked.
        /// </summary>
        private async Task RebuildRootBlock(byte[] oldRoot)
        {
            var name = IsNameValid(oldRoot) && oldRoot[OffsetName] > 0 && oldRoot[OffsetName] <= Constants.MAXNAMELEN
                ? ReadName(oldRoot, true)
                : "Recovered";

            var root = new byte[volume.BlockSize];
            SetUInt32(root, OffsetType, Constants.T_HEADER);
            SetUInt32(root, OffsetHashTableSize, (uint)tableSize);
            SetUInt32(root, OffsetBitmapFlag, Constants.BM_INVALID);
            var now = DateTime.Now;
            DateHelper.WriteDate(root, OffsetRootDate, now);
            DateHelper.WriteDate(root, volume.BlockSize - 0x28, now);
            DateHelper.WriteDate(root, volume.BlockSize - 0x1c, now);
            var nameBytes = AmigaTextHelper.GetBytes(name);
            root[OffsetName] = (byte)nameBytes.Length;
            Array.Copy(nameBytes, 0, root, OffsetName + 1, nameBytes.Length);
            SetUInt32(root, OffsetSecType, unchecked((uint)Constants.ST_ROOT));

            ShowMsg("Scanning volume for entries in root directory");
            var found = 0;
            await ScanBlocks(async (block, data) =>
            {
                if (block == volume.RootBlock || !IsValidEntryBlock(block, data) ||
                    GetUInt32(data, OffsetParent) != volume.RootBlock || !IsNameValid(data))
                {
                    return;
                }

                /* insert entry first in hash chain */
                var copy = (byte[])data.Clone();
                var slot = HashName(ReadName(copy));
                SetUInt32(copy, OffsetHashChain, GetUInt32(root, TableOffset(slot)));
                SetUInt32(root, TableOffset(slot), block);
                ChangeBlock(block, copy);
                found++;
                await Task.CompletedTask;
            });

            ChangeBlock(volume.RootBlock, root);
            ShowMsg($"Root block rebuilt with volume name '{name}' and {found} entries");
        }

        /// <summary>
        /// Read all blocks in volume managed by bitmap in chunks and call action for each block.
        /// </summary>
        private async Task ScanBlocks(Func<uint, byte[], Task> action)
        {
            const int blocksPerChunk = 256;

            for (var block = volume.Reserved; block < volume.Blocks; block += blocksPerChunk)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = (int)Math.Min(blocksPerChunk, volume.Blocks - block);
                var chunk = await ReadRawBlock(block, count * volume.BlockSize);
                for (var i = 0; i < count; i++)
                {
                    var current = block + (uint)i;
                    byte[] data;
                    if (!changedBlocks.TryGetValue(current, out data))
                    {
                        data = new byte[volume.BlockSize];
                        Array.Copy(chunk, i * volume.BlockSize, data, 0, volume.BlockSize);
                    }

                    await action(current, data);
                }
            }
        }

        /// <summary>
        /// Check directory tree from root directory.
        /// </summary>
        private async Task CheckDirectoryTree()
        {
            var directories = new Stack<ScanDirectory>();
            directories.Push(new ScanDirectory { Block = volume.RootBlock, Path = string.Empty });

            while (directories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = directories.Pop();
                await CheckDirectory(directory, directories);
            }
        }

        private PointerRef SlotRef(uint directory, int slot) =>
            new PointerRef { Block = directory, Offset = TableOffset(slot) };

        private async Task<uint> GetPointer(PointerRef pointerRef)
        {
            var data = await ReadBlock(pointerRef.Block);
            return GetUInt32(data, pointerRef.Offset);
        }

        /// <summary>
        /// Set pointer in hash table slot or hash chain field in repair mode.
        /// </summary>
        private async Task SetPointer(PointerRef pointerRef, uint value)
        {
            if (!Repair)
            {
                return;
            }

            var data = await ReadBlock(pointerRef.Block);
            SetUInt32(data, pointerRef.Offset, value);
            ChangeBlock(pointerRef.Block, data);
        }

        private static string CombinePath(string path, string name) =>
            string.IsNullOrEmpty(path) ? name : string.Concat(path, "/", name);

        /// <summary>
        /// Check directory by walking all hash chains in its hash table.
        /// </summary>
        private async Task CheckDirectory(ScanDirectory directory, Stack<ScanDirectory> directories)
        {
            var names = new HashSet<string>();
            var entries = new List<uint>();
            var moves = new List<uint>();
            directoryEntries[directory.Block] = entries;
            directoryPaths[directory.Block] = directory.Path;

            for (var slot = 0; slot < tableSize; slot++)
            {
                var pointerRef = SlotRef(directory.Block, slot);
                var current = await GetPointer(pointerRef);

                while (current != 0)
                {
                    if (!InRange(current))
                    {
                        AddIssue(IssueType.HashChainOutOfRange, pointerRef.Block, directory.Path,
                            $"Hash chain points to block {current} outside volume, hash chain is truncated");
                        await SetPointer(pointerRef, 0);
                        break;
                    }

                    if (entryBlocks.Contains(current))
                    {
                        AddIssue(IssueType.HashChainLoop, pointerRef.Block, directory.Path,
                            $"Hash chain points to entry block {current} already found, hash chain is truncated");
                        await SetPointer(pointerRef, 0);
                        break;
                    }

                    var data = await ReadBlock(current);
                    if (!IsEntryBlockStructure(current, data))
                    {
                        AddIssue(IssueType.InvalidEntryBlock, pointerRef.Block, directory.Path,
                            $"Hash chain points to block {current}, which is not a valid entry block, hash chain is truncated");
                        await SetPointer(pointerRef, 0);
                        break;
                    }

                    var name = ReadName(data);
                    var path = CombinePath(directory.Path, name);

                    if (!ChecksumValid(data))
                    {
                        /* only trust entry block with checksum error, if it's in the right place */
                        if (GetUInt32(data, OffsetParent) != directory.Block || !IsNameValid(data) ||
                            HashName(name) != slot)
                        {
                            AddIssue(IssueType.InvalidEntryBlock, current, path,
                                "Entry block has checksum error and doesn't belong in directory, hash chain is truncated");
                            await SetPointer(pointerRef, 0);
                            break;
                        }

                        AddIssue(IssueType.EntryBlockChecksum, current, path, "Entry block has checksum error");
                        if (Repair)
                        {
                            ChangeBlock(current, data);
                        }
                    }

                    if (used[(int)current])
                    {
                        AddIssue(IssueType.CrossLinkedBlock, current, path,
                            "Entry block is already used by another entry, hash chain is truncated");
                        await SetPointer(pointerRef, 0);
                        break;
                    }

                    entryBlocks.Add(current);
                    used[(int)current] = true;
                    var next = GetUInt32(data, OffsetHashChain);

                    if (GetUInt32(data, OffsetParent) != directory.Block)
                    {
                        AddIssue(IssueType.WrongParent, current, path,
                            $"Entry parent is block {GetUInt32(data, OffsetParent)}, but entry is in directory block {directory.Block}");
                        if (Repair)
                        {
                            SetUInt32(data, OffsetParent, directory.Block);
                            ChangeBlock(current, data);
                        }
                    }

                    if (!IsNameValid(data))
                    {
                        var newName = CreateName(string.Empty, $"Entry_{current}");
                        AddIssue(IssueType.InvalidName, current, path,
                            $"Entry name is invalid, entry is renamed to '{newName}'");
                        if (Repair)
                        {
                            name = newName;
                            path = CombinePath(directory.Path, name);
                            WriteName(data, name);
                            ChangeBlock(current, data);
                        }
                    }

                    var upperName = UpperName(name);
                    if (names.Contains(upperName))
                    {
                        var newName = CreateName(name, $"_{current}");
                        AddIssue(IssueType.DuplicateName, current, path,
                            $"Entry name is already used in directory, entry is renamed to '{newName}'");
                        if (Repair)
                        {
                            name = newName;
                            path = CombinePath(directory.Path, name);
                            upperName = UpperName(name);
                            WriteName(data, name);
                            ChangeBlock(current, data);
                        }
                    }

                    names.Add(upperName);

                    var unlinked = false;
                    if (HashName(name) != slot)
                    {
                        AddIssue(IssueType.WrongHashChain, current, path,
                            $"Entry is in hash chain {slot}, but name hash is {HashName(name)}, entry is moved");

                        if (Repair)
                        {
                            /* unlink entry, it's inserted in right hash chain when directory is checked */
                            await SetPointer(pointerRef, next);
                            SetUInt32(data, OffsetHashChain, 0);
                            ChangeBlock(current, data);
                            moves.Add(current);
                            unlinked = true;
                        }
                    }

                    if (!IsDateValid(data, OffsetDate))
                    {
                        AddIssue(IssueType.InvalidDate, current, path, "Entry has invalid date", true);
                        if (Repair)
                        {
                            FixDate(data, OffsetDate);
                            ChangeBlock(current, data);
                        }
                    }

                    var keep = await CheckEntry(directory, current, data, path, directories);
                    if (keep)
                    {
                        entries.Add(current);
                    }
                    else
                    {
                        /* entry is removed from directory */
                        entryBlocks.Remove(current);
                        used[(int)current] = false;
                        names.Remove(upperName);
                        if (Repair && !unlinked)
                        {
                            await SetPointer(pointerRef, next);
                            unlinked = true;
                        }

                        moves.Remove(current);
                    }

                    if (!unlinked)
                    {
                        pointerRef = new PointerRef { Block = current, Offset = OffsetHashChain };
                    }

                    current = next;
                }
            }

            /* insert moved entries first in hash chain matching their name */
            foreach (var block in moves)
            {
                var data = await ReadBlock(block);
                var slot = HashName(ReadName(data));
                var slotRef = SlotRef(directory.Block, slot);
                SetUInt32(data, OffsetHashChain, await GetPointer(slotRef));
                ChangeBlock(block, data);
                await SetPointer(slotRef, block);
            }
        }

        /// <summary>
        /// Check entry by type. Returns false, if entry is removed from directory.
        /// </summary>
        private async Task<bool> CheckEntry(ScanDirectory directory, uint block, byte[] data, string path,
            Stack<ScanDirectory> directories)
        {
            var secType = GetInt32(data, OffsetSecType);
            switch (secType)
            {
                case Constants.ST_DIR:
                    stats.Directories++;
                    await CheckLnfsCommentBlock(block, data, path);
                    directories.Push(new ScanDirectory { Block = block, Path = path });
                    return true;
                case Constants.ST_FILE:
                    stats.Files++;
                    await CheckLnfsCommentBlock(block, data, path);
                    await CheckFile(block, data, path);
                    return true;
                case Constants.ST_LFILE:
                case Constants.ST_LDIR:
                    return await CheckHardLink(directory, block, data, path);
                case Constants.ST_LSOFT:
                    stats.SoftLinks++;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Set invalid minutes and ticks in date to 0.
        /// </summary>
        private static void FixDate(byte[] data, int offset)
        {
            if (GetUInt32(data, offset) >= 0x7fffffff)
            {
                SetUInt32(data, offset, 0);
            }

            if (GetUInt32(data, offset + 4) >= 24 * 60)
            {
                SetUInt32(data, offset + 4, 0);
            }

            if (GetUInt32(data, offset + 8) >= 60 * 50)
            {
                SetUInt32(data, offset + 8, 0);
            }
        }

        /// <summary>
        /// Check long name file system comment block of entry.
        /// </summary>
        private async Task CheckLnfsCommentBlock(uint block, byte[] data, string path)
        {
            if (!useLnfs)
            {
                return;
            }

            var commentBlock = GetUInt32(data, OffsetLnfsCommentBlock);
            if (commentBlock == 0)
            {
                return;
            }

            var comment = InRange(commentBlock) && !used[(int)commentBlock] ? await ReadBlock(commentBlock) : null;
            if (comment == null ||
                GetInt32(comment, OffsetType) != Constants.TYPE_COMMENT ||
                GetUInt32(comment, 0x4) != commentBlock ||
                GetUInt32(comment, 0x8) != block ||
                !ChecksumValid(comment))
            {
                AddIssue(IssueType.InvalidCommentBlock, block, path,
                    $"Comment block {commentBlock} is invalid, comment is removed");
                if (Repair)
                {
                    SetUInt32(data, OffsetLnfsCommentBlock, 0);
                    ChangeBlock(block, data);
                }

                return;
            }

            used[(int)commentBlock] = true;
        }

        /// <summary>
        /// Find entry in directory and unlink it from its hash chain in repair mode.
        /// </summary>
        private async Task UnlinkEntry(uint directory, uint block)
        {
            if (!Repair)
            {
                return;
            }

            for (var slot = 0; slot < tableSize; slot++)
            {
                var pointerRef = SlotRef(directory, slot);
                var current = await GetPointer(pointerRef);
                var guard = new HashSet<uint>();
                while (current != 0 && InRange(current) && guard.Add(current))
                {
                    var data = await ReadBlock(current);
                    if (current == block)
                    {
                        await SetPointer(pointerRef, GetUInt32(data, OffsetHashChain));
                        return;
                    }

                    pointerRef = new PointerRef { Block = current, Offset = OffsetHashChain };
                    current = GetUInt32(data, OffsetHashChain);
                }
            }
        }

        private int CountUsedBlocks()
        {
            var count = 0;
            for (var i = (int)volume.Reserved; i < used.Length; i++)
            {
                if (used[i])
                {
                    count++;
                }
            }

            return count;
        }

        private IEnumerable<uint> FormatBlocks(IEnumerable<uint> blocks) => blocks.Take(MaxListedBlocks);
    }
}
