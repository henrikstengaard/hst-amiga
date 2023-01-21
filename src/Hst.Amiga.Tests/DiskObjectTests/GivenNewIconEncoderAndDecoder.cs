namespace Hst.Amiga.Tests.DiskObjectTests
{
    using System;
    using System.IO;
    using System.Linq;
    using DataTypes.DiskObjects;
    using Xunit;

    public class GivenNewIconEncoderAndDecoder : DiskObjectsTestBase
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
        public void WhenEncodeAndDecodeNewIconThenNewIconAndImageAreEqual(string imagePath)
        {
            // arrange - new icon image number set to 1
            var imageNumber = 1;

            // arrange - read image
            var image = Imaging.Pngcs.PngReader.Read(File.OpenRead(Path.Combine("TestData", "DiskObjects", imagePath)));

            // arrange - encode image to new icon
            var newIcon = NewIconConverter.ToNewIcon(image);

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
            for (var i = 0; i < Math.Min(newIcon.Palette.Length, decodedNewIcon.Palette.Length); i++)
            {
                Assert.Equal(newIcon.Palette[i].R, decodedNewIcon.Palette[i].R);
                Assert.Equal(newIcon.Palette[i].G, decodedNewIcon.Palette[i].G);
                Assert.Equal(newIcon.Palette[i].B, decodedNewIcon.Palette[i].B);
                // alpha channel is not used by new icons
            }

            // assert - new icon and decoded new icon image pixels are equal
            Assert.Equal(newIcon.Data.Length, decodedNewIcon.Data.Length);
            for (var i = 0; i < newIcon.Data.Length; i++)
            {
                Assert.Equal(newIcon.Data[i], decodedNewIcon.Data[i]);
            }
            
            // assert - decoded new icon is equal to image
            var decodedImage = NewIconConverter.ToImage(decodedNewIcon);
            AssertEqual(image, decodedImage);
        }
    }
}