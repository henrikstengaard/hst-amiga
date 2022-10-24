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
            
            var hashtableSize = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc); // hashtable

            if (hashtableSize != Constants.HT_SIZE)
            {
                throw new IOException($"Invalid root block hashtable size '{hashtableSize}'");
            }
            
            var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x14);
            
            var index = new List<uint>();
            for (var i = 0; i < Constants.HT_SIZE; i++)
            {
                index.Add(BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x18 + (i * SizeOf.Long)));
            }

            var bitmapFlags = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x138); // bm_flag

            var bitmapBlockOffsets = new List<uint>();

            for (var i = 0; i < 25; i++)
            {
                var bitmapBlockOffset =
                    BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x13c + (i * SizeOf.Long));
                bitmapBlockOffsets.Add(bitmapBlockOffset);
            }

            var bitmapExtensionBlocksOffset = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1a0);

            var date = DateHelper.ReadDate(blockBytes, 0x1a4);

            var diskName = blockBytes.ReadStringWithLength(0x1b0, Constants.MAXNAMELEN);

            var diskAlterationDate = DateHelper.ReadDate(blockBytes, 0x1d8);
            var fileSystemCreationDate = DateHelper.ReadDate(blockBytes, 0x1e4);

            var extension = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1f8);
            var secType = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x1fc);

            if (secType != Constants.ST_ROOT)
            {
                throw new IOException($"Invalid root block sec type '{type}'");
            }
            
            return new RootBlock
            {
                BlockBytes = blockBytes,
                HighSeq = 0,
                HashTableSize = Constants.INDEX_SIZE,
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