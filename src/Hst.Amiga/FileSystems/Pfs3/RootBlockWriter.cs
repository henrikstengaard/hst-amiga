namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using Blocks;
    using Core.Converters;

    public static class RootBlockWriter
    {
        public static byte[] BuildBlock(RootBlock rootBlock, globaldata g)
        {
            var blockBytes = new byte[512];
            if (rootBlock.BlockBytes != null)
            {
                Array.Copy(rootBlock.BlockBytes, 0, blockBytes, 0,
                    Math.Min(rootBlock.BlockBytes.Length, 512));
            }

            BigEndianConverter.ConvertInt32ToBytes(rootBlock.DiskType, blockBytes, 0); // 0
            BigEndianConverter.ConvertUInt32ToBytes((uint)rootBlock.Options, blockBytes, 4); // 0
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.Datestamp, blockBytes, 8); // 8
            DateHelper.WriteDate(rootBlock.CreationDate, blockBytes, 0xc); // 12
            BigEndianConverter.ConvertUInt16ToBytes(rootBlock.Protection, blockBytes, 0x12); // 18

            var diskNameBytes = AmigaTextHelper.GetBytes(rootBlock.DiskName.Length > 31 ? rootBlock.DiskName.Substring(0, 31) : rootBlock.DiskName);
            blockBytes[0x14] = (byte)diskNameBytes.Length;// 20
            Array.Copy(diskNameBytes, 0, blockBytes, 0x15, diskNameBytes.Length);// 21
            
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.LastReserved, blockBytes, 0x34); // 52
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.FirstReserved, blockBytes, 0x38); // 56
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.ReservedFree, blockBytes, 0x3c); // 60
            BigEndianConverter.ConvertUInt16ToBytes(rootBlock.ReservedBlksize, blockBytes, 0x40); // 64
            BigEndianConverter.ConvertUInt16ToBytes(rootBlock.RblkCluster, blockBytes, 0x42); // 66
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.BlocksFree, blockBytes, 0x44); // 68
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.AlwaysFree, blockBytes, 0x48); // 72
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.RovingPtr, blockBytes, 0x4c); // 76
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.DelDir, blockBytes, 0x50); // 80
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.DiskSize, blockBytes, 0x54); // 84
            BigEndianConverter.ConvertUInt32ToBytes(rootBlock.Extension, blockBytes, 0x58); // 88
            BigEndianConverter.ConvertUInt32ToBytes(0, blockBytes, 0x5c); // 92, not used
            
            var offset = 0x60;
            foreach (var index in rootBlock.idx.union)
            {
                BigEndianConverter.ConvertUInt32ToBytes(index, blockBytes, offset);
                offset += Amiga.SizeOf.ULong;
            }

            return blockBytes;            
        }
    }
}