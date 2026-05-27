using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Core.Converters;
using Hst.Core.Extensions;

namespace Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons
{
    public static class TrueColorIconReader
    {
        public static async Task<IEnumerable<TrueColorIcon>> ReadTrueColorIcons(Stream stream)
        {
            var trueColorIcon1 = await ReadTrueColorIcon(stream);

            if (stream.Position == stream.Length)
            {
#if !NETSTANDARD2_1_OR_GREATER
                return [trueColorIcon1];
#else
                return new[] { trueColorIcon1 };
#endif
            }
            
            var trueColorIcon2 = await ReadTrueColorIcon(stream);
            
#if !NETSTANDARD2_1_OR_GREATER
            return [trueColorIcon1, trueColorIcon2];
#else
            return new[] { trueColorIcon1, trueColorIcon2 };
#endif
        }

        private static async Task<TrueColorIcon> ReadTrueColorIcon(Stream stream)
        {
            var pngStream = new MemoryStream();
            
            var header = await stream.ReadBytes(8);
            if (header.Length < 8 || !header.SequenceEqual(Constants.PngSignature))
            {
                throw new IOException("Invalid PNG header");
            }
            
            await pngStream.WriteAsync(header);

            var chunks = new List<PngChunk>();
            PngChunk chunk;
            do
            {
                chunk = await ReadPngChunk(stream);
                chunks.Add(chunk);
                await pngStream.WriteAsync(chunk.ChunkData);
            } while(stream.Position < stream.Length &&
                    !chunk.Type.SequenceEqual(Constants.PngChunkTypes.Iend));

            pngStream.Position = 0;
            var image = Imaging.Pngcs.PngReader.Read(pngStream);
            
            return new TrueColorIcon(pngStream.ToArray(), header, chunks.ToArray(), image);
        }

        private static async Task<PngChunk> ReadPngChunk(Stream stream, bool ignoreCrc = false)
        {
            var crc32 = new Crc32();
            var chunkData = new MemoryStream();
            
            var lengthBytes = await stream.ReadBytes(4);
            var length = BigEndianConverter.ConvertBytesToUInt32(lengthBytes);
            chunkData.Write(lengthBytes);

            var type = await stream.ReadBytes(4);
            chunkData.Write(type);
            crc32.Compute(type);

            var data = length > 0 ? await stream.ReadBytes((int)length) : Array.Empty<byte>();
            chunkData.Write(data);
            crc32.Compute(data);
            var calculatedCrc = crc32.GetCalculatedCrc();

            var crcBytes = await stream.ReadBytes(4);
            var crc = BigEndianConverter.ConvertBytesToUInt32(crcBytes);
            chunkData.Write(crcBytes);

            var ignoreCrcCheck = ignoreCrc || type.SequenceEqual(Constants.PngChunkTypes.Icon);

            return !ignoreCrcCheck && calculatedCrc != crc
                ? throw new IOException($"Invalid CRC for PNG chunk 0x{type.FormatHex()}. Expected: {crc}, calculated: {calculatedCrc}")
                : new PngChunk(chunkData.ToArray(), length, type, data, crc);
        }

        public static PngHeader ReadPngHeader(byte[] iHdrChunk)
        {
            var width = BigEndianConverter.ConvertBytesToUInt32(iHdrChunk);
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