namespace Hst.Amiga.Tests.DiskObjectTests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using DataTypes.DiskObjects;
    using Xunit;

    public class GivenNewIconEncoderAndDecoder : InfoTestBase
    {
        [Theory]
        [InlineData(@"Floppy.png")]
        [InlineData(@"Puzzle-Bubble-60-colors.png")]
        [InlineData(@"Puzzle-Bubble-100-colors.png")]
        [InlineData(@"Puzzle-Bubble-127-colors.png")]
        [InlineData(@"Puzzle-Bubble-128-colors.png")]
        [InlineData(@"Puzzle-Bubble-129-colors.png")]
        [InlineData(@"Puzzle-Bubble-150-colors.png")]
        [InlineData(@"Puzzle-Bubble-255-colors.png")]
        public async Task WhenEncodeAndDecodeNewIconThenNewIconAndImageAreEqual(string imagePath)
        {
            // arrange - new icon image number set to 1
            var imageNumber = 1;

            // arrange - read image
            var image = Imaging.Pngcs.PngReader.Read(File.OpenRead(Path.Combine("TestData", "DiskObjects", imagePath)));

            // arrange - encode image to new icon
            var newIcon = NewIconEncoder.Encode(image);

            // act - encode new icon to tool types text datas
            var textDatas = NewIconToolTypesEncoder.Encode(imageNumber, newIcon).ToList();

            // act - create decoder to decode tool types text datas
            var decoder = new NewIconToolTypesDecoder(textDatas);
            
            // act - decode tool types text datas to new icon
            var decodedNewIcon = decoder.Decode(imageNumber);

            // assert - new icon and decoded new icon width, height, depth and transparency are equal
            Assert.Equal(newIcon.Width, decodedNewIcon.Width);
            Assert.Equal(newIcon.Height, decodedNewIcon.Height);
            Assert.Equal(newIcon.Depth, decodedNewIcon.Depth);
            Assert.Equal(newIcon.Transparent, decodedNewIcon.Transparent);

            // assert - new icon and decoded new icon are equal
            for (var i = 0; i < Math.Min(newIcon.Image.Palette.Colors.Count, decodedNewIcon.Image.Palette.Colors.Count); i++)
            {
                Assert.Equal(newIcon.Image.Palette.Colors[i].R, decodedNewIcon.Image.Palette.Colors[i].R);
                Assert.Equal(newIcon.Image.Palette.Colors[i].G, decodedNewIcon.Image.Palette.Colors[i].G);
                Assert.Equal(newIcon.Image.Palette.Colors[i].B, decodedNewIcon.Image.Palette.Colors[i].B);
                // alpha channel is not used by new icons
            }

            // assert - new icon and decoded new icon image pixels are equal
            Assert.Equal(newIcon.Image.PixelData.Length, decodedNewIcon.Image.PixelData.Length);
            for (var i = 0; i < newIcon.Image.PixelData.Length; i++)
            {
                Assert.Equal(newIcon.Image.PixelData[i], decodedNewIcon.Image.PixelData[i]);
            }
            
            // assert - decoded new icon is equal to image
            AssertEqual(image, decodedNewIcon.Image);
        }
    }
}