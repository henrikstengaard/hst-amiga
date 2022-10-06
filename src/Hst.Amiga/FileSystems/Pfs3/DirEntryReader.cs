namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Converters;
    using Hst.Core.Extensions;

    public static class DirEntryReader
    {
        public static async Task<direntry> Read(Stream stream)
        {
            return new direntry
            {
                next = (byte)stream.ReadByte(),
                type = (byte)stream.ReadByte(),
                anode = await stream.ReadBigEndianUInt32(),
                fsize = await stream.ReadBigEndianUInt32(),
                CreationDate = await DateHelper.ReadDate(stream),
                protection = (byte)stream.ReadByte(),
                nlength = (byte)stream.ReadByte(),
                startofname = (byte)stream.ReadByte(),
                pad = (byte)stream.ReadByte()
            };
        }
        
        public static direntry Read(byte[] bytes, int offset)
        {
            var nlength = bytes[offset + 17];
            return new direntry
            {
                Offset = offset,
                next = bytes[offset],
                type = bytes[offset + 1],
                anode = BigEndianConverter.ConvertBytesToUInt32(bytes, offset + 2),
                fsize = BigEndianConverter.ConvertBytesToUInt32(bytes, offset + 6),
                CreationDate = DateHelper.ReadDate(bytes, offset + 10),
                protection = bytes[offset + 16],
                nlength = nlength,
                name = AmigaTextHelper.GetString(bytes, offset + 18, nlength),
                startofname = (byte)(offset + 18),
                pad = 0
            };
        }
    }
}