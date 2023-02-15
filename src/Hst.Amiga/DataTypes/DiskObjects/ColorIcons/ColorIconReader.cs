namespace Hst.Amiga.DataTypes.DiskObjects.ColorIcons
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;
    using Imaging;

    public static class ColorIconReader
    {
        public static async Task<ColorIcon> Read(Stream stream)
        {
            var formChunkIdentifier = BitConverter.ToUInt32(await stream.ReadBytes(4), 0);
            if (formChunkIdentifier != ColorIconChunkIdentifiers.FORM)
            {
                throw new IOException("Invalid form chunk identifier");
            }

            var formChunkSize = await stream.ReadBigEndianUInt32();

            var iconChunkIdentifier = BitConverter.ToUInt32(await stream.ReadBytes(4), 0);
            if (iconChunkIdentifier != ColorIconChunkIdentifiers.ICON)
            {
                throw new IOException("Invalid icon chunk identifier");
            }

            FaceChunk faceChunk = null;
            var colorIconImages = new List<ColorIconImage>();

            while (stream.Position < stream.Length)
            {
                var chunkIdentifier = BitConverter.ToUInt32(await stream.ReadBytes(4), 0);
                var chunkSize = await stream.ReadBigEndianUInt32();

                switch (chunkIdentifier)
                {
                    case ColorIconChunkIdentifiers.FACE:
                        faceChunk = await ReadFace(stream);
                        break;
                    case ColorIconChunkIdentifiers.IMAG:
                        if (faceChunk == null)
                        {
                            throw new IOException("FACE chunk not found");
                        }

                        colorIconImages.Add(await ReadImag(stream, faceChunk));
                        break;
                    default:
                        // unknown chunk, skip
                        stream.Seek(chunkSize, SeekOrigin.Current);
                        break;
                }
            }

            if (faceChunk == null)
            {
                throw new IOException("FACE chunk not found");
            }
            
            return new ColorIcon
            {
                Width = faceChunk.Width,
                Height = faceChunk.Height,
                Flags = faceChunk.Flags,
                Aspect = faceChunk.Aspect,
                MaxPalBytes = faceChunk.MaxPalBytes,
                Images = colorIconImages.ToArray()
            };
        }
        
        private static async Task<FaceChunk> ReadFace(Stream stream)
        {
            var width = stream.ReadByte();
            var height = stream.ReadByte();
            var flags = stream.ReadByte();
            var aspect = stream.ReadByte();
            var maxPalBytes = await stream.ReadBigEndianUInt16();

            return new FaceChunk
            {
                Width = width + 1,
                Height = height + 1,
                Flags = flags,
                Aspect = aspect,
                MaxPalBytes = maxPalBytes
            };
        }

        private static async Task<ColorIconImage> ReadImag(Stream stream, FaceChunk faceChunk)
        {
            var transparentColor = stream.ReadByte();
            var numColors = stream.ReadByte() + 1;
            var flags = stream.ReadByte();

            var imageCompressed = stream.ReadByte() == 1; // 0 = uncompressed, 1 = compressed
            var paletteCompressed = stream.ReadByte() == 1; // 0 = uncompressed, 1 = compressed
            var depth = stream.ReadByte();

            var imageSize = (await stream.ReadBigEndianUInt16()) + 1;
            var paletteSize = (await stream.ReadBigEndianUInt16()) + 1;

            // pad uneven to even
            if (imageSize + paletteSize % 2 != 0)
            {
                paletteSize++;
            }

            var pixels =
                (imageCompressed
                    ? ReadCompressedPixels(stream, faceChunk.Width, faceChunk.Height, depth, imageSize)
                    : ReadUncompressedPixels(stream, faceChunk.Width, faceChunk.Height, imageSize)).ToArray();

            // if palette is attached
            var colors = Array.Empty<Color>();

            var hasTransparentColor = (flags & 1) == 1;
            var hasPalette = (flags & (1 << 1)) == 1 << 1;

            if (hasPalette)
            {
                colors = (paletteCompressed
                    ? ReadCompressedPalette(stream, numColors, paletteSize, hasTransparentColor, transparentColor)
                    : ReadUncompressedPalette(stream, numColors, paletteSize, hasTransparentColor, transparentColor)).ToArray();
            }

            var palette = new Palette(colors);
            if (hasTransparentColor)
            {
                palette.TransparentColor = transparentColor;
            }

            return new ColorIconImage
            {
                Depth = depth,
                Image = new Image(faceChunk.Width, faceChunk.Height, hasPalette ? 8 : 24, palette, pixels)
            };
        }

        private static IEnumerable<Color> ReadUncompressedPalette(Stream stream, int numColors, int paletteSize,
            bool hasTransparentColor, int transparentColor)
        {
            var colors = new List<Color>();
            var position = stream.Position;
            for (var i = 0; i < numColors; i++)
            {
                var r = stream.ReadByte();
                var g = stream.ReadByte();
                var b = stream.ReadByte();
                colors.Add(new Color(r, g, b, hasTransparentColor && transparentColor == i ? 0 : 255));
            }

            stream.Seek(position + paletteSize, SeekOrigin.Begin);
            return colors;
        }

        private static IEnumerable<Color> ReadCompressedPalette(Stream stream, int numColors, int paletteSize,
            bool hasTransparentColor, int transparentColor)
        {
            var position = stream.Position;
            var reader = new RleStreamReader(stream, 8, paletteSize);
            for (var i = 0; i < numColors; i++)
            {
                var r = reader.ReadData8();
                var g = reader.ReadData8();
                var b = reader.ReadData8();
                yield return new Color(r, g, b, hasTransparentColor && transparentColor == i ? 0 : 255);
            }

            stream.Seek(position + paletteSize, SeekOrigin.Begin);
        }

        private static IEnumerable<byte> ReadCompressedPixels(Stream stream, int width, int height, int depth,
            int imageSize)
        {
            var position = stream.Position;
            var pixels = new byte[width * height];

            var reader = new RleStreamReader(stream, depth, imageSize);

            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = reader.ReadData8();
            }

            stream.Seek(position + imageSize, SeekOrigin.Begin);

            return pixels;
        }

        private static IEnumerable<byte> ReadUncompressedPixels(Stream stream, int width, int height, int imageSize)
        {
            var position = stream.Position;
            var pixels = new byte[width * height];

            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = (byte)stream.ReadByte();
            }

            stream.Seek(position + imageSize, SeekOrigin.Begin);

            return pixels;
        }
    }
}