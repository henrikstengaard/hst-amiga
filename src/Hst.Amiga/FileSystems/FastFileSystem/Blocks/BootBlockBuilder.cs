namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using Core.Converters;

    public static class BootBlockBuilder
    {
        public static byte[] Build(BootBlock bootBlock, int blockSize)
        {
            var blockBytes = new byte[blockSize];

            for (var i = 0; i < bootBlock.DosType.Length; i++)
            {
                blockBytes[i] = bootBlock.DosType[i];
            }

            BigEndianConverter.ConvertUInt32ToBytes(bootBlock.RootBlockOffset, blockBytes, 8);
            
            for (var i = 0; i < FloppyDiskConstants.BootableBootBlockBytes.Length; i++)
            {
                blockBytes[0xc + i] = FloppyDiskConstants.BootableBootBlockBytes[i];
            }
            
            BootBlockChecksumHelper.UpdateChecksum(blockBytes, 4);

            return blockBytes;
        }
    }
}