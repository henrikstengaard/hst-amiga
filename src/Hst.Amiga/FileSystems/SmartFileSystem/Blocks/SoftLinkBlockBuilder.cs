namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using System;
    using System.IO;
    using Core.Converters;

    public static class SoftLinkBlockBuilder
    {
        public static byte[] Build(SoftLinkBlock softLinkBlock, int blockSize)
        {
            var blockBytes = new byte[blockSize];
            BlockHelper.WriteHeader(blockBytes, softLinkBlock);

            BigEndianConverter.ConvertUInt32ToBytes(softLinkBlock.Parent, blockBytes, 0xc);
            BigEndianConverter.ConvertUInt32ToBytes(softLinkBlock.Next, blockBytes, 0x10);
            BigEndianConverter.ConvertUInt32ToBytes(softLinkBlock.Previous, blockBytes, 0x14);

            var pathBytes = AmigaTextHelper.GetBytes(softLinkBlock.Path ?? string.Empty);
            if (pathBytes.Length + 1 > blockSize - Constants.SoftLinkHeaderSize)
            {
                throw new IOException($"Soft link path is too long, max length is {blockSize - Constants.SoftLinkHeaderSize - 1}");
            }

            Array.Copy(pathBytes, 0, blockBytes, Constants.SoftLinkHeaderSize, pathBytes.Length);

            return BlockHelper.UpdateChecksum(blockBytes, softLinkBlock);
        }
    }
}
