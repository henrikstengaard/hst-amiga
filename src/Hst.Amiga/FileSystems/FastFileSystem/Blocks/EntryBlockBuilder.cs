namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;

    public static class EntryBlockBuilder
    {
        public static byte[] Build(EntryBlock entryBlock, int blockSize, bool useLnfs = false)
        {
            if (entryBlock.Type != Constants.T_HEADER)
            {
                throw new ArgumentException($"Invalid entry block type '{entryBlock.SecType}'",
                    nameof(entryBlock.Type));
            }

            switch (entryBlock.SecType)
            {
                case Constants.ST_ROOT:
                    return RootBlockBuilder.Build(entryBlock as RootBlock, blockSize);
                case Constants.ST_DIR:
                    return useLnfs
                        ? LongNameFileSystemDirBlockWriter.Build(entryBlock as DirBlock, blockSize)
                        : DirBlockBuilder.Build(entryBlock as DirBlock, blockSize);
                case Constants.ST_FILE:
                    return useLnfs
                        ? LongNameFileSystemFileHeaderBlockWriter.Build(entryBlock as FileHeaderBlock, blockSize) 
                        : FileHeaderBlockBuilder.Build(entryBlock as FileHeaderBlock, blockSize);
                default:
                    throw new ArgumentException($"Invalid entry block secondary type '{entryBlock.SecType}'",
                        nameof(entryBlock.SecType));
            }
        }
    }
}