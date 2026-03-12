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
            var colorIconData = TestDataHelper.CreateColorIconDataWithOddSizeChunks();
            
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
    }
}