namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using Blocks;
    using Core.Converters;

    public static class AnodeBlockWriter
    {
        public static byte[] BuildBlock(anodeblock anodeBlock, globaldata g)
        {
            var blockBytes = new byte[g.RootBlock.ReservedBlksize];
            if (anodeBlock.BlockBytes != null)
            {
                Array.Copy(anodeBlock.BlockBytes, 0, blockBytes, 0,
                    Math.Min(anodeBlock.BlockBytes.Length, g.RootBlock.ReservedBlksize));
            }

            BigEndianConverter.ConvertUInt16ToBytes(anodeBlock.id, blockBytes, 0);
            BigEndianConverter.ConvertUInt16ToBytes(0, blockBytes, 0x2); // not_used 1
            BigEndianConverter.ConvertUInt32ToBytes(anodeBlock.datestamp, blockBytes, 0x4);
            BigEndianConverter.ConvertUInt32ToBytes(anodeBlock.seqnr, blockBytes, 0x8);
            BigEndianConverter.ConvertUInt32ToBytes(0, blockBytes, 0xc); // not_used 2

            var offset = 0x10;
            foreach (var anode in anodeBlock.nodes)
            {
                BigEndianConverter.ConvertUInt32ToBytes(anode.clustersize, blockBytes, offset);
                BigEndianConverter.ConvertUInt32ToBytes(anode.blocknr, blockBytes, offset + 0x4);
                BigEndianConverter.ConvertUInt32ToBytes(anode.next, blockBytes, offset + 0x8);
                offset += 3 * Amiga.SizeOf.ULong;
            }

            anodeBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}