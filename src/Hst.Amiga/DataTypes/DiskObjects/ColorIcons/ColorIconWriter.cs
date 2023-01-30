namespace Hst.Amiga.DataTypes.DiskObjects.ColorIcons
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Core.Converters;
    using Core.Extensions;
    using Imaging;

    public static class ColorIconWriter
    {
        public static async Task Write(Stream stream, ColorIcon colorIcon)
        {
            if (colorIcon.Width > 256 || colorIcon.Height > 256)
            {
                throw new ArgumentException(
                    $"New icon is too large: {colorIcon.Width}x{colorIcon.Height} (max is 256 wide, 256 high)",
                    nameof(colorIcon));
            }

            if (colorIcon.Images.Any(x => x.Width != colorIcon.Width))
            {
                throw new ArgumentException($"Color icon images doesn't match width {colorIcon.Width}",
                    nameof(colorIcon));
            }

            if (colorIcon.Images.Any(x => x.Height != colorIcon.Height))
            {
                throw new ArgumentException($"Color icon images doesn't match height {colorIcon.Height}",
                    nameof(colorIcon));
            }

            await stream.WriteBytes(BitConverter.GetBytes(ColorIconChunkIdentifiers.FORM));

            // face chunk size
            var faceChunkSize = 6;


            var bytes = new List<byte>();

            bytes.AddRange(BuildChunk(ColorIconChunkIdentifiers.FACE, BuildFaceChunk(colorIcon)));
            bytes.AddRange(BuildChunk(ColorIconChunkIdentifiers.IMAG, BuildFaceChunk(colorIcon)));
        }

        private static byte[] BuildChunk(uint id, byte[] bytes)
        {
            var chunkBytes = new List<byte>();
            var idBytes = new byte[8];
            BigEndianConverter.ConvertUInt32ToBytes(id, idBytes, 0);
            BigEndianConverter.ConvertUInt32ToBytes((uint)bytes.Length, idBytes, 4);
            chunkBytes.AddRange(bytes);
            return chunkBytes.ToArray();
        }

        private static byte[] BuildFaceChunk(ColorIcon colorIcon)
        {
            var chunkBytes = new byte[6];
            chunkBytes[0] = (byte)colorIcon.Width;
            chunkBytes[1] = (byte)colorIcon.Height;
            chunkBytes[2] = (byte)colorIcon.Flags;
            chunkBytes[3] = (byte)colorIcon.Aspect;
            BigEndianConverter.ConvertUInt16ToBytes((ushort)colorIcon.MaxPalBytes, chunkBytes, 4);
            return chunkBytes;
        }

        private static byte[] BuildImagChunk(Image image, bool compressImage, bool compressPalette)
        {
            var chunkBytes = new List<byte>();
            chunkBytes.Add((byte)image.Palette.TransparentColor);
            chunkBytes.Add((byte)(image.Palette.Colors.Count - 1));

            var flags = 0;
            
            // add has transparent color flags bit, if image has transparent color 
            if (image.Palette.IsTransparent)
            {
                flags |= 1;
            }

            // add has palette flags bit, if image palette has colors 
            if (image.Palette.Colors.Count > 0)
            {
                flags |= 1 << 1;
            }

            chunkBytes.Add((byte)flags);

            chunkBytes.Add((byte)(compressImage ? 0 : 1)); // 0 = uncompressed, 1 = compressed
            chunkBytes.Add((byte)(compressPalette ? 0 : 1)); // 0 = uncompressed, 1 = compressed
            chunkBytes.Add((byte)DiskObjectHelper.CalculateDepth(image.Palette.Colors.Count));

            var pixelBytes = BuildUncompressedPixels(image);
            var paletteBytes = BuildUncompressedPalette(image.Palette);

            var imageSizeBytes = new byte[2];
            BigEndianConverter.ConvertUInt16ToBytes((ushort)(pixelBytes.Length - 1), imageSizeBytes, 0);
            var paletteSizeBytes = new byte[2];
            BigEndianConverter.ConvertUInt16ToBytes((ushort)(paletteBytes.Length - 1), paletteSizeBytes, 0);

            chunkBytes.AddRange(imageSizeBytes);
            chunkBytes.AddRange(paletteSizeBytes);

            chunkBytes.AddRange(pixelBytes);
            chunkBytes.AddRange(paletteBytes);

            // pad uneven to even
            if (pixelBytes.Length - 1 + paletteBytes.Length - 1 % 2 != 0)
            {
                chunkBytes.Add(0);
            }

            return chunkBytes.ToArray();
        }

        private static byte[] BuildUncompressedPixels(Image image)
        {
            var imageSize = image.Width * image.Height;
            var pixels = new byte[imageSize];
            Array.Copy(image.PixelData, 0, pixels, 0, imageSize);
            return pixels;
        }

        private static byte[] BuildUncompressedPalette(Palette palette)
        {
            var bytes = new List<byte>();
            foreach (var color in palette.Colors)
            {
                bytes.Add((byte)color.R);
                bytes.Add((byte)color.G);
                bytes.Add((byte)color.B);
            }

            return bytes.ToArray();
        }
    }
}