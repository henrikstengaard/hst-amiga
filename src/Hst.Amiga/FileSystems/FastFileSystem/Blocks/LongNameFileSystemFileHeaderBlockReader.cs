namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System.Collections.Generic;
    using System.IO;
    using Amiga.Extensions;
    using Core.Converters;

    /// <summary>
    /// Reader for long name file system file header block (LNFS) present in DOS\6 and DOS\7
    /// </summary>
    public static class LongNameFileSystemFileHeaderBlockReader
    {
        public static FileHeaderBlock Parse(byte[] blockBytes)
        {
            var type = BigEndianConverter.ConvertBytesToInt32(blockBytes);
            
            if (type != Constants.T_HEADER)
            {
                throw new IOException("Invalid long name file system file header block type");
            }
            
            var headerKey = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4);
            var highSeq = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8);
            var indexSize = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc); // hashtable & data blocks
            var firstData = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10);
            var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x14);

            var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 0x14);
            if (checksum != calculatedChecksum)
            {
                throw new IOException("Invalid file header block checksum");
            }
            
            var index = new List<uint>();
            for (var i = 0; i < Constants.INDEX_SIZE; i++)
            {
                index.Add(BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x18 + (i * SizeOf.Long)));
            }

            var access = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x140);
            var byteSize = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x144);
            
            // char  NaC[112];      /* Merged name and comment */
            var name = blockBytes.ReadStringWithLength(0x148);
            var comment = blockBytes.ReadStringWithLength(0x148 + name.Length + 1);
            
            /* Number of the block which holds the associated comment string */
            var commentBlock = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1b8); // set, if comment is present in comment block

            // long * 2: spare 4 / not used, must be set to zero
            
            var date = DateHelper.ReadDate(blockBytes, 0x1c4);

            // long * 2: spare 2 / not used, must be set to zero
            
            var realEntry = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1d4);
            var nextLink = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1d8);
            
            // long * 6: spare 6 / not used, must be set to zero
            
            var nextSameHash = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1f0);
            var parent = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1f4);
            var extension = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1f8);
            var secType = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x1f0 + (SizeOf.Long * 3));

            if (secType != Constants.ST_FILE)
            {
                throw new IOException($"Invalid long name file system file header block sec type '{type}'");
            }
            
            return new FileHeaderBlock
            {
                BlockBytes = blockBytes,
                HeaderKey = headerKey,
                HighSeq = highSeq,
                FirstData = firstData,
                Checksum = checksum,
                IndexSize = indexSize,
                Index = index.ToArray(),
                Access = access,
                ByteSize = byteSize,
                Comment = comment,
                Date = date,
                Name = name,
                RealEntry = realEntry,
                NextLink = nextLink,
                NextSameHash = nextSameHash,
                Parent = parent,
                Extension = extension,
                CommentBlock = commentBlock
            };
        }
    }
}