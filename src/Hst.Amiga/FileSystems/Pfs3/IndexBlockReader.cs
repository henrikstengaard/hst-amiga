namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.IO;
    using Blocks;
    using Core.Converters;

    public static class IndexBlockReader
    {
        public static indexblock Parse(byte[] blockBytes, globaldata g)
        {
            var id = BigEndianConverter.ConvertBytesToUInt16(blockBytes);
            if (id != Constants.IBLKID && id != Constants.SBLKID && id != Constants.BMIBLKID)
            {
                throw new IOException($"Invalid index block id '{id}'");
            }
            
            var datestamp = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4);
            var seqNr = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8);

            var offset = 0xc;
            var indexCount = (blockBytes.Length - Amiga.SizeOf.UWord * 2 - Amiga.SizeOf.ULong * 2) / Amiga.SizeOf.Long;
            var indexes = new int[indexCount];
            for (var i = 0; i < indexCount; i++)
            {
                indexes[i] = BigEndianConverter.ConvertBytesToInt32(blockBytes, offset);
                offset += Amiga.SizeOf.Long;
            }
            
            return new indexblock(g)
            {
                id = id,
                datestamp = datestamp,
                seqnr = seqNr,
                index = indexes
            };
        }
    }
}