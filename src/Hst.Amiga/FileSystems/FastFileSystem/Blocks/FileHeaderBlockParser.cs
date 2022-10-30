namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System.Collections.Generic;
    using System.IO;
    using Core.Converters;

    public static class FileHeaderBlockParser
    {
        public static FileHeaderBlock Parse(byte[] blockBytes)
        {
            var type = BigEndianConverter.ConvertBytesToInt32(blockBytes);
            
            if (type != Constants.T_HEADER)
            {
                throw new IOException("Invalid file header block type");
            }
            
            var headerKey = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4);
            var highSeq = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8);
            var firstData = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10);
            var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x14);

            var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 0x14);
            if (checksum != calculatedChecksum)
            {
                throw new IOException("Invalid file header block checksum");
            }
            
            var indexSize = FastFileSystemHelper.CalculateHashtableSize((uint)blockBytes.Length);
            var index = new List<uint>();
            for (var i = 0; i < indexSize; i++)
            {
                index.Add(BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x18 + (i * SizeOf.Long)));
            }

            var fileHeaderBlock = new FileHeaderBlock(blockBytes.Length)
            {
                BlockBytes = blockBytes,
                HeaderKey = headerKey,
                HighSeq = highSeq,
                FirstData = firstData,
                Checksum = checksum,
                IndexSize = indexSize,
                Index = index.ToArray(),
            };
            
            EntryBlockParser.ReadGenericEntryBlock(fileHeaderBlock, blockBytes);

            if (fileHeaderBlock.SecType != Constants.ST_FILE)
            {
                throw new IOException($"Invalid file header block sec type '{type}'");
            }

            return fileHeaderBlock;
        }
    }
}