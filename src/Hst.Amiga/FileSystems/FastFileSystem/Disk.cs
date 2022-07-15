namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;

    public static class Disk
    {
        private static async Task<byte[]> ReadBlockBytes(Volume volume, int sector)
        {
            var blockOffset = volume.PartitionStartOffset + sector * volume.BlockSize;
            volume.Stream.Seek(blockOffset, SeekOrigin.Begin);
            return await Amiga.Disk.ReadBlock(volume.Stream, volume.BlockSize);
        }

        private static async Task WriteBlockBytes(Volume volume, int sector, byte[] blockBytes)
        {
            var blockOffset = volume.PartitionStartOffset + sector * volume.BlockSize;
            volume.Stream.Seek(blockOffset, SeekOrigin.Begin);
            await Amiga.Disk.WriteBlock(volume.Stream, blockBytes);
        }
        
        public static async Task<RootBlock> ReadRootBlock(Volume volume, uint sector)
        {
            var blockBytes = await ReadBlockBytes(volume, (int)sector);
            return RootBlockParser.Parse(blockBytes);
        }
        
        public static async Task WriteRootBlock(Volume volume, int nSect, RootBlock root)
        {
            var blockBytes = RootBlockBuilder.Build(root, volume.BlockSize);
            await WriteBlock(volume, nSect, blockBytes);
        }
        
        public static async Task<BitmapBlock> ReadBitmapBlock(Volume volume, uint sector)
        {
            var blockBytes = await ReadBlockBytes(volume, (int)sector);
            return BitmapBlockParser.Parse(blockBytes);
        }
        
        public static async Task WriteBitmapBlock(Volume vol, int nSect, BitmapBlock bitmapBlock)
        {
            var blockBytes = BitmapBlockBuilder.Build(bitmapBlock, vol.BlockSize);
            await WriteBlock(vol, nSect, blockBytes);
        }

        public static async Task<BitmapExtensionBlock> ReadBitmapExtensionBlock(Volume volume, uint sector)
        {
            var blockBytes = await ReadBlockBytes(volume, (int)sector);
            return BitmapExtensionBlockParser.Parse(blockBytes);
        }

        public static async Task<EntryBlock> ReadEntryBlock(Volume volume, int sector)
        {
            var blockBytes = await ReadBlockBytes(volume, sector);
            return EntryBlockParser.Parse(blockBytes);
        }
        
        public static async Task<DataBlock> ReadDataBlock(Volume vol, int nSect)
        {
            var blockBytes = await ReadBlock(vol, nSect);

            if (Macro.isOFS(vol.DosType))
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
        
        public static async Task WriteDataBlock(Volume volume, int nSect, DataBlock dataBlock)
        {
            var blockBytes = Macro.isOFS(volume.DosType)
                ? DataBlockBuilder.Build(dataBlock, volume.BlockSize)
                : dataBlock.Data;
            await WriteBlock(volume,nSect,blockBytes);
        }        

        public static async Task<FileExtBlock> ReadFileExtBlock(Volume volume, int nSect)
        {
            var blockBytes = await ReadBlockBytes(volume, nSect);
            var fileExtBlock = FileExtBlockParser.Parse(blockBytes);

            if (fileExtBlock.HeaderKey != nSect)
            {
                throw new IOException("Header key not equal to sector");
            }

            if (fileExtBlock.HighSeq < 0 || fileExtBlock.HighSeq > Constants.MAX_DATABLK)
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

        public static async Task WriteFileHdrBlock(Volume vol, int nSect, FileHeaderBlock fileHeaderBlock)
        {
            var blockBytes = EntryBlockBuilder.Build(fileHeaderBlock, vol.BlockSize);
            await WriteBlock(vol, nSect, blockBytes);
        }
        
        public static async Task WriteFileExtBlock(Volume vol, int nSect, FileExtBlock fileExtBlock)
        {
            var blockBytes = FileExtBlockBuilder.Build(fileExtBlock, vol.BlockSize);
            await WriteBlock(vol, nSect, blockBytes);
        }
        
        /// <summary>
        /// Is sector number valid
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="logicalSector"></param>
        /// <returns>True, if logical sector number is within volume first and last block. Otherwise false.</returns>
        public static bool IsSectorNumberValid(Volume volume, int logicalSector)
        {
            return 0 <= logicalSector && logicalSector <= volume.LastBlock - volume.FirstBlock;
        }

        public static void ThrowExceptionIfSectorNumberInvalid(Volume volume, int logicalSector)
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
        public static async Task<byte[]> ReadBlock(Volume volume, int logicalSector)
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

            return await ReadBlockBytes(volume, (int)physicalSector);
        }
        
        public static async Task WriteBlock(Volume volume, int logicalSector, byte[] blockBytes)
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
            
            await WriteBlockBytes(volume, (int)physicalSector, blockBytes);
        }        
    }
}