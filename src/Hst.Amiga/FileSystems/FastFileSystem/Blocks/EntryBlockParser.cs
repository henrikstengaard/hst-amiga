namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System.IO;
    using Core.Converters;

    public static class EntryBlockParser
    {
        public static EntryBlock Parse(byte[] blockBytes, bool useLnfs = false)
        {
            var type = BigEndianConverter.ConvertBytesToInt32(blockBytes);
            var secType = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x1f0 + (SizeOf.Long * 3));

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
    }
}