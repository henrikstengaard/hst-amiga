using System;
using System.Linq;

namespace Hst.Amiga.Tests.DiskObjectTests
{
    using System.IO;
    using System.Threading.Tasks;
    using DataTypes.DiskObjects;
    using DataTypes.DiskObjects.ColorIcons;
    using Imaging.Pngcs;
    using Xunit;

    public class GivenColorIconReader : DiskObjectsTestBase
    {
        [Fact]
        public async Task WhenReadColorIconThenImagesMatch()
        {
            // arrange - read expected first and second image
            var expectedFirstImagePath =
                Path.Combine("TestData", "DiskObjects", "ColorIcons", "AF-OS35-Icons1-image1.png");
            var expectedFirstImage = PngReader.Read(File.OpenRead(expectedFirstImagePath));
            var expectedSecondImagePath =
                Path.Combine("TestData", "DiskObjects", "ColorIcons", "AF-OS35-Icons1-image2.png");
            var expectedSecondImage = PngReader.Read(File.OpenRead(expectedSecondImagePath));

            // act - read color icon
            await using var stream =
                File.OpenRead(Path.Combine("TestData", "DiskObjects", "ColorIcons", "AF-OS35-Icons1.readme.info"));
            await DiskObjectReader.Read(stream);
            var colorIcon = await ColorIconReader.Read(stream);

            // assert - image palette and pixels match
            var firstImage = colorIcon.Images[0];
            var secondImage = colorIcon.Images[1];
            AssertEqual(expectedFirstImage, firstImage.Image);
            AssertEqual(expectedSecondImage, secondImage.Image);
        }

        [Fact]
        public async Task When_ReadingColorIconWithOddSizeChunks_Then_ColorIconIsRead()
        {
            // arrange - create color icon data with zero padded chunks
            var colorIconData = CreateColorIconDataWithOddSizeChunks();
            
            // arrange - create stream from color icon data
            await using var stream = new MemoryStream(colorIconData);
            
            // act - read color icon
            var colorIcon = await ColorIconReader.Read(stream);
            
            // assert - image palette and pixels match
            Assert.Equal(1, colorIcon.Width);
            Assert.Equal(1, colorIcon.Height);
            Assert.Equal(0, colorIcon.Flags);
            Assert.Equal(0, colorIcon.Aspect);
            Assert.Equal(3, colorIcon.MaxPalBytes);
            
            // assert - image palette and pixels match
            Assert.Single(colorIcon.Images);
            var colorIconImage = colorIcon.Images[0];
            Assert.Equal(1, colorIconImage.Depth);
            Assert.Equal(1, colorIconImage.Image.Width);
            Assert.Equal(1, colorIconImage.Image.Height);

            // assert - palette contains one color with RGB values (200, 210, 220)
            Assert.Single(colorIconImage.Image.Palette.Colors);
            Assert.Equal(200, colorIconImage.Image.Palette.Colors[0].R);
            Assert.Equal(210, colorIconImage.Image.Palette.Colors[0].G);
            Assert.Equal(220, colorIconImage.Image.Palette.Colors[0].B);
        }
        
        /// <summary>
        /// Creates color icon data with zero padded chunks to test that the reader can handle odd sized chunks.
        /// The color icon contains one image of 1x1 pixel with a palette of 2 colors (1 bit depth).
        /// 1x1 pixel results in a odd size "IMAG" chunk, which is padded with a zero byte.
        /// </summary>
        /// <returns>Byte array containing color icon.</returns>
        private static byte[] CreateColorIconDataWithOddSizeChunks()
        {
            const byte width = 0; // 0 = 1 pixel
            const byte height = 0; // 0 = 1 pixel
            const byte faceFlags = 0;
            const byte aspect = 0;
            const byte maxPalBytes = 2;
            
            var faceData = new byte[] { width, height, faceFlags, aspect, 0x00, maxPalBytes };
            var faceChunk = MakeChunk("FACE", faceData);

            const byte transparentColor = 0;
            const byte numColors = 0; // 0 = 1 color
            const byte imagFlags = 2; // 2 = has palette
            const byte imageCompressed = 0; // 0 = uncompressed
            const byte paletteCompressed = 0; // 0 = uncompressed
            const byte depth = 1; // 1 = 2 colors, bits 0, 1
            const byte imageSize = 0; // 0 = 1 byte, ushort = 0x00, 0x00
            const byte paletteSize = 2; // 2 = 3 bytes, ushort = 0x00, 0x02
            const byte pixelData = 0; // 1 pixel with color index 0
            const byte color1R = 200;
            const byte color1G = 210;
            const byte color1B = 220;
            
            var imagData = new byte[] { transparentColor, numColors, imagFlags, imageCompressed,
                paletteCompressed, depth, 0, imageSize, 0, paletteSize, pixelData, color1R, color1G, color1B };
            var imagChunk = MakeChunk("IMAG", imagData);
            
            var iconChunk = MakeId("ICON").Concat(faceChunk).Concat(imagChunk).ToArray();
            var formChunk = MakeChunk("FORM", iconChunk);

            return formChunk;
        }

        private static byte[] MakeId(string chunkId) => System.Text.Encoding.ASCII.GetBytes(chunkId);

        private static byte[] MakeChunk(string chunkId, byte[] data)
        {
            var chunkIdBytes = MakeId(chunkId);
            var paddingByte = (data.Length % 2 == 0) ? Array.Empty<byte>() : new byte[] { 0 };
            var chunkSize = data.Length + paddingByte.Length;
            var chunkSizeBytes = new[]
            {
                (byte)((chunkSize >> 24) & 0xFF),
                (byte)((chunkSize >> 16) & 0xFF),
                (byte)((chunkSize >> 8) & 0xFF),
                (byte)(chunkSize & 0xFF)
            };
            return chunkIdBytes.Concat(chunkSizeBytes).Concat(data).Concat(paddingByte).ToArray();
        }
    }
}