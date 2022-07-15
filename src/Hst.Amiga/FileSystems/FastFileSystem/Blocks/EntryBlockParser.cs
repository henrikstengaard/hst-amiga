namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System.IO;
    using Core.Converters;

    public static class EntryBlockParser
    {
        public static EntryBlock Parse(byte[] blockBytes)
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
                    return DirBlockParser.Parse(blockBytes);
                case Constants.ST_FILE:
                    return FileHeaderBlockParser.Parse(blockBytes);
                default:
                    throw new IOException($"Invalid entry block sec type '{secType}'");
            }
        }
    }
}