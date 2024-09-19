using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Hst.Core.Converters;

namespace Hst.Amiga.DataTypes.UaeFsDbs
{
    public static class UaeFsDbReader
    {
        /// <summary>
        /// Read UAEFSDB node from bytes
        /// </summary>
        /// <param name="data">Bytes to read UAEFSDB nodes from</param>
        /// <param name="offset">Offset in bytes to read from</param>
        /// <param name="version">UAEFSDB node version to read</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static UaeFsDbNode ReadFromBytes(byte[] data, int offset = 0, UaeFsDbNode.NodeVersion version = UaeFsDbNode.NodeVersion.Version1)
        {
            if (data.Length < Constants.UaeFsDbNodeVersion1Size)
            {
                throw new ArgumentException($"Data bytes {data.Length} is less than UAEFSDB node v1 size of {Constants.UaeFsDbNodeVersion1Size} bytes", nameof(data));
            }
            
            if (offset + Constants.UaeFsDbNodeVersion1Size > data.Length)
            {
                throw new ArgumentOutOfRangeException($"Data bytes {data.Length} doesn't fit a UAEFSDB node v1 size of {Constants.UaeFsDbNodeVersion1Size} bytes reading from offset {offset}", nameof(data));
            }

            var valid = data[offset];
            var mode = BigEndianConverter.ConvertBytesToUInt32(data, offset + 0x1);
            var amigaName = AmigaTextHelper.GetNullTerminatedString(data, offset + 0x5, 256);
            var normalName = TextHelper.ReadNullTerminatedString(Encoding.UTF8, data, offset + 0x106, 257);
            var comment = TextHelper.ReadNullTerminatedString(Encoding.UTF8, data, offset + 0x207, 81);

            if (version != UaeFsDbNode.NodeVersion.Version2)
            {
                return new UaeFsDbNode
                {
                    Version = version,
                    Valid = valid,
                    Mode = mode,
                    AmigaName = amigaName,
                    NormalName = normalName,
                    Comment = comment,
                    WinMode = 0,
                    AmigaNameUnicode = string.Empty,
                    NormalNameUnicode = string.Empty
                };
            }

            if (offset + Constants.UaeFsDbNodeVersion2Size > data.Length)
            {
                throw new ArgumentOutOfRangeException($"Data bytes {data.Length} doesn't fit a UAEFSDB node v2 size of {Constants.UaeFsDbNodeVersion2Size} bytes reading from offset {offset}", nameof(data)); 
            }
            
            var winMode = BigEndianConverter.ConvertBytesToUInt32(data, offset + 0x258);
            var amigaNameUnicode = TextHelper.ReadNullTerminatedString(Encoding.Unicode, data, offset + 0x25c, 514);
            var normalNameUnicode = TextHelper.ReadNullTerminatedString(Encoding.Unicode, data, offset + 0x45e, 514);

            return new UaeFsDbNode
            {
                Version = version,
                Mode = mode,
                AmigaName = amigaName,
                NormalName = normalName,
                Comment = comment,
                WinMode = winMode,
                AmigaNameUnicode = amigaNameUnicode,
                NormalNameUnicode = normalNameUnicode 
            };
        }

        /// <summary>
        /// Read UAEFSDB nodes from stream.
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <returns>List of UAEFSDB nodes read</returns>
        public static async Task<IEnumerable<UaeFsDbNode>> ReadFromStream(Stream stream)
        {
            var nodes = new List<UaeFsDbNode>();

            var uaeFsDbNodeVersion = UaeFsDbNodeHelper.GetUaeFsDbNodeVersion(stream.Length);

            var uaeFsDbNodeSize = uaeFsDbNodeVersion == UaeFsDbNode.NodeVersion.Version1
                ? Constants.UaeFsDbNodeVersion1Size
                : Constants.UaeFsDbNodeVersion2Size;

            var buffer = new byte[uaeFsDbNodeSize];
            int bytesRead;
            
            while ((bytesRead = await stream.ReadAsync(buffer, 0, uaeFsDbNodeSize)) > 0)
            {
                if (uaeFsDbNodeSize != bytesRead)
                {
                    throw new InvalidOperationException($"Read {bytesRead} bytes, but expected {uaeFsDbNodeSize} bytes");
                }

                var node = ReadFromBytes(buffer, 0);
                nodes.Add(node);
            }

            return nodes;
        }

        /// <summary>
        /// Read UAEFSDB nodes from file.
        /// </summary>
        /// <param name="path">Path to read UAEFSDB nodes from</param>
        /// <returns>List of UAEFSDB nodes read</returns>
        public static async Task<IEnumerable<UaeFsDbNode>> ReadFromFile(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                return await ReadFromStream(stream);
            }
        }
    }
}