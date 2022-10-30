namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;

    public static class Disk
    {
        private static async Task<byte[]> ReadBlockBytes(Volume volume, uint sector)
        {
            var blockOffset = volume.PartitionStartOffset + sector * volume.FileSystemBlockSize;
            volume.Stream.Seek(blockOffset, SeekOrigin.Begin);
            return await Amiga.Disk.ReadBlock(volume.Stream, volume.FileSystemBlockSize);
        }

        private static async Task WriteBlockBytes(Volume volume, uint sector, byte[] blockBytes)
        {
            var blockOffset = volume.PartitionStartOffset + sector * volume.FileSystemBlockSize;
            volume.Stream.Seek(blockOffset, SeekOrigin.Begin);
            await Amiga.Disk.WriteBlock(volume.Stream, blockBytes);
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
            var blockBytes = await ReadBlock(vol, nSect);

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
            await WriteBlock(volume, nSect, blockBytes);
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

        public static async Task WriteFileHdrBlock(Volume vol, uint nSect, FileHeaderBlock fileHeaderBlock)
        {
            var blockBytes = EntryBlockBuilder.Build(fileHeaderBlock, vol.FileSystemBlockSize, vol.UseLnfs);
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
        /// <returns></returns>
        /// <exception cref="IOException"></exception>
        public static async Task<byte[]> ReadBlock(Volume volume, uint logicalSector)
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

            return await ReadBlockBytes(volume, physicalSector);
        }
        
        public static async Task WriteBlock(Volume volume, uint logicalSector, byte[] blockBytes)
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
            
            await WriteBlockBytes(volume, physicalSector, blockBytes);
        }        
    }
}