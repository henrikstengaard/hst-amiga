namespace Hst.Amiga.FileSystems.FastFileSystem.Doctor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Converters;
    using Salvage;

    /// <summary>
    /// Salvage of directories and files.
    /// </summary>
    public partial class FastFileSystemDoctor
    {
        /// <summary>
        /// Maximum directory depth salvaged, guards against corrupt directory structures.
        /// </summary>
        private const int SalvageMaxDepth = 256;

        /// <summary>
        /// Entry found by salvage.
        /// </summary>
        private class SalvageNode
        {
            public uint Block;
            public int SecType;
            public SalvageEntry Entry;
            public List<SalvageNode> Children { get; } = new List<SalvageNode>();
        }

        private SalvageResult salvageResult;

        /// <summary>
        /// Valid entry blocks found in volume.
        /// </summary>
        private Dictionary<uint, byte[]> salvageEntryBlocks;

        /// <summary>
        /// Valid entry blocks found in volume by parent.
        /// </summary>
        private Dictionary<uint, List<uint>> salvageEntryBlocksByParent;

        /// <summary>
        /// Valid OFS data blocks found in volume by header key and sequence number.
        /// </summary>
        private Dictionary<(uint, uint), uint> salvageOfsDataBlocks;

        /// <summary>
        /// Entry blocks added to directory tree.
        /// </summary>
        private HashSet<uint> salvageReached;

        /// <summary>
        /// Salvage directories and files from a Fast File System volume and re-create them in target. All blocks in
        /// the volume are scanned for entry blocks, so directories and files can be salvaged even when the root
        /// block, hash chains or bitmap are damaged. Directories are read from the root directory by following hash
        /// chains. If a hash chain is broken, all entry blocks with the directory as parent are used. Entry blocks
        /// that can't be reached from the root directory are salvaged as orphaned entries in a lost directory.
        /// Protection bits, comments and dates are preserved. Hard links to files are salvaged as copies of the
        /// linked file, while soft links and hard links to directories are skipped and reported. The volume is never
        /// written to.
        /// </summary>
        /// <param name="volume">Volume to salvage, opened with <see cref="OpenVolume"/> or <see cref="OpenAdf"/>.</param>
        /// <param name="target">Target to re-create salvaged directories and files in.</param>
        /// <param name="options">Salvage options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result of salvage.</returns>
        public static async Task<SalvageResult> Salvage(DoctorVolume volume, ISalvageTarget target,
            SalvageOptions options = null, CancellationToken cancellationToken = default)
        {
            if (volume == null) throw new ArgumentNullException(nameof(volume));
            if (target == null) throw new ArgumentNullException(nameof(target));

            /* salvage runs in check mode, so the volume is never written to */
            var doctor = new FastFileSystemDoctor(volume, Mode.Check, cancellationToken);
            return await doctor.RunSalvage(target, options ?? new SalvageOptions());
        }

        private void SalvageMessage(string message)
        {
            salvageResult.Messages.Add(message);
            ShowMsg(message);
        }

        private async Task<SalvageResult> RunSalvage(ISalvageTarget target, SalvageOptions options)
        {
            salvageResult = new SalvageResult();
            salvageEntryBlocks = new Dictionary<uint, byte[]>();
            salvageEntryBlocksByParent = new Dictionary<uint, List<uint>>();
            salvageOfsDataBlocks = new Dictionary<(uint, uint), uint>();
            salvageReached = new HashSet<uint>();

            await SalvageSetDosType();

            var root = await ReadBlock(volume.RootBlock);
            var rootValid = root != null && IsRootBlockStructure(root) && ChecksumValid(root);
            salvageResult.RootBlockFound = rootValid;
            if (!rootValid)
            {
                SalvageMessage($"Root block {volume.RootBlock} is invalid, entries with root block as parent are used for root directory");
            }

            await SalvageScanVolume();

            /* read directory tree from root directory */
            ShowMsg("Reading directory tree");
            var rootNode = new SalvageNode { Block = volume.RootBlock, SecType = Constants.ST_ROOT };
            if (rootValid)
            {
                await SalvageLoadDirectory(rootNode, root, volume.RootBlock, 0);
            }
            else
            {
                await SalvageAddEntriesByParent(rootNode, volume.RootBlock, 0);
            }

            if (options.SalvageOrphans)
            {
                await SalvageLoadOrphans(rootNode, options);
            }

            /* write directories and files to target */
            ShowMsg("Writing directories and files");
            await SalvageWriteChildren(target, new List<string>(), rootNode);
            await target.Flush();

            SalvageMessage(
                $"Salvaged {salvageResult.Directories} directories and {salvageResult.Files} files ({salvageResult.Bytes} bytes)");

            return salvageResult;
        }

        /// <summary>
        /// Set dos type from volume or boot block. If neither is a Fast File System dos type, DOS\3 is used.
        /// </summary>
        private async Task SalvageSetDosType()
        {
            var dosType = volume.DosType;
            if (!IsFastFileSystemDosType(dosType))
            {
                var bootBlock = await ReadRawBlock(0, 4);
                dosType = BigEndianConverter.ConvertBytesToUInt32(bootBlock);
            }

            if (!IsFastFileSystemDosType(dosType))
            {
                SalvageMessage($"Dos type {FormatDosType(dosType)} is not a Fast File System dos type, DOS\\3 is used");
                dosType = 0x444f5303;
            }

            SetDosType(dosType);
            ShowMsg($"Dos type {FormatDosType(dosType)}, block size {volume.BlockSize}, blocks {volume.Blocks}, root block {volume.RootBlock}");
        }

        /// <summary>
        /// Scan all blocks in volume for entry blocks and OFS data blocks.
        /// </summary>
        private async Task SalvageScanVolume()
        {
            ShowMsg("Scanning volume for entry blocks");
            await ScanBlocks((block, data) =>
            {
                if (IsValidEntryBlock(block, data))
                {
                    salvageEntryBlocks[block] = data;
                    var parent = GetUInt32(data, OffsetParent);
                    if (!salvageEntryBlocksByParent.TryGetValue(parent, out var children))
                    {
                        children = new List<uint>();
                        salvageEntryBlocksByParent[parent] = children;
                    }

                    children.Add(block);
                }
                else if (useOfs && GetInt32(data, OffsetType) == Constants.T_DATA &&
                         GetUInt32(data, OffsetDataSize) <= dataBlockSize && ChecksumValid(data))
                {
                    var key = (GetUInt32(data, OffsetHeaderKey), GetUInt32(data, OffsetHighSeq));
                    if (!salvageOfsDataBlocks.ContainsKey(key))
                    {
                        salvageOfsDataBlocks[key] = block;
                    }
                }

                return Task.CompletedTask;
            });

            ShowMsg($"Found {salvageEntryBlocks.Count} entry blocks");
        }

        /// <summary>
        /// Load directory by following hash chains. If a hash chain is broken, all entry blocks with directory as
        /// parent are added.
        /// </summary>
        private async Task SalvageLoadDirectory(SalvageNode node, byte[] directory, uint directoryBlock, int depth)
        {
            var broken = false;
            for (var slot = 0; slot < tableSize; slot++)
            {
                var block = GetUInt32(directory, TableOffset(slot));
                var guard = new HashSet<uint>();
                while (block != 0)
                {
                    if (!salvageEntryBlocks.TryGetValue(block, out var data) || !guard.Add(block))
                    {
                        broken = true;
                        break;
                    }

                    if (salvageReached.Contains(block))
                    {
                        break;
                    }

                    await SalvageAddChild(node, block, data, depth);
                    block = GetUInt32(data, OffsetHashChain);
                }
            }

            if (broken)
            {
                SalvageMessage($"Directory '{node.Entry?.Name ?? "/"}' has broken hash chain, entries with directory as parent are used");
                await SalvageAddEntriesByParent(node, directoryBlock, depth);
            }
        }

        /// <summary>
        /// Add entry blocks with directory as parent, which are not already added.
        /// </summary>
        private async Task SalvageAddEntriesByParent(SalvageNode node, uint directoryBlock, int depth)
        {
            if (!salvageEntryBlocksByParent.TryGetValue(directoryBlock, out var children))
            {
                return;
            }

            foreach (var child in children.ToList())
            {
                if (!salvageReached.Contains(child))
                {
                    await SalvageAddChild(node, child, salvageEntryBlocks[child], depth);
                }
            }
        }

        /// <summary>
        /// Add entry to directory. If an entry with same name exists, the newest entry is kept.
        /// </summary>
        private async Task SalvageAddChild(SalvageNode node, uint block, byte[] data, int depth)
        {
            salvageReached.Add(block);

            var child = new SalvageNode
            {
                Block = block,
                SecType = GetInt32(data, OffsetSecType),
                Entry = await SalvageCreateEntry(data)
            };

            if (child.SecType == Constants.ST_DIR)
            {
                if (depth < SalvageMaxDepth)
                {
                    await SalvageLoadDirectory(child, data, block, depth + 1);
                }
                else
                {
                    SalvageMessage($"Directory '{child.Entry.Name}' exceeds maximum depth {SalvageMaxDepth}, entries are skipped");
                }
            }

            var upperName = UpperName(child.Entry.Name);
            var existing = node.Children.FirstOrDefault(x => UpperName(x.Entry.Name) == upperName);
            if (existing != null)
            {
                if (existing.Entry.Date >= child.Entry.Date)
                {
                    SalvageMessage($"Entry '{child.Entry.Name}' in block {block} has same name as newer entry in block {existing.Block}, entry is skipped");
                    salvageResult.Skipped++;
                    return;
                }

                SalvageMessage($"Entry '{existing.Entry.Name}' in block {existing.Block} has same name as newer entry in block {block}, entry is skipped");
                salvageResult.Skipped++;
                node.Children.Remove(existing);
            }

            node.Children.Add(child);
        }

        private async Task<SalvageEntry> SalvageCreateEntry(byte[] data)
        {
            var secType = GetInt32(data, OffsetSecType);
            var name = ReadName(data);
            if (!IsNameValid(data))
            {
                name = CreateName(name, $"_{GetUInt32(data, OffsetHeaderKey)}");
            }

            var comment = ReadComment(data);
            if (useLnfs)
            {
                var commentBlock = GetUInt32(data, OffsetLnfsCommentBlock);
                var commentData = commentBlock != 0 && InRange(commentBlock) ? await ReadBlock(commentBlock) : null;
                if (commentData != null && GetInt32(commentData, OffsetType) == Constants.TYPE_COMMENT &&
                    ChecksumValid(commentData))
                {
                    comment = AmigaTextHelper.GetString(commentData, 0x19,
                        Math.Min((int)commentData[0x18], Constants.MAXCMMTLEN));
                }
            }

            return new SalvageEntry
            {
                Name = name,
                Type = secType == Constants.ST_DIR ? EntryType.Dir : EntryType.File,
                Size = secType == Constants.ST_FILE ? GetUInt32(data, OffsetByteSize) : 0,
                Protection = GetUInt32(data, OffsetAccess),
                Date = ReadDate(data, OffsetDate),
                Comment = comment
            };
        }

        /// <summary>
        /// Load entry blocks not reached from root directory as orphans in lost directory. Orphans are grouped by
        /// their parent block in sub directories of lost directory.
        /// </summary>
        private async Task SalvageLoadOrphans(SalvageNode rootNode, SalvageOptions options)
        {
            var lostNode = new SalvageNode
            {
                SecType = Constants.ST_DIR,
                Entry = new SalvageEntry
                {
                    Name = options.LostDirectoryName,
                    Type = EntryType.Dir,
                    Date = DateTime.Now,
                    Comment = string.Empty
                }
            };
            var groups = new Dictionary<uint, SalvageNode>();

            foreach (var block in salvageEntryBlocks.Keys.OrderBy(x => x).ToList())
            {
                if (salvageReached.Contains(block))
                {
                    continue;
                }

                /* find top most unreached directory, so directory structure of orphans is kept */
                var top = block;
                var guard = new HashSet<uint> { top };
                while (true)
                {
                    var parent = GetUInt32(salvageEntryBlocks[top], OffsetParent);
                    if (!salvageEntryBlocks.TryGetValue(parent, out var parentData) ||
                        salvageReached.Contains(parent) ||
                        GetInt32(parentData, OffsetSecType) != Constants.ST_DIR ||
                        !guard.Add(parent))
                    {
                        break;
                    }

                    top = parent;
                }

                var groupBlock = GetUInt32(salvageEntryBlocks[top], OffsetParent);
                if (!groups.TryGetValue(groupBlock, out var group))
                {
                    group = new SalvageNode
                    {
                        Block = groupBlock,
                        SecType = Constants.ST_DIR,
                        Entry = new SalvageEntry
                        {
                            Name = $"Dir_{groupBlock}",
                            Type = EntryType.Dir,
                            Date = DateTime.Now,
                            Comment = string.Empty
                        }
                    };
                    groups[groupBlock] = group;
                    lostNode.Children.Add(group);
                }

                await SalvageAddChild(group, top, salvageEntryBlocks[top], 1);
            }

            if (groups.Count == 0)
            {
                return;
            }

            salvageResult.OrphanedDirectories = groups.Count;
            SalvageMessage($"Found orphaned entries in {groups.Count} directories, salvaged in '{options.LostDirectoryName}'");

            /* lost directory gets a unique name, if root directory has entry with same name */
            var lostName = lostNode.Entry.Name;
            var index = 1;
            while (rootNode.Children.Any(x => UpperName(x.Entry.Name) == UpperName(lostNode.Entry.Name)))
            {
                lostNode.Entry.Name = $"{lostName}.{index++}";
            }

            rootNode.Children.Add(lostNode);
        }

        private async Task SalvageWriteChildren(ISalvageTarget target, List<string> parentPath, SalvageNode node)
        {
            foreach (var child in node.Children)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = string.Join("/", parentPath.Concat(new[] { child.Entry.Name }));

                try
                {
                    switch (child.SecType)
                    {
                        case Constants.ST_DIR:
                            await target.CreateDirectory(parentPath, child.Entry);
                            salvageResult.Directories++;
                            await SalvageWriteChildren(target, parentPath.Concat(new[] { child.Entry.Name }).ToList(),
                                child);
                            await target.SetDirectoryMetadata(parentPath, child.Entry);
                            break;
                        case Constants.ST_FILE:
                            await SalvageWriteFile(target, parentPath, child.Entry, child.Block,
                                salvageEntryBlocks.TryGetValue(child.Block, out var data) ? data : null, path);
                            break;
                        case Constants.ST_LFILE:
                            await SalvageWriteHardLink(target, parentPath, child, path);
                            break;
                        case Constants.ST_LDIR:
                            SalvageMessage($"Hard link to directory '{path}' is skipped");
                            salvageResult.Skipped++;
                            break;
                        case Constants.ST_LSOFT:
                            SalvageMessage($"Soft link '{path}' is skipped");
                            salvageResult.Skipped++;
                            break;
                    }
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    SalvageMessage($"Failed to salvage '{path}': {e.Message}");
                    salvageResult.Errors++;
                }
            }
        }

        private async Task SalvageWriteHardLink(ISalvageTarget target, List<string> parentPath, SalvageNode link,
            string path)
        {
            var realEntry = GetUInt32(salvageEntryBlocks[link.Block], OffsetRealEntry);
            if (!salvageEntryBlocks.TryGetValue(realEntry, out var real) ||
                GetInt32(real, OffsetSecType) != Constants.ST_FILE)
            {
                SalvageMessage($"Hard link '{path}' points to block {realEntry}, which is not a valid file, hard link is skipped");
                salvageResult.Skipped++;
                return;
            }

            /* hard link is salvaged as copy of linked file with name of hard link */
            var entry = await SalvageCreateEntry(real);
            entry.Name = link.Entry.Name;
            await SalvageWriteFile(target, parentPath, entry, realEntry, real, path);
        }

        private async Task SalvageWriteFile(ISalvageTarget target, List<string> parentPath, SalvageEntry entry,
            uint block, byte[] header, string path)
        {
            var (blocks, length, complete) = await SalvageGetDataBlocks(block, header);
            entry.Size = length;

            using (var stream = new DataBlockStream(volume, blocks, useOfs, length))
            {
                await target.WriteFile(parentPath, entry, stream);
            }

            salvageResult.Files++;
            salvageResult.Bytes += length;

            if (!complete)
            {
                salvageResult.PartialFiles++;
                SalvageMessage($"File '{path}' is partially salvaged, {length} of {GetUInt32(header, OffsetByteSize)} bytes");
            }
        }

        /// <summary>
        /// Get data blocks of file by following data block pointers in file header and file extension blocks. For
        /// OFS, data blocks found by scan are used, if data block pointers are broken.
        /// </summary>
        private async Task<(IList<uint>, long, bool)> SalvageGetDataBlocks(uint block, byte[] header)
        {
            var byteSize = GetUInt32(header, OffsetByteSize);
            var needed = (int)((byteSize + (long)dataBlockSize - 1) / dataBlockSize);
            var pointers = new List<uint>();
            var complete = true;

            /* read data block pointers */
            var container = header;
            var guard = new HashSet<uint> { block };
            while (pointers.Count < needed)
            {
                var highSeq = Math.Min(GetUInt32(container, OffsetHighSeq), (uint)tableSize);
                for (var i = 0; i < highSeq; i++)
                {
                    pointers.Add(GetUInt32(container, DataPointerOffset(i)));
                }

                var extension = GetUInt32(container, OffsetExtension);
                if (extension == 0)
                {
                    break;
                }

                var extensionData = InRange(extension) && guard.Add(extension) ? await ReadBlock(extension) : null;
                if (extensionData == null ||
                    GetInt32(extensionData, OffsetType) != Constants.T_LIST ||
                    GetUInt32(extensionData, OffsetHeaderKey) != extension ||
                    !ChecksumValid(extensionData))
                {
                    break;
                }

                container = extensionData;
            }

            /* validate data blocks */
            var blocks = new List<uint>();
            long readable = 0;
            for (var seq = 1; seq <= needed; seq++)
            {
                var pointer = seq - 1 < pointers.Count ? pointers[seq - 1] : 0;
                if (useOfs)
                {
                    var data = InRange(pointer) ? await ReadBlock(pointer) : null;
                    if (data == null ||
                        GetInt32(data, OffsetType) != Constants.T_DATA ||
                        GetUInt32(data, OffsetHeaderKey) != block ||
                        GetUInt32(data, OffsetHighSeq) != seq ||
                        GetUInt32(data, OffsetDataSize) > dataBlockSize ||
                        !ChecksumValid(data))
                    {
                        if (!salvageOfsDataBlocks.TryGetValue((block, (uint)seq), out pointer))
                        {
                            complete = false;
                            break;
                        }

                        data = await ReadBlock(pointer);
                    }

                    readable += GetUInt32(data, OffsetDataSize);
                }
                else
                {
                    if (!InRange(pointer))
                    {
                        complete = false;
                        break;
                    }

                    readable += dataBlockSize;
                }

                blocks.Add(pointer);
            }

            var length = Math.Min(readable, byteSize);
            if (length < byteSize)
            {
                complete = false;
            }

            return (blocks, length, complete);
        }
    }
}
