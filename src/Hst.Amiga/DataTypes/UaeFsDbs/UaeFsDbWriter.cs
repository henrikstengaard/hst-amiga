using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Hst.Core.Converters;
using Hst.Core.Extensions;

namespace Hst.Amiga.DataTypes.UaeFsDbs
{
    public static class UaeFsDbWriter
    {
        /// <summary>
        /// Build UAEFSDB bytes from UaeFsDbNode
        /// </summary>
        /// <param name="node">UaeFsDbNode to build from</param>
        /// <returns>UAEFSDB bytes with UaeFsDbNode data</returns>
        public static byte[] Build(UaeFsDbNode node)
        {
            if (string.IsNullOrEmpty(node.AmigaName))
            {
                throw new ArgumentException("Amiga name is required", nameof(node));
            }

            if (string.IsNullOrWhiteSpace(node.NormalName))
            {
                throw new ArgumentException("Normal name is required", nameof(node));
            }

            var nodeBytes = new byte[node.Version == UaeFsDbNode.NodeVersion.Version1
                ? Constants.UaeFsDbNodeVersion1Size
                : Constants.UaeFsDbNodeVersion2Size];
            
            nodeBytes[0] = node.Valid;
            BigEndianConverter.ConvertUInt32ToBytes(node.Mode, nodeBytes, 0x1);
            TextHelper.WriteNullTerminatedString(Encoding.ASCII, node.AmigaName, nodeBytes, 0x5, 257);
            TextHelper.WriteNullTerminatedString(Encoding.ASCII, node.NormalName, nodeBytes, 0x106, 257);
            TextHelper.WriteNullTerminatedString(Encoding.ASCII, node.Comment ?? string.Empty, nodeBytes, 0x207, 81);

            if (node.Version != UaeFsDbNode.NodeVersion.Version2)
            {
                return nodeBytes;
            }

            var amigaNameUnicode = string.IsNullOrWhiteSpace(node.AmigaNameUnicode) ? node.AmigaName : node.AmigaNameUnicode;
            var normalNameUnicode = string.IsNullOrWhiteSpace(node.NormalNameUnicode) ? node.NormalName : node.NormalNameUnicode;

            BigEndianConverter.ConvertUInt32ToBytes(node.WinMode, nodeBytes, 0x258);
            TextHelper.WriteNullTerminatedString(Encoding.Unicode, amigaNameUnicode, nodeBytes, 0x25c, 514);
            TextHelper.WriteNullTerminatedString(Encoding.Unicode, normalNameUnicode, nodeBytes, 0x45e, 514);

            return nodeBytes;
        }

        /// <summary>
        /// Write UAEFSDB node to stream
        /// </summary>
        /// <param name="stream">Stream to write UAEFSDB node to</param>
        /// <param name="node">UAEFSDB node to write</param>
        /// <returns>Task</returns>
        public static async Task WriteToStream(Stream stream, UaeFsDbNode node)
        {
            stream.Seek(0, SeekOrigin.Begin);

            switch(node.Version)
            {
                case UaeFsDbNode.NodeVersion.Version1:
                    await FindVersion1NodePosition(stream, node);
                    break;
                case UaeFsDbNode.NodeVersion.Version2:
                    FindVersion2NodePosition(stream, node);
                    break;
            }

            var nodeBytes = Build(node);

            await stream.WriteAsync(nodeBytes, 0, nodeBytes.Length);
        }

        private static async Task FindVersion1NodePosition(Stream stream, UaeFsDbNode node)
        {
            if (stream.Length % Constants.UaeFsDbNodeVersion1Size != 0)
            {
                throw new ArgumentException($"Stream length {stream.Length} is not a multiple of UAEFSDB node size {Constants.UaeFsDbNodeVersion1Size} bytes", nameof(stream));
            }

            while (stream.Length >= Constants.UaeFsDbNodeVersion1Size && stream.Position < stream.Length)
            {
                var position = stream.Position;

                var bytes = await stream.ReadBytes(Constants.UaeFsDbNodeVersion1Size);

                var existingNode = UaeFsDbReader.ReadFromBytes(bytes);

                if (node.AmigaName.Equals(existingNode.AmigaName, StringComparison.OrdinalIgnoreCase))
                {
                    stream.Seek(position, SeekOrigin.Begin);

                    break;
                }
            }
        }

        private static void FindVersion2NodePosition(Stream stream, UaeFsDbNode node)
        {
            if (stream.Length % Constants.UaeFsDbNodeVersion2Size != 0)
            {
                throw new ArgumentException($"Stream length {stream.Length} is not a multiple of UAEFSDB node size {Constants.UaeFsDbNodeVersion2Size} bytes", nameof(stream));
            }

            stream.SetLength(Constants.UaeFsDbNodeVersion2Size);
        }

        /// <summary>
        /// Write UAEFSDB node to file
        /// </summary>
        /// <param name="uaeFsDbPath">Path to write UAEFSDB node to</param>
        /// <param name="node">UAEFSDB node to write</param>
        /// <returns>Task</returns>
        public static async Task WriteToFile(string uaeFsDbPath, UaeFsDbNode node)
        {
            using (var stream = File.OpenWrite(uaeFsDbPath))
            {
                await WriteToStream(stream, node);
            }
        }
    }
}