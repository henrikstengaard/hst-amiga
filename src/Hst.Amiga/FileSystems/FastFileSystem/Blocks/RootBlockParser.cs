namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System.Collections.Generic;
    using System.IO;
    using Hst.Amiga.Extensions;
    using Core.Converters;

    public static class RootBlockParser
    {
        public static RootBlock Parse(byte[] blockBytes)
        {
            var type = BigEndianConverter.ConvertBytesToInt32(blockBytes);

            if (type != Constants.T_HEADER)
            {
                throw new IOException($"Invalid root block type '{type}'");
            }
            
            var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x14);
            
            var indexSize = FastFileSystemHelper.CalculateHashtableSize((uint)blockBytes.Length);
            var index = new List<uint>();
            for (var i = 0; i < indexSize; i++)
            {
                index.Add(BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x18 + (i * SizeOf.Long)));
            }

            var bitmapFlags = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0xc8); // bm_flag
            
            var bitmapBlockOffsets = new List<uint>();
            for (var i = 0; i < 25; i++)
            {
                var bitmapBlockOffset =
                    BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0xc4 + (SizeOf.ULong * i));
                bitmapBlockOffsets.Add(bitmapBlockOffset);
            }

            var bitmapExtensionBlocksOffset = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x60);

            var date = DateHelper.ReadDate(blockBytes, blockBytes.Length - 0x5c);

            var diskName = blockBytes.ReadStringWithLength(blockBytes.Length - 0x50, Constants.MAXNAMELEN);

            var diskAlterationDate = DateHelper.ReadDate(blockBytes, blockBytes.Length - 0x28);
            var fileSystemCreationDate = DateHelper.ReadDate(blockBytes, blockBytes.Length - 0x1c);

            var extension = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x08);
            var secType = BigEndianConverter.ConvertBytesToInt32(blockBytes, blockBytes.Length - 0x04);

            if (secType != Constants.ST_ROOT)
            {
                throw new IOException($"Invalid root block sec type '{type}'");
            }
            
            return new RootBlock(blockBytes.Length)
            {
                BlockBytes = blockBytes,
                HighSeq = 0,
                HashTableSize = indexSize,
                HashTable = index.ToArray(),
                FirstData = 0,
                Checksum = checksum,
                BitmapFlags = bitmapFlags,
                BitmapBlocksOffset = bitmapBlockOffsets[0],
                BitmapBlockOffsets = bitmapBlockOffsets.ToArray(),
                BitmapExtensionBlocksOffset = bitmapExtensionBlocksOffset,
                Date = date,
                Name = diskName,
                DiskAlterationDate = diskAlterationDate,
                FileSystemCreationDate = fileSystemCreationDate,
                NextSameHash = 0,
                Parent = 0,
                Extension = extension
            };
        }
    }
}