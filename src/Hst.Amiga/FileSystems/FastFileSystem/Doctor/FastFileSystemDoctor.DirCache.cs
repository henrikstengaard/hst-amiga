namespace Hst.Amiga.FileSystems.FastFileSystem.Doctor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;

    /// <summary>
    /// Check of directory cache blocks for DOS\4 and DOS\5. Directory cache blocks only contain copies of
    /// information in entry blocks, so invalid directory cache blocks are rebuilt.
    /// </summary>
    public partial class FastFileSystemDoctor
    {
        /// <summary>
        /// Directory cache record read from directory cache block.
        /// </summary>
        private class DirCacheRecord
        {
            public uint Header;
            public uint Size;
            public uint Protect;
            public int Type;
            public string Name;
        }

        /// <summary>
        /// Directories with dir cache to rebuild and valid dir cache blocks, which can be reused.
        /// </summary>
        private Dictionary<uint, List<uint>> dirCachesToRebuild;

        private int DirCacheRecordsSize => volume.BlockSize - OffsetTable;

        /// <summary>
        /// Check dir cache blocks of all directories found in directory tree.
        /// </summary>
        private async Task CheckDirCaches()
        {
            dirCachesToRebuild = new Dictionary<uint, List<uint>>();

            foreach (var entry in directoryEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await CheckDirCache(entry.Key, entry.Value);
            }
        }

        private async Task CheckDirCache(uint directory, IList<uint> entries)
        {
            var path = directoryPaths.TryGetValue(directory, out var p) ? p : string.Empty;
            var dir = await ReadBlock(directory);
            var dirCacheBlock = GetUInt32(dir, OffsetExtension);
            var chain = new List<uint>();
            var records = new List<DirCacheRecord>();
            string reason = null;

            if (dirCacheBlock == 0)
            {
                reason = "Directory has no dir cache block";
            }

            while (dirCacheBlock != 0)
            {
                var data = InRange(dirCacheBlock) && !used[(int)dirCacheBlock] && !chain.Contains(dirCacheBlock)
                    ? await ReadBlock(dirCacheBlock)
                    : null;
                if (data == null ||
                    GetInt32(data, OffsetType) != Constants.T_DIRC ||
                    GetUInt32(data, OffsetHeaderKey) != dirCacheBlock ||
                    GetUInt32(data, 0x8) != directory ||
                    !ChecksumValid(data) ||
                    !ReadDirCacheRecords(data, records))
                {
                    reason = $"Dir cache block {dirCacheBlock} is invalid";
                    break;
                }

                chain.Add(dirCacheBlock);
                dirCacheBlock = GetUInt32(data, 0x10);
            }

            if (reason == null)
            {
                reason = await CompareDirCacheRecords(records, entries);
            }

            if (reason == null)
            {
                foreach (var block in chain)
                {
                    used[(int)block] = true;
                }

                return;
            }

            AddIssue(IssueType.DirCacheInvalid, directory, path, $"{reason}, dir cache is rebuilt");
            if (Repair)
            {
                /* valid dir cache blocks of directory are reused */
                foreach (var block in chain)
                {
                    used[(int)block] = true;
                }

                dirCachesToRebuild[directory] = chain;
            }
        }

        /// <summary>
        /// Read records from dir cache block. Returns false, if records are outside dir cache block.
        /// </summary>
        private bool ReadDirCacheRecords(byte[] data, IList<DirCacheRecord> records)
        {
            var count = GetUInt32(data, OffsetDataSize);
            var ptr = OffsetTable;
            var end = OffsetTable + DirCacheRecordsSize;

            for (var i = 0; i < count; i++)
            {
                if (ptr + 25 > end)
                {
                    return false;
                }

                var nameLength = data[ptr + 23];
                if (ptr + 25 + nameLength > end)
                {
                    return false;
                }

                var commentLength = data[ptr + 24 + nameLength];
                var length = 25 + nameLength + commentLength;
                if (ptr + length > end)
                {
                    return false;
                }

                records.Add(new DirCacheRecord
                {
                    Header = GetUInt32(data, ptr),
                    Size = GetUInt32(data, ptr + 4),
                    Protect = GetUInt32(data, ptr + 8),
                    Type = unchecked((sbyte)data[ptr + 22]),
                    Name = AmigaTextHelper.GetString(data, ptr + 24, nameLength)
                });

                ptr += length % 2 == 0 ? length : length + 1;
            }

            return true;
        }

        /// <summary>
        /// Compare dir cache records with entries in directory. Returns reason, if records don't match entries.
        /// </summary>
        private async Task<string> CompareDirCacheRecords(IList<DirCacheRecord> records, IList<uint> entries)
        {
            if (records.Count != entries.Count)
            {
                return $"Dir cache has {records.Count} records, but directory has {entries.Count} entries";
            }

            var recordsByHeader = new Dictionary<uint, DirCacheRecord>();
            foreach (var record in records)
            {
                if (recordsByHeader.ContainsKey(record.Header))
                {
                    return $"Dir cache has more than one record for entry block {record.Header}";
                }

                recordsByHeader[record.Header] = record;
            }

            foreach (var entry in entries)
            {
                if (!recordsByHeader.TryGetValue(entry, out var record))
                {
                    return $"Dir cache has no record for entry block {entry}";
                }

                var expected = CreateCacheEntry(await ReadBlock(entry), entry);
                if (record.Name != expected.Name || record.Type != expected.Type ||
                    record.Size != expected.Size || record.Protect != expected.Protect)
                {
                    return $"Dir cache record for entry block {entry} doesn't match entry";
                }
            }

            return null;
        }

        private CacheEntry CreateCacheEntry(byte[] data, uint block)
        {
            var secType = GetInt32(data, OffsetSecType);
            return new CacheEntry
            {
                Header = block,
                Size = secType == Constants.ST_FILE ? GetUInt32(data, OffsetByteSize) : 0,
                Protect = GetUInt32(data, OffsetAccess),
                Date = ReadDate(data, OffsetDate),
                Type = secType,
                Name = ReadName(data),
                Comment = ReadComment(data)
            };
        }

        /// <summary>
        /// Rebuild dir caches of directories with invalid dir caches. Valid dir cache blocks are reused and new
        /// blocks are allocated, if needed.
        /// </summary>
        private async Task RebuildDirCaches()
        {
            if (dirCachesToRebuild == null)
            {
                return;
            }

            foreach (var rebuild in dirCachesToRebuild)
            {
                await RebuildDirCache(rebuild.Key, rebuild.Value);
            }
        }

        private async Task RebuildDirCache(uint directory, IList<uint> reuse)
        {
            var entries = directoryEntries.TryGetValue(directory, out var e) ? e : new List<uint>();

            /* pack cache entries in dir cache blocks */
            var blocks = new List<DirCacheBlock>();
            var current = new DirCacheBlock(volume.BlockSize);
            blocks.Add(current);
            var ptr = 0;
            foreach (var entry in entries)
            {
                var cacheEntry = CreateCacheEntry(await ReadBlock(entry), entry);
                if (ptr + cacheEntry.EntryLen > current.Records.Length)
                {
                    current = new DirCacheBlock(volume.BlockSize);
                    blocks.Add(current);
                    ptr = 0;
                }

                ptr += Cache.PutCacheEntry(current, ref ptr, cacheEntry);
                current.RecordsNb++;
            }

            /* reuse valid dir cache blocks and allocate new blocks */
            var blockNumbers = new List<uint>();
            for (var i = 0; i < blocks.Count; i++)
            {
                if (i < reuse.Count)
                {
                    blockNumbers.Add(reuse[i]);
                    continue;
                }

                var allocated = AllocateBlock();
                if (allocated == 0)
                {
                    AddIssue(IssueType.DiskFull, directory, directoryPaths.TryGetValue(directory, out var p) ? p : null,
                        "No free blocks to rebuild dir cache", fixable: false);
                    return;
                }

                blockNumbers.Add(allocated);
            }

            foreach (var block in reuse.Skip(blocks.Count))
            {
                used[(int)block] = false;
            }

            for (var i = 0; i < blocks.Count; i++)
            {
                var dirCacheBlock = blocks[i];
                dirCacheBlock.HeaderKey = blockNumbers[i];
                dirCacheBlock.Parent = directory;
                dirCacheBlock.NextDirC = i < blocks.Count - 1 ? blockNumbers[i + 1] : 0;
                var data = DirCacheBlockBuilder.Build(dirCacheBlock, volume.BlockSize);
                ChangeBlock(blockNumbers[i], data);
            }

            var dir = await ReadBlock(directory);
            SetUInt32(dir, OffsetExtension, blockNumbers[0]);
            ChangeBlock(directory, dir);
        }
    }
}
