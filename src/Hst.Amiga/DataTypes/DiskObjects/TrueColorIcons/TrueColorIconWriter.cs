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
            var chunkData = new MemoryStream();
            await chunkData.WriteAsync(BigEndianConverter.ConvertUInt32ToBytes(length));
            await chunkData.WriteAsync(type);
            await chunkData.WriteAsync(data);
            var crc32 = new Crc32();
            crc32.Compute(type);
            crc32.Compute(data);
            var crc = crc32.GetCalculatedCrc();
            await chunkData.WriteAsync(BigEndianConverter.ConvertUInt32ToBytes(crc));
            
            return new PngChunk(chunkData.ToArray(), length, type, data, crc);
        }
    }
}