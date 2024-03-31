using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.Iffs;
using Hst.Core.Converters;
using Hst.Imaging;

namespace Hst.Amiga.DataTypes.InterleavedBitmaps
{
    public class InterleavedBitmapWriter
    {
        private readonly IffWriter iffWriter;

        public InterleavedBitmapWriter(Stream stream)
        {
            this.iffWriter = new IffWriter(stream);
        }

        /// <summary>
        /// Write image to stream
        /// </summary>
        /// <param name="image">Image to write</param>
        /// <param name="compress">Compress image</param>
        public async Task Write(Image image, bool compress = true)
        {
            if (!(image.BitsPerPixel == 4 ||
                  image.BitsPerPixel == 8))
            {
                throw new ArgumentException(
                    $"Image with '{image.BitsPerPixel}' bpp is not supported. Only 4-bpp or 8-bpp indexed images are supported!");
            }
            
            await BuildInterleavedBitmapChunk(image, compress);
        }
        
        /// <summary>
        /// Build interleaved bitmap chunk
        /// </summary>
        /// <param name="image"></param>
        /// <param name="compress"></param>
        private async Task BuildInterleavedBitmapChunk(Image image, bool compress)
        {
            var chunk = iffWriter.BeginChunk(ChunkIdentifiers.Form);
            chunk.AddData(Encoding.ASCII.GetBytes(ChunkIdentifiers.InterleavedBitmap));

            await BuildBitmapHeaderChunk(image, compress);
            await BuildColorMapChunk(image);
            await BuildBodyChunk(image, compress);

            await iffWriter.EndChunk();
        }
        
        /// <summary>
        /// Build bitmap header chunk
        /// </summary>
        /// <param name="image"></param>
        /// <param name="compress"></param>
        /// <returns></returns>
        private async Task BuildBitmapHeaderChunk(Image image, bool compress)
        {
            var chunk = iffWriter.BeginChunk(ChunkIdentifiers.BitmapHeader);
            
            const short x = 0;
            const short y = 0;

            chunk.AddData(BigEndianConverter.ConvertUInt16ToBytes((ushort)image.Width)); // width
            chunk.AddData(BigEndianConverter.ConvertUInt16ToBytes((ushort)image.Height)); // height 
            chunk.AddData(BigEndianConverter.ConvertInt16ToBytes(x)); // x
            chunk.AddData(BigEndianConverter.ConvertInt16ToBytes(y)); // y
            chunk.AddData((byte)image.BitsPerPixel); // planes
            chunk.AddData(0); // mask
            chunk.AddData((byte)(compress ? 1 : 0)); // tcomp
            chunk.AddData(0); // pad1
            chunk.AddData(BigEndianConverter.ConvertUInt16ToBytes((ushort)image.Palette.TransparentColor)); // transparent color
            chunk.AddData(60); // xAspect
            chunk.AddData(60); // yAspect
            chunk.AddData(BigEndianConverter.ConvertUInt16ToBytes((ushort)image.Width)); // Lpage
            chunk.AddData(BigEndianConverter.ConvertUInt16ToBytes((ushort)image.Height)); // Hpage

            await iffWriter.EndChunk();
        }
        
        public static int CalculateDepth(int colors)
        {
            return colors > 1 ? Convert.ToInt32(Math.Ceiling(Math.Log(colors) / Math.Log(2))) : 1;
        }

        /// <summary>
        /// Build color map chunk storing color information for the image data
        /// </summary>
        /// <param name="image"></param>
        /// <param name="depth"></param>
        /// <returns></returns>
        private async Task BuildColorMapChunk(Image image)
        {
            var chunk = iffWriter.BeginChunk(ChunkIdentifiers.ColorMap);

            foreach (var color in image.Palette.Colors)
            {
                if (image.BitsPerPixel == 8)
                {
                    chunk.AddData((byte)color.R);
                    chunk.AddData((byte)color.G);
                    chunk.AddData((byte)color.B);
                }
                else
                {
                    chunk.AddData((byte)((color.R & 0xf0) | (color.R >> image.BitsPerPixel)));
                    chunk.AddData((byte)((color.G & 0xf0) | (color.G >> image.BitsPerPixel)));
                    chunk.AddData((byte)((color.B & 0xf0) | (color.B >> image.BitsPerPixel)));
                }
            }

            await iffWriter.EndChunk();
        }

        // create camg chunk
        public async Task CamgChunk(Image image)
        {
            var chunk = iffWriter.BeginChunk(ChunkIdentifiers.Camg);

            chunk.AddData(BigEndianConverter.ConvertUInt32ToBytes((uint)image.BitsPerPixel));

            await iffWriter.EndChunk();
            /*
            return ,$cmagStream.ToArray()

# if mode is not None:
        # camg = iff_chunk("CAMG", struct.pack(">L", mode))
# else:
# camg = ""
                # //    uint viewmodes = input.ReadBEUInt32();

                # //    bytesloaded = size;
                # //    if ((viewmodes & 0x0800) > 0)
                # //        flagHAM = true;
                # //    if ((viewmodes & 0x0080) > 0)
                # //        flagEHB = true;
                # //}
*/
        }

        public static int FindNextDuplicate(byte[] bytes, int start)
        {
            // int last = -1;
            if (start >= bytes.Length)
            {
                return -1;
            }

            var prev = bytes[start];

            for (var i = start + 1; i < bytes.Length; i++)
            {
                var b = bytes[i];

                if (b == prev)
                {
                    return i - 1;
                }

                prev = b;
            }

            return -1;
        }

        public static int FindRunLength(byte[] bytes, int start)
        {
            var b = bytes[start];

            var i = 0;

            for (i = start + 1; (i < bytes.Length) && (bytes[i] == b); i++)
            {
                // do nothing
            }

            return i - start;
        }

        public static byte[] Compress(byte[] bytes)
        {
            var baos = new MemoryStream();
            // max length 1 extra byte for every 128
            var ptr = 0;
            while (ptr < bytes.Length)
            {
                var dup = FindNextDuplicate(bytes, ptr);

                if (dup == ptr)
                {
                    // write run length
                    var len = FindRunLength(bytes, dup);
                    var actualLen = Math.Min(len, 128);
                    baos.WriteByte((byte)(256 - (actualLen - 1)));
                    baos.WriteByte(bytes[ptr]);
                    ptr += actualLen;
                }
                else
                {
                    // write literals
                    var len = dup - ptr;

                    if (dup < 0)
                    {
                        len = bytes.Length - ptr;
                    }

                    var actualLen = Math.Min(len, 128);
                    baos.WriteByte((byte)(actualLen - 1));
                    for (var i = 0; i < actualLen; i++)
                    {
                        baos.WriteByte(bytes[ptr]);
                        ptr++;
                    }
                }
            }

            return baos.ToArray();
        }

        public static int GetPaletteIndex(byte[] imageBytes, int stride, int height, int depth, int x, int y)
        {
            var offset = y;
            if (stride < 0)
            {
                offset = y - height + 1;
            }

            var biti = (offset * stride * 8) + (x * depth);

            // get the byte index
            var i = Convert.ToInt32(Math.Floor((double)biti / 8));

            var c = 0;
            if (depth == 8)
            {
                c = imageBytes[i];
            }

            if (depth == 4)
            {
                if (biti % 8 == 0)
                {
                    c = imageBytes[i] >> 4;
                }
                else
                {
                    c = imageBytes[i] & 0x0F;
                }
            }

            if (depth == 1)
            {
                var bbi = biti % 8;
                var mask = bbi << 1;
                c = (imageBytes[i] & mask) == 0 ? 1 : 0;
            }

            return c;
        }

        public static int CalculateBpr(int width)
        {
            var planeWidth = Math.Floor(((double)width + 15) / 16) * 16;
            return Convert.ToInt32(Math.Floor(planeWidth / 8));
        }

        /// <summary>
        /// Convert image to planes
        /// </summary>
        /// <param name="image"></param>
        /// <param name="bpr"></param>
        /// <returns></returns>
        public static IEnumerable<byte[]> ConvertPlanar(Image image, int bpr)
        {
            // var rect = Rectangle.FromLTRB(0, 0, image.Width, image.Height);
            // var imageData = image.LockBits(rect, ImageLockMode.ReadOnly, image.PixelFormat);
            // var dataPointer = imageData.Scan0;
            //
            // var totalBytes = imageData.Stride * image.Height;

            // var imageBytes = new byte[totalBytes];
            // Marshal.Copy(dataPointer, imageBytes, 0, totalBytes);
            // image.UnlockBits(imageData);

            // Calculate dimensions.
            var planeSize = Convert.ToInt32(bpr * image.Height);

            var planes = new List<byte[]>();

            for (var plane = 0; plane < image.BitsPerPixel; plane++)
            {
                planes.Add(new byte[planeSize]);
            }

            for (var y = 0; y < image.Height; y++)
            {
                var rowOffset = y * bpr;
                for (var x = 0; x < image.Width; x++)
                {
                    var offset = Convert.ToInt32(rowOffset + Math.Floor((double)x / 8));
                    var xmod = 7 - (x & 7);

                    var paletteIndex = image.PixelData[(y * image.Width) + x];

                    for (var plane = 0; plane < image.BitsPerPixel; plane++)
                    {
                        planes[plane][offset] = (byte)(planes[plane][offset] | (((paletteIndex >> plane) & 1) << xmod));
                    }
                }
            }

            return planes;
        }

        /// <summary>
        /// Build body chunk storing the actual image data as a byte array
        /// </summary>
        /// <param name="image"></param>
        /// <param name="compress"></param>
        /// <returns></returns>
        public async Task BuildBodyChunk(Image image, bool compress)
        {
            var chunk = iffWriter.BeginChunk(ChunkIdentifiers.Body);
            
            // Get planar bitmap.
            var bpr = CalculateBpr(image.Width);
            var planes = ConvertPlanar(image, bpr).ToList();

            for (var y = 0; y < image.Height; y++)
            {
                for (var plane = 0; plane < image.BitsPerPixel; plane++)
                {
                    var row = new byte[bpr];
                    Array.Copy(planes[plane], y * bpr, row, 0, bpr);

                    if (compress)
                    {
                        row = Compress(row);
                    }

                    chunk.AddData(row);
                }
            }

            await iffWriter.EndChunk();
        }
    }
}