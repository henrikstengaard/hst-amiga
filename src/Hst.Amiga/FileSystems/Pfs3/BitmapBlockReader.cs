namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.Collections.Generic;
    using Blocks;
    using Core.Converters;

    public static class BitmapBlockReader
    {
        public static BitmapBlock Parse(byte[] blockBytes, int longsPerBmb)
        {
            var id = BigEndianConverter.ConvertBytesToUInt16(blockBytes);
            if (id == 0)
            {
                return null;
            }
            
            var notUsed1 = BigEndianConverter.ConvertBytesToUInt16(blockBytes, 2);
            var datestamp = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 4);
            var seqNr = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 8);
            
            var bitmap = new List<uint>();
            var offset = 0xc;
            for (var i = 0; i < longsPerBmb; i++)
            {
                bitmap.Add(BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset));
                offset += Amiga.SizeOf.ULong;
            }
            
            return new BitmapBlock(longsPerBmb)
            {
                id = id,
                not_used_1 = notUsed1,
                datestamp = datestamp,
                seqnr = seqNr,
                bitmap = bitmap.ToArray()
            };
        }
    }
}