using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Hst.Core.Converters;

namespace Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons
{
    public static class TrueColorIconWriter
    {
        public static async Task WriteTrueColorIcons(IEnumerable<TrueColorIcon> trueColorIcons, Stream stream)
        {
            foreach (var trueColorIcon in trueColorIcons)
            {
                await stream.WriteAsync(trueColorIcon.PngData);
            }
        }
        
        public static async Task<PngChunk> CreatePngChunk(byte[] type, byte[] data)
        {
            var length = (uint)data.Length;
            var chunkData = new byte[4 + type.Length + data.Length + 4];
            BigEndianConverter.ConvertUInt32ToBytes(length, chunkData, 0);
            System.Buffer.BlockCopy(type, 0, chunkData, 4, type.Length);
            System.Buffer.BlockCopy(data, 0, chunkData, 4 + type.Length, data.Length);
            var crc32 = new Crc32();
            crc32.Compute(type);
            crc32.Compute(data);
            var crc = crc32.GetCalculatedCrc();
            BigEndianConverter.ConvertUInt32ToBytes(crc, chunkData, 4 + type.Length + data.Length);
            
            return new PngChunk(chunkData, length, type, data, crc);
        }
    }
}