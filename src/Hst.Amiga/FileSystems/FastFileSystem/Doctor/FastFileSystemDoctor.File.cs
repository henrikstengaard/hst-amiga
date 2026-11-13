namespace Hst.Amiga.FileSystems.FastFileSystem.Doctor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Check of files and hard links.
    /// </summary>
    public partial class FastFileSystemDoctor
    {
        /// <summary>
        /// Check file header, file extension blocks and data blocks. Data block list is truncated at first invalid
        /// data block pointer, data block or file extension block, and file size is updated to match data blocks
        /// in repair mode.
        /// </summary>
        private async Task CheckFile(uint block, byte[] header, string path)
        {
            var byteSize = GetUInt32(header, OffsetByteSize);
            var dataBlocks = new List<uint>();
            var extensionBlocks = new List<uint>();
            var ofsDataSizes = new List<uint>();
            var rewrite = false;
            var broken = false;

            var container = header;
            var containerBlock = block;
            var guard = new HashSet<uint> { block };

            while (true)
            {
                var highSeq = GetUInt32(container, OffsetHighSeq);
                if (highSeq > tableSize)
                {
                    AddIssue(IssueType.InvalidFileBlockList, containerBlock, path,
                        $"High seq {highSeq} is larger than table size {tableSize}");
                    highSeq = (uint)tableSize;
                    rewrite = true;
                }

                for (var i = 0; i < highSeq && !broken; i++)
                {
                    var dataBlock = GetUInt32(container, DataPointerOffset(i));
                    var seq = dataBlocks.Count + 1;

                    if (!InRange(dataBlock) || used[(int)dataBlock])
                    {
                        AddIssue(
                            InRange(dataBlock) ? IssueType.CrossLinkedBlock : IssueType.InvalidDataBlockPointer,
                            containerBlock, path,
                            InRange(dataBlock)
                                ? $"Data block {dataBlock} for sequence {seq} is already used by another entry, file is truncated"
                                : $"Data block pointer {dataBlock} for sequence {seq} is outside volume, file is truncated");
                        broken = true;
                        break;
                    }

                    if (useOfs)
                    {
                        var data = await ReadBlock(dataBlock);
                        var dataSize = GetUInt32(data, OffsetDataSize);
                        if (GetInt32(data, OffsetType) != Constants.T_DATA ||
                            GetUInt32(data, OffsetHeaderKey) != block ||
                            GetUInt32(data, OffsetHighSeq) != seq ||
                            dataSize > dataBlockSize ||
                            !ChecksumValid(data))
                        {
                            AddIssue(IssueType.InvalidDataBlock, dataBlock, path,
                                $"Data block for sequence {seq} is invalid, file is truncated");
                            broken = true;
                            break;
                        }

                        ofsDataSizes.Add(dataSize);
                    }

                    used[(int)dataBlock] = true;
                    dataBlocks.Add(dataBlock);
                }

                if (broken)
                {
                    break;
                }

                var extension = GetUInt32(container, OffsetExtension);
                if (extension == 0)
                {
                    break;
                }

                if (highSeq != tableSize)
                {
                    AddIssue(IssueType.InvalidFileBlockList, containerBlock, path,
                        $"Block has file extension block, but only {highSeq} of {tableSize} data block pointers are used");
                    rewrite = true;
                }

                var extensionData = InRange(extension) && !used[(int)extension] && !guard.Contains(extension)
                    ? await ReadBlock(extension)
                    : null;
                if (extensionData == null ||
                    GetInt32(extensionData, OffsetType) != Constants.T_LIST ||
                    GetUInt32(extensionData, OffsetHeaderKey) != extension ||
                    GetUInt32(extensionData, OffsetParent) != block ||
                    GetInt32(extensionData, OffsetSecType) != Constants.ST_FILE ||
                    !ChecksumValid(extensionData))
                {
                    AddIssue(IssueType.InvalidFileExtensionBlock, containerBlock, path,
                        $"File extension block {extension} is invalid, file is truncated");
                    broken = true;
                    break;
                }

                guard.Add(extension);
                used[(int)extension] = true;
                extensionBlocks.Add(extension);
                container = extensionData;
                containerBlock = extension;
            }

            if (broken)
            {
                rewrite = true;
            }

            /* check file size matches data blocks */
            var keep = dataBlocks.Count;
            var newByteSize = byteSize;
            var neededBlocks = (int)((byteSize + (long)dataBlockSize - 1) / dataBlockSize);
            if (useOfs)
            {
                /* ofs data blocks are full except last, so file size is sum of data sizes */
                var full = true;
                long size = 0;
                for (var i = 0; i < ofsDataSizes.Count; i++)
                {
                    if (i < ofsDataSizes.Count - 1 && ofsDataSizes[i] != dataBlockSize)
                    {
                        full = false;
                    }

                    size += ofsDataSizes[i];
                }

                if (!full)
                {
                    AddIssue(IssueType.InvalidDataBlock, block, path,
                        "Data blocks other than last data block are not full, file size is set to sum of data sizes",
                        true);
                }

                if (size != byteSize)
                {
                    newByteSize = (uint)Math.Min(size, uint.MaxValue);
                    AddIssue(IssueType.FileSizeMismatch, block, path,
                        $"File size {byteSize} doesn't match size of data blocks {size}, file size is set to {newByteSize}");
                }
            }
            else if (neededBlocks != dataBlocks.Count)
            {
                /* data blocks are kept and file size is set to cover all data blocks, so no data is lost */
                newByteSize = (uint)Math.Min((long)dataBlocks.Count * dataBlockSize, uint.MaxValue);
                AddIssue(IssueType.FileSizeMismatch, block, path,
                    $"File size {byteSize} needs {neededBlocks} data blocks, but file has {dataBlocks.Count} data blocks, file size is set to {newByteSize}");
            }

            /* check first data and ofs data block chain */
            var firstData = GetUInt32(header, OffsetFirstData);
            var expectedFirstData = keep > 0 ? dataBlocks[0] : 0;
            if (useOfs && firstData != expectedFirstData)
            {
                AddIssue(IssueType.InvalidFirstData, block, path,
                    $"First data is block {firstData}, but first data block is {expectedFirstData}");
                rewrite = true;
            }

            if (useOfs)
            {
                for (var i = 0; i < keep; i++)
                {
                    var data = await ReadBlock(dataBlocks[i]);
                    var expectedNext = i < keep - 1 ? dataBlocks[i + 1] : 0;
                    if (GetUInt32(data, OffsetNextData) == expectedNext)
                    {
                        continue;
                    }

                    AddIssue(IssueType.InvalidDataBlockChain, dataBlocks[i], path,
                        $"Next data for sequence {i + 1} is block {GetUInt32(data, OffsetNextData)}, but should be {expectedNext}");
                    if (Repair)
                    {
                        SetUInt32(data, OffsetNextData, expectedNext);
                        ChangeBlock(dataBlocks[i], data);
                    }
                }
            }

            if (!Repair)
            {
                return;
            }

            if (newByteSize != byteSize)
            {
                SetUInt32(header, OffsetByteSize, newByteSize);
                ChangeBlock(block, header);
            }

            if (rewrite)
            {
                await RewriteFileBlockList(block, header, dataBlocks, extensionBlocks);
            }
        }

        /// <summary>
        /// Rewrite data block pointers in file header and file extension blocks. Data block pointers are written
        /// in canonical layout, where each block is filled before the next file extension block is used. File
        /// extension blocks no longer needed are freed.
        /// </summary>
        private async Task RewriteFileBlockList(uint block, byte[] header, IList<uint> dataBlocks,
            IList<uint> extensionBlocks)
        {
            var containersNeeded = Math.Max(1, (dataBlocks.Count + tableSize - 1) / tableSize);
            var containers = new List<uint> { block };
            containers.AddRange(extensionBlocks.Take(containersNeeded - 1));

            /* free file extension blocks no longer needed */
            foreach (var extension in extensionBlocks.Skip(containersNeeded - 1))
            {
                used[(int)extension] = false;
            }

            for (var c = 0; c < containers.Count; c++)
            {
                var containerBlock = containers[c];
                var data = c == 0 ? header : await ReadBlock(containerBlock);
                var start = c * tableSize;
                var count = Math.Max(0, Math.Min(tableSize, dataBlocks.Count - start));

                for (var i = 0; i < tableSize; i++)
                {
                    SetUInt32(data, DataPointerOffset(i), i < count ? dataBlocks[start + i] : 0);
                }

                SetUInt32(data, OffsetHighSeq, (uint)count);
                SetUInt32(data, OffsetExtension, c < containers.Count - 1 ? containers[c + 1] : 0);
                if (c == 0)
                {
                    SetUInt32(data, OffsetFirstData, dataBlocks.Count > 0 ? dataBlocks[0] : 0);
                }

                ChangeBlock(containerBlock, data);
            }

            if (useOfs && dataBlocks.Count > 0)
            {
                var last = dataBlocks[dataBlocks.Count - 1];
                var data = await ReadBlock(last);
                if (GetUInt32(data, OffsetNextData) != 0)
                {
                    SetUInt32(data, OffsetNextData, 0);
                    ChangeBlock(last, data);
                }
            }
        }

        /// <summary>
        /// Check hard link. Hard link pointing outside volume or to a block that is not a valid entry block of
        /// matching type is removed. Returns false, if hard link is removed.
        /// </summary>
        private async Task<bool> CheckHardLink(ScanDirectory directory, uint block, byte[] data, string path)
        {
            var secType = GetInt32(data, OffsetSecType);
            var realEntry = GetUInt32(data, OffsetRealEntry);
            var expectedSecType = secType == Constants.ST_LFILE ? Constants.ST_FILE : Constants.ST_DIR;

            var real = InRange(realEntry) ? await ReadBlock(realEntry) : null;
            if (real == null || !IsValidEntryBlock(realEntry, real) ||
                GetInt32(real, OffsetSecType) != expectedSecType)
            {
                AddIssue(IssueType.DanglingLink, block, path,
                    $"Hard link points to block {realEntry}, which is not a valid {(expectedSecType == Constants.ST_FILE ? "file" : "directory")}, hard link is removed");
                return !Repair;
            }

            stats.HardLinks++;
            links.Add(new ScanLink
            {
                Block = block,
                Directory = directory.Block,
                RealEntry = realEntry,
                Path = path
            });
            return true;
        }

        /// <summary>
        /// Check hard links, when directory tree is scanned. Hard links to entries not found in directory tree are
        /// removed, since the blocks used by the entry are not marked used. Hard links not in the link chain of the
        /// entry are added to the link chain.
        /// </summary>
        private async Task CheckLinks()
        {
            foreach (var link in links)
            {
                if (!entryBlocks.Contains(link.RealEntry))
                {
                    AddIssue(IssueType.DanglingLink, link.Block, link.Path,
                        $"Hard link points to block {link.RealEntry}, which is not in directory tree, hard link is removed");
                    if (Repair)
                    {
                        await UnlinkEntry(link.Directory, link.Block);
                        entryBlocks.Remove(link.Block);
                        used[(int)link.Block] = false;
                        if (directoryEntries.TryGetValue(link.Directory, out var entries))
                        {
                            entries.Remove(link.Block);
                        }
                    }

                    continue;
                }

                /* walk link chain of real entry */
                var real = await ReadBlock(link.RealEntry);
                var next = GetUInt32(real, OffsetNextLink);
                var guard = new HashSet<uint>();
                var found = false;
                while (next != 0 && InRange(next) && guard.Add(next))
                {
                    if (next == link.Block)
                    {
                        found = true;
                        break;
                    }

                    var linkData = await ReadBlock(next);
                    if (!IsValidEntryBlock(next, linkData))
                    {
                        break;
                    }

                    next = GetUInt32(linkData, OffsetNextLink);
                }

                if (found)
                {
                    continue;
                }

                AddIssue(IssueType.LinkNotInChain, link.Block, link.Path,
                    $"Hard link is not in link chain of block {link.RealEntry}, hard link is added to link chain");
                if (Repair)
                {
                    var linkBlock = await ReadBlock(link.Block);
                    SetUInt32(linkBlock, OffsetNextLink, GetUInt32(real, OffsetNextLink));
                    ChangeBlock(link.Block, linkBlock);
                    SetUInt32(real, OffsetNextLink, link.Block);
                    ChangeBlock(link.RealEntry, real);
                }
            }
        }
    }
}
