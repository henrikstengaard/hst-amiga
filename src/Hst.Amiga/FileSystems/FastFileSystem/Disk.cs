namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;

    internal static class Disk
    {
        private static async Task<byte[]> ReadBlockBytes(Volume volume, uint sector)
        {
            var blockOffset = volume.PartitionStartOffset + (long)sector * volume.FileSystemBlockSize;
            var blockBytes = new byte[volume.FileSystemBlockSize];
            await volume.Device.ReadExactlyAsync(blockOffset, blockBytes, 0, blockBytes.Length);
            return blockBytes;
        }

        private static async Task WriteBlockBytes(Volume volume, uint sector, byte[] blockBytes)
        {
            var blockOffset = volume.PartitionStartOffset + (long)sector * volume.FileSystemBlockSize;
            await volume.Device.WriteAsync(blockOffset, blockBytes, 0, blockBytes.Length);
        }
        
        public static async Task<RootBlock> ReadRootBlock(Volume volume, uint sector)
        {
            var blockBytes = await ReadBlock(volume, sector);
            return RootBlockParser.Parse(blockBytes);
        }
        
        public static async Task WriteRootBlock(Volume volume, uint nSect, RootBlock root)
        {
            var blockBytes = RootBlockBuilder.Build(root, volume.FileSystemBlockSize);
            await WriteBlock(volume, nSect, blockBytes);
        }
        
        public static async Task<BitmapBlock> ReadBitmapBlock(Volume volume, uint sector)
        {
            var blockBytes = await ReadBlock(volume, sector);
            return BitmapBlockParser.Parse(blockBytes);
        }
        
        public static async Task WriteBitmapBlock(Volume vol, uint nSect, BitmapBlock bitmapBlock)
        {
            var blockBytes = BitmapBlockBuilder.Build(bitmapBlock, vol.FileSystemBlockSize);
            await WriteBlock(vol, nSect, blockBytes);
        }

        public static async Task<BitmapExtensionBlock> ReadBitmapExtensionBlock(Volume volume, uint sector)
        {
            var blockBytes = await ReadBlock(volume, sector);
            return BitmapExtensionBlockParser.Parse(blockBytes);
        }

        public static async Task<EntryBlock> ReadEntryBlock(Volume volume, uint sector)
        {
            var blockBytes = await ReadBlock(volume, sector);
            var entryBlock = EntryBlockParser.Parse(blockBytes, volume.UseLnfs);

            if (volume.UseLnfs && entryBlock.CommentBlock != 0)
            {
                var commentBlockBytes = await ReadBlock(volume, entryBlock.CommentBlock);
                var commentBlock = LongNameFileSystemCommentBlockReader.Parse(commentBlockBytes);
                entryBlock.Comment = commentBlock.Comment;
            }

            return entryBlock;
        }
        
        public static async Task<DataBlock> ReadDataBlock(Volume vol, uint nSect)
        {
            var blockBytes = await ReadBlock(vol, nSect, false);

            if (vol.UseOfs)
            {
                var dBlock = DataBlockParser.Parse(blockBytes);
                if (!IsSectorNumberValid(vol, dBlock.HeaderKey))
                    throw new IOException("headerKey out of range");
                if (!IsSectorNumberValid(vol, dBlock.NextData))
                    throw new IOException("nextData out of range");

                return dBlock;
            }

            return new DataBlock
            {
                BlockBytes = blockBytes,
                Data = blockBytes
            };
        }
        
        public static async Task WriteDataBlock(Volume volume, uint nSect, DataBlock dataBlock)
        {
            var blockBytes = volume.UseOfs
                ? DataBlockBuilder.Build(dataBlock, volume.FileSystemBlockSize)
                : dataBlock.Data;
            await WriteBlock(volume, nSect, blockBytes, false);
        }        

        public static async Task<FileExtBlock> ReadFileExtBlock(Volume volume, uint nSect)
        {
            var blockBytes = await ReadBlock(volume, nSect);
            var fileExtBlock = FileExtBlockParser.Parse(blockBytes);

            if (fileExtBlock.HeaderKey != nSect)
            {
                throw new IOException("Header key not equal to sector");
            }

            if (fileExtBlock.HighSeq > volume.IndexSize)
            {
                throw new IOException("High seq out of range");
            }
            
            if (!IsSectorNumberValid(volume, fileExtBlock.Parent))
            {
                throw new IOException("Parent out of range");
            }

            if (fileExtBlock.Extension != 0 && !IsSectorNumberValid(volume, fileExtBlock.Extension))
            {
                throw new IOException("Extension out of range");
            }
            
            return fileExtBlock;
        }

        public static async Task WriteEntryBlock(Volume vol, uint nSect, EntryBlock entryBlock)
        {
            var blockBytes = EntryBlockBuilder.Build(entryBlock, vol.FileSystemBlockSize, vol.UseLnfs);
            await WriteBlock(vol, nSect, blockBytes);
        }
        
        public static async Task WriteFileExtBlock(Volume vol, uint nSect, FileExtBlock fileExtBlock)
        {
            var blockBytes = FileExtBlockBuilder.Build(fileExtBlock, vol.FileSystemBlockSize);
            await WriteBlock(vol, nSect, blockBytes);
        }
        
        public static async Task<DirCacheBlock> ReadDirCacheBlock(Volume vol, uint nSect)
        {
            var blockBytes = await ReadBlock(vol, nSect);

            var dirCacheBlock = DirCacheBlockParser.Parse(blockBytes);
            if (dirCacheBlock.HeaderKey != nSect)
            {
                throw new IOException($"Invalid dir cache block header key '{dirCacheBlock.HeaderKey}' is not equal to sector {nSect}");
            }

            return dirCacheBlock;
        }

        public static async Task WriteDirCacheBlock(Volume vol, uint nSect, DirCacheBlock dirCacheBlock)
        {
            dirCacheBlock.HeaderKey = nSect;

            var blockBytes = DirCacheBlockBuilder.Build(dirCacheBlock, vol.FileSystemBlockSize);
            await WriteBlock(vol, nSect, blockBytes);
        }
        
        /// <summary>
        /// Is sector number valid
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="logicalSector"></param>
        /// <returns>True, if logical sector number is within volume first and last block. Otherwise false.</returns>
        public static bool IsSectorNumberValid(Volume volume, uint logicalSector)
        {
            return logicalSector <= volume.LastBlock - volume.FirstBlock;
        }

        public static void ThrowExceptionIfSectorNumberInvalid(Volume volume, uint logicalSector)
        {
            if (IsSectorNumberValid(volume, logicalSector))
            {
                return;
            }
            
            throw new IOException($"Logical sector '{logicalSector}' is out of range");
        }
        
        /// <summary>
        /// Read block
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="logicalSector">Logical block number</param>
        /// <param name="cache">Read block from and add block to metadata block cache</param>
        /// <returns></returns>
        /// <exception cref="IOException"></exception>
        public static async Task<byte[]> ReadBlock(Volume volume, uint logicalSector, bool cache = true)
        {
            if (!volume.Mounted)
            {
                throw new IOException("Volume is not mounted");
            }
            
            // translate logical sector to physical sector
            var physicalSector = logicalSector + volume.FirstBlock;
            if (physicalSector < volume.FirstBlock || physicalSector > volume.LastBlock)
            {
                throw new IOException($"Logical sector '{logicalSector}' is out of range");
            }

            if (cache && volume.BlockCache.TryGet(logicalSector, out var cachedBlockBytes))
            {
                return cachedBlockBytes;
            }

            // return dirty cached block not written to disk yet, if block is read without cache
            if (!cache && volume.BlockCache.TryGetDirty(logicalSector, out var dirtyBlockBytes))
            {
                var copy = new byte[dirtyBlockBytes.Length];
                Array.Copy(dirtyBlockBytes, copy, copy.Length);
                return copy;
            }

            var blockBytes = await ReadBlockBytes(volume, physicalSector);

            if (cache)
            {
                volume.BlockCache.Set(logicalSector, blockBytes, false);
            }

            return blockBytes;
        }
        
        /// <summary>
        /// Max number of blocks between blocks to prefetch, which are read with a single read
        /// </summary>
        private const int PrefetchMaxGap = 8;

        /// <summary>
        /// Max number of blocks read with a single read, when prefetching blocks
        /// </summary>
        private const int PrefetchMaxBlocks = 128;

        /// <summary>
        /// Prefetch metadata blocks into block cache. Sectors not cached are read sorted by sector, where sectors
        /// close to each other are read with a single read. Only sectors prefetched are added to block cache.
        /// </summary>
        /// <param name="volume">Volume</param>
        /// <param name="sectors">Logical sectors to prefetch</param>
        public static async Task PrefetchBlocks(Volume volume, IEnumerable<uint> sectors)
        {
            var sectorsToRead = new List<uint>();
            foreach (var sector in sectors)
            {
                if (IsSectorNumberValid(volume, sector) && !volume.BlockCache.Contains(sector))
                {
                    sectorsToRead.Add(sector);
                }
            }

            if (sectorsToRead.Count <= 1)
            {
                return;
            }

            sectorsToRead.Sort();

            var blockSize = volume.FileSystemBlockSize;
            byte[] buffer = null;
            var i = 0;
            while (i < sectorsToRead.Count)
            {
                // find run of sectors with max gap between them
                var first = sectorsToRead[i];
                var last = i;
                while (last + 1 < sectorsToRead.Count &&
                       sectorsToRead[last + 1] - sectorsToRead[last] <= PrefetchMaxGap + 1 &&
                       sectorsToRead[last + 1] - first < PrefetchMaxBlocks)
                {
                    last++;
                }

                var blocks = (int)(sectorsToRead[last] - first + 1);
                if (buffer == null || buffer.Length < blocks * blockSize)
                {
                    buffer = new byte[blocks * blockSize];
                }

                await ReadBlocks(volume, first, blocks, buffer, 0);

                var blockBytes = new byte[blockSize];
                for (var j = i; j <= last; j++)
                {
                    if (j > i && sectorsToRead[j] == sectorsToRead[j - 1])
                    {
                        continue;
                    }

                    Array.Copy(buffer, (int)(sectorsToRead[j] - first) * blockSize, blockBytes, 0, blockSize);
                    volume.BlockCache.Set(sectorsToRead[j], blockBytes, false);
                }

                i = last + 1;
            }
        }

        /// <summary>
        /// Read consecutive blocks into buffer with a single read
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="logicalSector">First logical block number</param>
        /// <param name="blocks">Number of blocks to read</param>
        /// <param name="buffer">Buffer to read blocks into</param>
        /// <param name="offset">Offset in buffer</param>
        /// <exception cref="IOException"></exception>
        public static async Task ReadBlocks(Volume volume, uint logicalSector, int blocks, byte[] buffer, int offset)
        {
            if (!volume.Mounted)
            {
                throw new IOException("Volume is not mounted");
            }

            ThrowExceptionIfBlockRangeInvalid(volume, logicalSector, blocks, buffer, offset);

            var physicalSector = logicalSector + volume.FirstBlock;
            await volume.Device.ReadExactlyAsync(
                volume.PartitionStartOffset + (long)physicalSector * volume.FileSystemBlockSize, buffer, offset,
                blocks * volume.FileSystemBlockSize);

            // overlay dirty cached blocks not written to disk yet
            if (volume.BlockCache.DirtyCount == 0)
            {
                return;
            }

            for (var i = 0; i < blocks; i++)
            {
                if (volume.BlockCache.TryGetDirty(logicalSector + (uint)i, out var dirtyBlockBytes))
                {
                    Array.Copy(dirtyBlockBytes, 0, buffer, offset + i * volume.FileSystemBlockSize,
                        volume.FileSystemBlockSize);
                }
            }
        }

        /// <summary>
        /// Write consecutive blocks from buffer with a single write
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="logicalSector">First logical block number</param>
        /// <param name="blocks">Number of blocks to write</param>
        /// <param name="buffer">Buffer to write blocks from</param>
        /// <param name="offset">Offset in buffer</param>
        /// <exception cref="IOException"></exception>
        public static async Task WriteBlocks(Volume volume, uint logicalSector, int blocks, byte[] buffer, int offset)
        {
            if (!volume.Mounted)
            {
                throw new IOException("Volume is not mounted");
            }

            if (volume.ReadOnly)
            {
                throw new IOException("Volume is mounted read only");
            }

            ThrowExceptionIfBlockRangeInvalid(volume, logicalSector, blocks, buffer, offset);

            var physicalSector = logicalSector + volume.FirstBlock;
            volume.BlockCache.Invalidate(logicalSector, blocks);
            await volume.Device.WriteAsync(
                volume.PartitionStartOffset + (long)physicalSector * volume.FileSystemBlockSize, buffer, offset,
                blocks * volume.FileSystemBlockSize);
        }

        private static void ThrowExceptionIfBlockRangeInvalid(Volume volume, uint logicalSector, int blocks,
            byte[] buffer, int offset)
        {
            if (blocks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(blocks), "Blocks must be greater than zero");
            }

            if (offset < 0 || (long)offset + (long)blocks * volume.FileSystemBlockSize > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset),
                    $"{blocks} blocks at offset {offset} exceeds buffer size {buffer.Length}");
            }

            var lastLogicalSector = (long)logicalSector + blocks - 1;
            if (lastLogicalSector > volume.LastBlock - volume.FirstBlock)
            {
                throw new IOException($"Logical sectors '{logicalSector}' to '{lastLogicalSector}' is out of range");
            }
        }

        /// <summary>
        /// Write block
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="logicalSector">Logical block number</param>
        /// <param name="blockBytes">Block bytes to write</param>
        /// <param name="cache">Add block to metadata block cache as dirty block, which is written when volume is
        /// flushed. Otherwise block is removed from cache and written to disk</param>
        public static async Task WriteBlock(Volume volume, uint logicalSector, byte[] blockBytes, bool cache = true)
        {
            if (!volume.Mounted)
            {
                throw new IOException("Volume is not mounted");
            }

            if (volume.ReadOnly)
            {
                throw new IOException("Volume is mounted read only");
            }

            // translate logical sector to physical sector
            var physicalSector = logicalSector + volume.FirstBlock;
            
            if (physicalSector < volume.FirstBlock || physicalSector > volume.LastBlock)
            {
                throw new IOException($"Logical sector '{logicalSector}' is out of range");
            }
            
            if (cache)
            {
                volume.BlockCache.Set(logicalSector, blockBytes, true);
                return;
            }

            volume.BlockCache.Invalidate(logicalSector);
            await WriteBlockBytes(volume, physicalSector, blockBytes);
        }

        /// <summary>
        /// Flush volume, if number of dirty cached blocks exceeds max dirty blocks. Must only be called, when
        /// a file system operation is completed, so a consistent state is written to disk.
        /// </summary>
        public static async Task CheckpointIfNeeded(Volume volume)
        {
            if (volume.BlockCache.DirtyCount < Volume.MaxDirtyBlocks)
            {
                return;
            }

            await FlushVolume(volume);
        }

        /// <summary>
        /// Flush volume by writing changed bitmap blocks, root block with bitmap flags valid and all dirty cached
        /// blocks in sector order with consecutive dirty blocks merged into single writes. Root block is written
        /// last.
        /// </summary>
        public static async Task FlushVolume(Volume volume)
        {
            if (!volume.Mounted || volume.ReadOnly || volume.Flushing)
            {
                return;
            }

            volume.Flushing = true;
            try
            {
                if (volume.BitmapUpdatePending)
                {
                    await Bitmap.AdfUpdateBitmap(volume);
                }

                await WriteDirtyBlocks(volume);
                await volume.Device.FlushAsync();
            }
            finally
            {
                volume.Flushing = false;
            }
        }

        private static async Task WriteDirtyBlocks(Volume volume)
        {
            var dirtyBlocks = volume.BlockCache.GetDirtyBlocks();
            if (dirtyBlocks.Count == 0)
            {
                return;
            }

            var blockSize = volume.FileSystemBlockSize;
            BlockCache.CachedBlock rootBlock = null;
            byte[] runBuffer = null;

            var i = 0;
            while (i < dirtyBlocks.Count)
            {
                if (dirtyBlocks[i].Sector == volume.RootBlockOffset)
                {
                    rootBlock = dirtyBlocks[i];
                    i++;
                    continue;
                }

                // find run of consecutive dirty blocks, root block is not part of runs as it's written last
                var runLength = 1;
                while (i + runLength < dirtyBlocks.Count &&
                       dirtyBlocks[i + runLength].Sector == dirtyBlocks[i].Sector + (uint)runLength &&
                       dirtyBlocks[i + runLength].Sector != volume.RootBlockOffset)
                {
                    runLength++;
                }

                if (runLength == 1)
                {
                    await WriteBlockBytes(volume, dirtyBlocks[i].Sector + volume.FirstBlock, dirtyBlocks[i].Bytes);
                }
                else
                {
                    if (runBuffer == null || runBuffer.Length < runLength * blockSize)
                    {
                        runBuffer = new byte[runLength * blockSize];
                    }

                    for (var j = 0; j < runLength; j++)
                    {
                        Array.Copy(dirtyBlocks[i + j].Bytes, 0, runBuffer, j * blockSize, blockSize);
                    }

                    var physicalSector = dirtyBlocks[i].Sector + volume.FirstBlock;
                    await volume.Device.WriteAsync(volume.PartitionStartOffset + (long)physicalSector * blockSize,
                        runBuffer, 0, runLength * blockSize);
                }

                for (var j = 0; j < runLength; j++)
                {
                    volume.BlockCache.MarkClean(dirtyBlocks[i + j]);
                }

                i += runLength;
            }

            if (rootBlock != null)
            {
                await WriteBlockBytes(volume, rootBlock.Sector + volume.FirstBlock, rootBlock.Bytes);
                volume.BlockCache.MarkClean(rootBlock);
            }

            volume.BlockCache.EvictCleanBlocks();
        }

        /// <summary>
        /// Write root block bitmap flags invalid directly to root block on disk, so volume is validated, if
        /// changes are not flushed. Root block on disk is used, so changes to root block not flushed yet are
        /// not written. Cached root block is updated with bitmap flags invalid.
        /// </summary>
        public static async Task WriteRootBlockBitmapFlagsInvalid(Volume volume)
        {
            var rootSector = volume.RootBlockOffset + volume.FirstBlock;
            var diskRoot = RootBlockParser.Parse(await ReadBlockBytes(volume, rootSector));
            diskRoot.BitmapFlags = Constants.BM_INVALID;
            var diskRootBytes = RootBlockBuilder.Build(diskRoot, volume.FileSystemBlockSize);
            await WriteBlockBytes(volume, rootSector, diskRootBytes);

            if (volume.BlockCache.TryGetDirty(volume.RootBlockOffset, out var dirtyRootBytes))
            {
                // update bitmap flags in dirty cached root block, which is written when volume is flushed
                var cachedRoot = RootBlockParser.Parse(dirtyRootBytes);
                cachedRoot.BitmapFlags = Constants.BM_INVALID;
                volume.BlockCache.Set(volume.RootBlockOffset,
                    RootBlockBuilder.Build(cachedRoot, volume.FileSystemBlockSize), true);
            }
            else
            {
                // cached root block is equal to root block on disk
                volume.BlockCache.Set(volume.RootBlockOffset, diskRootBytes, false);
            }
        }        
    }
}