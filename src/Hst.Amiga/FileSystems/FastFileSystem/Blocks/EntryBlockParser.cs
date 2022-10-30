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
                    return useLnfs
                        ? LongNameFileSystemDirBlockReader.Read(blockBytes) as EntryBlock
                        : DirBlockParser.Parse(blockBytes);
                case Constants.ST_FILE:
                    return useLnfs
                        ? LongNameFileSystemFileHeaderBlockReader.Parse(blockBytes) as EntryBlock
                        : FileHeaderBlockParser.Parse(blockBytes);
                default:
                    throw new IOException($"Invalid entry block sec type '{secType}'");
            }
        }

        public static void ReadGenericEntryBlock(EntryBlock entryBlock, byte[] blockBytes)
        {
            entryBlock.Access = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0xc0); // block size - 0xc0: access / protection
            entryBlock.ByteSize = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0xbc); // block size - 0xbc: byte size
            entryBlock.Comment = blockBytes.ReadStringWithLength(blockBytes.Length - 0xb8, Constants.MAXCMMTLEN); // block size - 0xb8: comment length (first byte) + comment max length 79 chars

            entryBlock.Date = DateHelper.ReadDate(blockBytes, blockBytes.Length - 0x5c); // block size - 0x5c: last access date
            entryBlock.Name = blockBytes.ReadStringWithLength(blockBytes.Length - 0x50, Constants.MAXNAMELEN); // block size - 0x4f: name length (first byte) + name max length 30 chars

            entryBlock.RealEntry = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x2c); // block size - 0x2c: real_entry, FFS : pointer to "real" file or directory
            entryBlock.NextLink = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x28); // block size - 0x28: next_link, FFS : hardlinks chained list (first=newest)

            entryBlock.NextSameHash = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x10); // block size - 0x10: hash_chain, next entry ptr with same hash
            entryBlock.Parent = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x0c); // block size - 0x0c: parent directory
            entryBlock.Extension = BigEndianConverter.ConvertBytesToUInt32(blockBytes, blockBytes.Length - 0x08); // block size - 0x08: FFS : first directory cache block
            var secType = BigEndianConverter.ConvertBytesToInt32(blockBytes, blockBytes.Length - 0x04); // block size - 0x04: secondary type, eg. ST_USERDIR (== 2) for directory

            if (entryBlock.SecType != secType)
            {
                throw new IOException(
                    $"Entry block '{entryBlock.GetType().Name}' secondary type '{entryBlock.SecType}' is not equal to secondary type read '{secType}'");
            }
        }
    }
}