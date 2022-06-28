namespace Hst.Amiga.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Core.Extensions;

    public static class StreamExtensions
    {
        /// <summary>
        /// Read string first by reading length of string and then read string 
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static async Task<string> ReadString(this Stream stream)
        {
            var length = stream.ReadByte();
            return AmigaTextHelper.GetString(await stream.ReadBytes(length));
        }
        
        public static async Task<string> ReadString(this Stream stream, int length)
        {
            return AmigaTextHelper.GetString(await stream.ReadBytes(length));
        }

        public static async Task<string> ReadNullTerminatedString(this Stream stream)
        {
            var stringBytes = new List<byte>();

            byte[] buffer = new byte[1];
            int bytesRead;
            do
            {
                bytesRead = await stream.ReadAsync(buffer, 0, 1);
                if (bytesRead == 1 && buffer[0] != 0)
                {
                    stringBytes.Add(buffer[0]);
                }
            } while (bytesRead == 1 && buffer[0] != 0);
            
            return AmigaTextHelper.GetString(stringBytes.ToArray());
        }
        
        public static async Task WriteString(this Stream stream, string value, int length, byte fillByte = 0)
        {
            var bytes = AmigaTextHelper.GetBytes(value.Length > length
                ? value.Substring(0, length)
                : value);

            await stream.WriteBytes(bytes);
            
            if (bytes.Length < length)
            {
                var fillBytes = new byte[length - bytes.Length];
                for (var i = 0; i < fillBytes.Length; i++)
                {
                    fillBytes[i] = fillByte;
                }
                await stream.WriteBytes(fillBytes);
            }
        }

        public static async Task WriteStringWithLength(this Stream stream, string value, int maxLength)
        {
            stream.WriteByte((byte)Math.Min(value.Length, maxLength));
            await stream.WriteString(value, maxLength);
        }
    }
}