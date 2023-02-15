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
        public static async Task Write(Stream stream, ColorIcon colorIcon, bool compressImage, bool compressPalette)
        {
            if (colorIcon.Width > 256)
            {
                throw new ArgumentException(
                    $"New icon width {colorIcon.Width} is larger than max width 256",
                    nameof(colorIcon));
            }

            if (colorIcon.Height > 256)
            {
                throw new ArgumentException(
                    $"New icon height {colorIcon.Height} is larger than max height 256",
                    nameof(colorIcon));
            }

            if (colorIcon.Images.Any(x => x.Image.Width != colorIcon.Width))
            {
                throw new ArgumentException($"Color icon images doesn't match width {colorIcon.Width}",
                    nameof(colorIcon));
            }

            if (colorIcon.Images.Any(x => x.Image.Height != colorIcon.Height))
            {
                throw new ArgumentException($"Color icon images doesn't match height {colorIcon.Height}",
                    nameof(colorIcon));
            }
            
            var iconBytes = new List<byte>();

            var idBytes = BitConverter.GetBytes(ColorIconChunkIdentifiers.ICON);
            iconBytes.AddRange(idBytes);

            iconBytes.AddRange(BuildChunk(ColorIconChunkIdentifiers.FACE, BuildFaceChunk(colorIcon)));

            foreach (var colorIconImage in colorIcon.Images)
            {
                iconBytes.AddRange(BuildChunk(ColorIconChunkIdentifiers.IMAG, BuildImagChunk(colorIconImage, compressImage, compressPalette)));
            }

            await stream.WriteBytes(BuildChunk(ColorIconChunkIdentifiers.FORM, iconBytes.ToArray()));
        }

        private static byte[] BuildChunk(uint id, byte[] bytes)
        {
            var chunkBytes = new List<byte>();

            var idBytes = BitConverter.GetBytes(id);
            chunkBytes.AddRange(idBytes);

            var sizeBytes = new byte[4];
            BigEndianConverter.ConvertUInt32ToBytes((uint)bytes.Length, sizeBytes, 0);
            chunkBytes.AddRange(sizeBytes);

            chunkBytes.AddRange(bytes);
            
            return chunkBytes.ToArray();
        }

        private static byte[] BuildFaceChunk(ColorIcon colorIcon)
        {
            var chunkBytes = new byte[6];
            chunkBytes[0] = (byte)(colorIcon.Width - 1);
            chunkBytes[1] = (byte)(colorIcon.Height - 1);
            chunkBytes[2] = (byte)colorIcon.Flags;
            chunkBytes[3] = (byte)colorIcon.Aspect;
            BigEndianConverter.ConvertUInt16ToBytes((ushort)colorIcon.MaxPalBytes, chunkBytes, 4);
            return chunkBytes;
        }

        private static byte[] BuildImagChunk(ColorIconImage image, bool compressImage, bool compressPalette)
        {
            var chunkBytes = new List<byte>();
            chunkBytes.Add((byte)image.Image.Palette.TransparentColor);
            chunkBytes.Add((byte)(image.Image.Palette.Colors.Count - 1));

            var flags = 0;
            
            // add has transparent color flags bit, if image has transparent color 
            if (image.Image.Palette.IsTransparent)
            {
                flags |= 1;
            }

            // add has palette flags bit, if image palette has colors 
            if (image.Image.Palette.Colors.Count > 0)
            {
                flags |= 1 << 1;
            }

            chunkBytes.Add((byte)flags);

            chunkBytes.Add((byte)(compressImage ? 1 : 0)); // 0 = uncompressed, 1 = compressed
            chunkBytes.Add((byte)(compressPalette ? 1 : 0)); // 0 = uncompressed, 1 = compressed
            chunkBytes.Add((byte)DiskObjectHelper.CalculateDepth(image.Image.Palette.Colors.Count));

            var pixelBytes = compressImage ? BuildCompressedPixels(image) : BuildUncompressedPixels(image);
            var paletteBytes = compressPalette ? BuildCompressedPalette(image.Image.Palette) : BuildUncompressedPalette(image.Image.Palette);

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

        private static byte[] BuildUncompressedPixels(ColorIconImage colorIconImage)
        {
            var imageSize = colorIconImage.Image.Width * colorIconImage.Image.Height;
            var pixels = new byte[imageSize];
            Array.Copy(colorIconImage.Image.PixelData, 0, pixels, 0, imageSize);
            return pixels;
        }

        private static byte[] BuildCompressedPixels(ColorIconImage colorIconImage)
        {
            var stream = new MemoryStream();
            var rleWriter = new RleStreamWriter(stream, colorIconImage.Depth);
            foreach (var d in colorIconImage.Image.PixelData)
            {
                rleWriter.Write(d);
            }
            rleWriter.Finish();
            return stream.ToArray();
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

        private static byte[] BuildCompressedPalette(Palette palette)
        {
            var stream = new MemoryStream();
            var rleWriter = new RleStreamWriter(stream, 8);
            foreach (var color in palette.Colors)
            {
                rleWriter.Write((byte)color.R);
                rleWriter.Write((byte)color.G);
                rleWriter.Write((byte)color.B);
            }
            rleWriter.Finish();
            return stream.ToArray();
        }
    }
}