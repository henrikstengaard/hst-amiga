using System.Collections.Generic;

namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System.IO;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;

    public static class TextDataReader
    {
        public static async Task<TextData> Read(Stream stream, bool ignoreSize)
        {
            if (ignoreSize)
            {
                var dataBytes = new List<byte>(30);
                do
                {
                    var dataByte = stream.ReadByte();
                    if (dataByte > 0)
                    {
                        dataBytes.Add((byte)dataByte);
                    }
                } while (stream.Position < stream.Length);
                
                dataBytes.Add(0);
                
                return new TextData
                {
                    Size = (byte)dataBytes.Count,
                    Data = dataBytes.ToArray()
                };
            }

            var size = await stream.ReadBigEndianUInt32();
            var data = await stream.ReadBytes((int)size);

            if (data[size - 1] != 0)
            {
                throw new IOException("Invalid zero byte");
            }

            return new TextData
            {
                Size = size,
                Data = data
            };
        }
    }
}