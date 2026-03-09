using System.Collections.Generic;

namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System.IO;
    using Amiga.Extensions;
    using Core.Converters;

    public static class EntryBlockParser
    {
        public static EntryBlock Parse(byte[] blockBytes, bool useLnfs = false)
        {
            var type = BigEndianConverter.ConvertBytesToInt32(blockBytes);
            var secType = BigEndianConverter.ConvertBytesToInt32(blockBytes, blockBytes.Length - 0x4);

            if (type != Constants.T_HEADER)
            {
                throw new IOException($"Invalid entry block type '{type}'");
            }

            switch (secType)
            {
                case Constants.ST_ROOT:
                    return RootBlockParser.Parse(blockBytes);
                case Constants.ST_DIR:
                case Constants.ST_FILE:
                case Constants.ST_LDIR:
                case Constants.ST_LFILE:
                    return useLnfs
                        ? LongNameEntryBlockReader.Read(blockBytes)
                        : ParseEntryBlock(blockBytes);
                default:
                    throw new IOException($"Invalid entry block sec type '{secType}'");
            }
        }

        public static EntryBlock ParseEntryBlock(byte[] blockBytes)
        {
            var type = BigEndianConverter.ConvertBytesToInt32(blockBytes);
            
            if (type != Constants.T_HEADER)
            {
                throw new IOException("Invalid entry block type");
            }

            var headerKey = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4);
            var highSeq = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8);
            var firstData = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10);
            var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x14);

            var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 0x14);
            if (checksum != calculatedChecksum)
            {
                throw new IOException("Invalid entry block checksum");
            }

            var indexSize = FastFileSystemHelper.CalculateHashtableSize((uint)blockBytes.Length);
            var index = new List<uint>();
            for (var i = 0; i < indexSize; i++)
            {
                index.Add(BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x18 + (i * SizeOf.Long)));
            }

            var access = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0xc0); // block size - 0xc0: access / protection
            var byteSize = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0xbc); // block size - 0xbc: byte size
            var comment = blockBytes.ReadStringWithLength(blockBytes.Length - 0xb8, Constants.MAXCMMTLEN); // block size - 0xb8: comment length (first byte) + comment max length 79 chars
            var date = DateHelper.ReadDate(blockBytes, blockBytes.Length - 0x5c); // block size - 0x5c: last access date
            var name = blockBytes.ReadStringWithLength(blockBytes.Length - 0x50, Constants.MAXNAMELEN); // block size - 0x4f: name length (first byte) + name max length 30 chars
            var realEntry = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x2c); // block size - 0x2c: real_entry, FFS : pointer to "real" file or directory
            var nextLink = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x28); // block size - 0x28: next_link, FFS : hardlinks chained list (first=newest)
            var nextSameHash = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x10); // block size - 0x10: hash_chain, next entry ptr with same hash
            var parent = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x0c); // block size - 0x0c: parent directory
            var extension = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x08); // block size - 0x08: FFS : first directory cache block
            var secType = BigEndianConverter.ConvertBytesToInt32(blockBytes, blockBytes.Length - 0x04); // block size - 0x04: secondary type, eg. ST_USERDIR (== 2) for directory
            
            if (secType != Constants.ST_FILE &&
                secType != Constants.ST_DIR &&
                secType != Constants.ST_LFILE &&
                secType != Constants.ST_LDIR)
            {
                throw new IOException($"Invalid entry block sec type '{type}'");
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
                SecType = secType
            };
        }
    }
}