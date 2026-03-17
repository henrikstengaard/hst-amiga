using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Core.Converters;
using Hst.Core.Extensions;
using Hst.Imaging;

namespace Hst.Amiga.DataTypes.DiskObjects.PngIcons
{
    public static class PngIconReader
    {
        public static async Task<IEnumerable<PngIcon>> Read(Stream stream)
        {
            var pngIcon1 = await ReadPngIcon(stream);

            if (stream.Position == stream.Length)
            {
#if !NETSTANDARD2_1_OR_GREATER
                return [pngIcon1];
#else
                return new[] { pngIcon1 };
#endif
            }
            
            var pngIcon2 = await ReadPngIcon(stream);
            
#if !NETSTANDARD2_1_OR_GREATER
            return [pngIcon1, pngIcon2];
#else
            return new[] { pngIcon1, pngIcon2 };
#endif
        }

        private static async Task<PngIcon> ReadPngIcon(Stream stream)
        {
            var pngData = new MemoryStream();
            
            var header = await stream.ReadBytes(8);
            if (header.Length < 8)
            {
                throw new IOException("Invalid PNG header");
            }
            
            await pngData.WriteAsync(header);

            var chunks = new List<PngChunk>();
            PngChunk chunk;
            do
            {
                chunk = await ReadPngChunk(stream);
                chunks.Add(chunk);
                await pngData.WriteAsync(chunk.ChunkData);
            } while(stream.Position < stream.Length &&
                    !chunk.Type.SequenceEqual(Constants.PngChunkTypes.Iend));

            return new PngIcon(pngData.ToArray(), header, chunks.ToArray());
        }

        private static async Task<PngChunk> ReadPngChunk(Stream stream)
        {
            var chunkData = new MemoryStream();
            
            var lengthBytes = await stream.ReadBytes(4);
            var length = BigEndianConverter.ConvertBytesToUInt32(lengthBytes);
            chunkData.Write(lengthBytes);

            var type = await stream.ReadBytes(4);
            chunkData.Write(type);

            var data = length > 0 ? await stream.ReadBytes((int)length) : Array.Empty<byte>();
            chunkData.Write(data);

            var crcBytes = await stream.ReadBytes(4);
            var crc = BigEndianConverter.ConvertBytesToUInt32(crcBytes);
            chunkData.Write(crcBytes);

            return new PngChunk(chunkData.ToArray(), length, type, data, crc);
        }

        public static IconData ReadIconData(byte[] iconChunk)
        {
            var tags = new List<IconAttributeTag>();
            string defaultTool = null;
            string toolType = null;
            
            var position = 0;
            while (position < iconChunk.Length)
            {
                var tagUIntValue = BigEndianConverter.ConvertBytesToUInt32(iconChunk, position);
                position += 4;
                var tag = (Constants.IconAttributeTags)tagUIntValue;

                switch (tag)
                {
                    case Constants.IconAttributeTags.ATTR_ICONX:
                    case Constants.IconAttributeTags.ATTR_ICONY:
                    case Constants.IconAttributeTags.ATTR_DD_CURRENTX:
                    case Constants.IconAttributeTags.ATTR_DD_CURRENTY:
                    case Constants.IconAttributeTags.ATTR_DRAWERX:
                    case Constants.IconAttributeTags.ATTR_DRAWERY:
                    case Constants.IconAttributeTags.ATTR_DRAWERWIDTH:
                    case Constants.IconAttributeTags.ATTR_DRAWERHEIGHT:
                    case Constants.IconAttributeTags.ATTR_DRAWERFLAGS:
                    case Constants.IconAttributeTags.ATTR_DRAWERFLAGS2:
                    case Constants.IconAttributeTags.ATTR_DRAWERFLAGS3:
                    case Constants.IconAttributeTags.ATTR_FRAMELESS:
                    case Constants.IconAttributeTags.ATTR_STACKSIZE:
                    case Constants.IconAttributeTags.ATTR_TYPE:
                    case Constants.IconAttributeTags.ATTR_VIEWMODES:
                    case Constants.IconAttributeTags.ATTR_VIEWMODES2:
                        var value = BigEndianConverter.ConvertBytesToUInt32(iconChunk, position);
                        position += 4;
                        tags.Add(new IconAttributeTag(tag, value));
                        break;
                    case Constants.IconAttributeTags.ATTR_DEFAULTTOOL:
                        var defaultToolLength = ReadTextLength(iconChunk, position);
                        defaultTool = AmigaTextHelper.GetString(iconChunk, position, defaultToolLength);
                        position += defaultToolLength + 1;
                        break;
                    case Constants.IconAttributeTags.ATTR_TOOLTYPE:
                        var toolTypeLength = ReadTextLength(iconChunk, position);
                        toolType = AmigaTextHelper.GetString(iconChunk, position, toolTypeLength);
                        position += toolTypeLength + 1;
                        break;
                    default:
                        throw new InvalidDataException($"Unknown icon attribute tag: 0x{tagUIntValue:x}");
                }
            }

            return new IconData(tags, defaultTool, toolType);
        }
        
        private static int ReadTextLength(byte[] data, int offset)
        {
            var length = 0;
            while (offset + length < data.Length && data[offset + length] != 0)
            {
                length++;
            }

            return length;
        }

        public static PngHeader ReadPngHeader(byte[] iHdrChunk)
        {
            var width = BigEndianConverter.ConvertBytesToUInt32(iHdrChunk, 0);
            var height = BigEndianConverter.ConvertBytesToUInt32(iHdrChunk, 4);
            var bitDepth = iHdrChunk[8];
            var colorType = iHdrChunk[9];
            var compressionMethod = iHdrChunk[10];
            var filterMethod = iHdrChunk[11];
            var interlaceMethod = iHdrChunk[12];

            return new PngHeader(width, height, bitDepth, colorType, compressionMethod, filterMethod, interlaceMethod);
        }
    }
}