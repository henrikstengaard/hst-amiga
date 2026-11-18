namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;

    /// <summary>
    /// Reads and writes blocks of a volume through its block device and write-back metadata block cache.
    /// </summary>
    internal class BlockStore
    {
        private readonly Volume volume;

        public BlockStore(Volume volume)
        {
            this.volume = volume;
        }

        private async Task<byte[]> ReadBlockBytes(uint sector)
        {
            var blockOffset = volume.PartitionStartOffset + (long)sector * volume.FileSystemBlockSize;
            var blockBytes = new byte[volume.FileSystemBlockSize];
            await volume.Device.ReadExactlyAsync(blockOffset, blockBytes, 0, blockBytes.Length);
            return blockBytes;
        }

        private async Task WriteBlockBytes(uint sector, byte[] blockBytes)
        {
            var blockOffset = volume.PartitionStartOffset + (long)sector * volume.FileSystemBlockSize;
            await volume.Device.WriteAsync(blockOffset, blockBytes, 0, blockBytes.Length);
        }
        
        public async Task<RootBlock> ReadRootBlock(uint sector)
        {
            var blockBytes = await ReadBlock(sector);
            return RootBlockParser.Parse(blockBytes);
        }
        
        public async Task WriteRootBlock(uint nSect, RootBlock root)
        {
            var blockBytes = RootBlockBuilder.Build(root, volume.FileSystemBlockSize);
            await WriteBlock(nSect, blockBytes);
        }
        
        public async Task<BitmapBlock> ReadBitmapBlock(uint sector)
        {
            var blockBytes = await ReadBlock(sector);
            return BitmapBlockParser.Parse(blockBytes);
        }
        
        public async Task WriteBitmapBlock(uint nSect, BitmapBlock bitmapBlock)
        {
            var blockBytes = BitmapBlockBuilder.Build(bitmapBlock, volume.FileSystemBlockSize);
            await WriteBlock(nSect, blockBytes);
        }

        public async Task<BitmapExtensionBlock> ReadBitmapExtensionBlock(uint sector)
        {
            var blockBytes = await ReadBlock(sector);
            return BitmapExtensionBlockParser.Parse(blockBytes);
        }

        public async Task<EntryBlock> ReadEntryBlock(uint sector)
        {
            var blockBytes = await ReadBlock(sector);
            var entryBlock = EntryBlockParser.Parse(blockBytes, volume.UseLnfs);

            if (volume.UseLnfs && entryBlock.CommentBlock != 0)
            {
                var commentBlockBytes = await ReadBlock(entryBlock.CommentBlock);
                var commentBlock = LongNameFileSystemCommentBlockReader.Parse(commentBlockBytes);
                entryBlock.Comment = commentBlock.Comment;
            }

            return entryBlock;
        }
        
        public async Task<DataBlock> ReadDataBlock(uint nSect)
        {
            var blockBytes = await ReadBlock(nSect, false);

            if (volume.UseOfs)
            {
                var dBlock = DataBlockParser.Parse(blockBytes);
                if (!IsSectorNumberValid(dBlock.HeaderKey))
                    throw new IOException("headerKey out of range");
                if (!IsSectorNumberValid(dBlock.NextData))
                    throw new IOException("nextData out of range");

                return dBlock;
            }

            return new DataBlock
            {
                BlockBytes = blockBytes,
                Data = blockBytes
            };
        }
        
        public async Task WriteDataBlock(uint nSect, DataBlock dataBlock)
        {
            var blockBytes = volume.UseOfs
                ? DataBlockBuilder.Build(dataBlock, volume.FileSystemBlockSize)
                : dataBlock.Data;
            await WriteBlock(nSect, blockBytes, false);
        }        

        public async Task<FileExtBlock> ReadFileExtBlock(uint nSect)
        {
            var blockBytes = await ReadBlock(nSect);
            var fileExtBlock = FileExtBlockParser.Parse(blockBytes);

            if (fileExtBlock.HeaderKey != nSect)
            {
                throw new IOException("Header key not equal to sector");
            }

            if (fileExtBlock.HighSeq > volume.IndexSize)
            {
                throw new IOException("High seq out of range");
            }
            
            if (!IsSectorNumberValid(fileExtBlock.Parent))
            {
                throw new IOException("Parent out of range");
            }

            if (fileExtBlock.Extension != 0 && !IsSectorNumberValid(fileExtBlock.Extension))
            {
                throw new IOException("Extension out of range");
            }
            
            return fileExtBlock;
        }

        public async Task WriteEntryBlock(uint nSect, EntryBlock entryBlock)
        {
            var blockBytes = EntryBlockBuilder.Build(entryBlock, volume.FileSystemBlockSize, volume.UseLnfs);
            await WriteBlock(nSect, blockBytes);
        }
        
        public async Task WriteFileExtBlock(uint nSect, FileExtBlock fileExtBlock)
        {
            var blockBytes = FileExtBlockBuilder.Build(fileExtBlock, volume.FileSystemBlockSize);
            await WriteBlock(nSect, blockBytes);
        }
        
        public async Task<DirCacheBlock> ReadDirCacheBlock(uint nSect)
        {
            var blockBytes = await ReadBlock(nSect);

            var dirCacheBlock = DirCacheBlockParser.Parse(blockBytes);
            if (dirCacheBlock.HeaderKey != nSect)
            {
                throw new IOException($"Invalid dir cache block header key '{dirCacheBlock.HeaderKey}' is not equal to sector {nSect}");
            }

            return dirCacheBlock;
        }

        public async Task WriteDirCacheBlock(uint nSect, DirCacheBlock dirCacheBlock)
        {
            dirCacheBlock.HeaderKey = nSect;

            var blockBytes = DirCacheBlockBuilder.Build(dirCacheBlock, volume.FileSystemBlockSize);
            await WriteBlock(nSect, blockBytes);
        }
        
        /// <summary>
        /// Is sector number valid
        /// </summary>
        /// <param name="logicalSector"></param>
        /// <returns>True, if logical sector number is within volume first and last block. Otherwise false.</returns>
        public bool IsSectorNumberValid(uint logicalSector)
        {
            return logicalSector <= volume.LastBlock - volume.FirstBlock;
        }

        public void ThrowExceptionIfSectorNumberInvalid(uint logicalSector)
        {
            if (IsSectorNumberValid(logicalSector))
            {
                return;
            }
            
            throw new IOException($"Logical sector '{logicalSector}' is out of range");
        }
        
        /// <summary>
        /// Read block
        /// </summary>
        /// <param name="logicalSector">Logical block number</param>
        /// <param name="cache">Read block from and add block to metadata block cache</param>
        /// <returns></returns>
        /// <exception cref="IOException"></exception>
        public async Task<byte[]> ReadBlock(uint logicalSector, bool cache = true)
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

            var blockBytes = await ReadBlockBytes(physicalSector);

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
        /// <param name="sectors">Logical sectors to prefetch</param>
        public async Task PrefetchBlocks(IEnumerable<uint> sectors)
        {
            var sectorsToRead = new List<uint>();
            foreach (var sector in sectors)
            {
                if (IsSectorNumberValid(sector) && !volume.BlockCache.Contains(sector))
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

                await ReadBlocks(first, blocks, buffer, 0);

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
        /// <param name="logicalSector">First logical block number</param>
        /// <param name="blocks">Number of blocks to read</param>
        /// <param name="buffer">Buffer to read blocks into</param>
        /// <param name="offset">Offset in buffer</param>
        /// <exception cref="IOException"></exception>
        public async Task ReadBlocks(uint logicalSector, int blocks, byte[] buffer, int offset)
        {
            if (!volume.Mounted)
            {
                throw new IOException("Volume is not mounted");
            }

            ThrowExceptionIfBlockRangeInvalid(logicalSector, blocks, buffer, offset);

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
        /// <param name="logicalSector">First logical block number</param>
        /// <param name="blocks">Number of blocks to write</param>
        /// <param name="buffer">Buffer to write blocks from</param>
        /// <param name="offset">Offset in buffer</param>
        /// <exception cref="IOException"></exception>
        public async Task WriteBlocks(uint logicalSector, int blocks, byte[] buffer, int offset)
        {
            if (!volume.Mounted)
            {
                throw new IOException("Volume is not mounted");
            }

            if (volume.ReadOnly)
            {
                throw new IOException("Volume is mounted read only");
            }

            ThrowExceptionIfBlockRangeInvalid(logicalSector, blocks, buffer, offset);

            var physicalSector = logicalSector + volume.FirstBlock;
            volume.BlockCache.Invalidate(logicalSector, blocks);
            await volume.Device.WriteAsync(
                volume.PartitionStartOffset + (long)physicalSector * volume.FileSystemBlockSize, buffer, offset,
                blocks * volume.FileSystemBlockSize);
        }

        private void ThrowExceptionIfBlockRangeInvalid(uint logicalSector, int blocks,
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
        /// <param name="logicalSector">Logical block number</param>
        /// <param name="blockBytes">Block bytes to write</param>
        /// <param name="cache">Add block to metadata block cache as dirty block, which is written when volume is
        /// flushed. Otherwise block is removed from cache and written to disk</param>
        public async Task WriteBlock(uint logicalSector, byte[] blockBytes, bool cache = true)
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
            await WriteBlockBytes(physicalSector, blockBytes);
        }

        /// <summary>
        /// Flush volume, if number of dirty cached blocks exceeds max dirty blocks. Must only be called, when
        /// a file system operation is completed, so a consistent state is written to disk.
        /// </summary>
        public async Task CheckpointIfNeeded()
        {
            if (volume.BlockCache.DirtyCount < Volume.MaxDirtyBlocks)
            {
                return;
            }

            await FlushVolume();
        }

        /// <summary>
        /// Flush volume by writing changed bitmap blocks, root block with bitmap flags valid and all dirty cached
        /// blocks in sector order with consecutive dirty blocks merged into single writes. Root block is written
        /// last.
        /// </summary>
        public async Task FlushVolume()
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
                    await volume.Allocator.UpdateBitmap();
                }

                await WriteDirtyBlocks();
                await volume.Device.FlushAsync();
            }
            finally
            {
                volume.Flushing = false;
            }
        }

        private async Task WriteDirtyBlocks()
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
                    await WriteBlockBytes(dirtyBlocks[i].Sector + volume.FirstBlock, dirtyBlocks[i].Bytes);
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
                await WriteBlockBytes(rootBlock.Sector + volume.FirstBlock, rootBlock.Bytes);
                volume.BlockCache.MarkClean(rootBlock);
            }

            volume.BlockCache.EvictCleanBlocks();
        }

        /// <summary>
        /// Write root block bitmap flags invalid directly to root block on disk, so volume is validated, if
        /// changes are not flushed. Root block on disk is used, so changes to root block not flushed yet are
        /// not written. Cached root block is updated with bitmap flags invalid.
        /// </summary>
        public async Task WriteRootBlockBitmapFlagsInvalid()
        {
            var rootSector = volume.RootBlockOffset + volume.FirstBlock;
            var diskRoot = RootBlockParser.Parse(await ReadBlockBytes(rootSector));
            diskRoot.BitmapFlags = Constants.BM_INVALID;
            var diskRootBytes = RootBlockBuilder.Build(diskRoot, volume.FileSystemBlockSize);
            await WriteBlockBytes(rootSector, diskRootBytes);

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