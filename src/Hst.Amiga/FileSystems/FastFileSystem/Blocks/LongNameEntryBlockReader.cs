using System.Collections.Generic;
using System.IO;
using Hst.Amiga.Extensions;
using Hst.Core.Converters;

namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    /// <summary>
    /// Long name entry block reader for long name file system (LNFS) present in DOS\6 and DOS\7.
    /// </summary>
    public static class LongNameEntryBlockReader
    {
        public static EntryBlock Read(byte[] blockBytes)
        {
            var type = BigEndianConverter.ConvertBytesToInt32(blockBytes);
            
            if (type != Constants.T_HEADER)
            {
                throw new IOException("Invalid long name file system file header block type");
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

            var access = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0xc0);
            var byteSize = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0xbc);
            
            // char  NaC[112];      /* Merged name and comment */
            var name = blockBytes.ReadStringWithLength(blockBytes.Length - 0xb8);
            var comment = blockBytes.ReadStringWithLength(blockBytes.Length - 0xb8 + name.Length + 1);
            
            /* Number of the block which holds the associated comment string */
            var commentBlock = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x48); // set, if comment is present in comment block

            // long * 2: spare 4 / not used, must be set to zero
            
            var date = DateHelper.ReadDate(blockBytes, blockBytes.Length - 0x3c);

            // long * 2: spare 2 / not used, must be set to zero
            
            var realEntry = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x2c);
            var nextLink = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x28);
            
            // long * 6: spare 6 / not used, must be set to zero
            
            var nextSameHash = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x10);
            var parent = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0xc);
            var extension = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x8);
            var secType = BigEndianConverter.ConvertBytesToInt32(blockBytes, blockBytes.Length - 0x4);

            if (secType != Constants.ST_FILE &&
                secType != Constants.ST_DIR &&
                secType != Constants.ST_LFILE &&
                secType != Constants.ST_LDIR)
            {
                throw new IOException($"Invalid long name entry block sec type '{type}'");
            }
            
            return new EntryBlock(blockBytes.Length)
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
                CommentBlock = commentBlock,
                SecType = secType
            };
        }
    }
}