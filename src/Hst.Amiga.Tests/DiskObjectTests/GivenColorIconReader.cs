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
            var expectedFirstImagePath = Path.Combine("TestData", "DiskObjects", "ColorIcons", "AF-OS35-Icons1-image1.png");
            var expectedFirstImage = PngReader.Read(File.OpenRead(expectedFirstImagePath));
            var expectedSecondImagePath = Path.Combine("TestData", "DiskObjects", "ColorIcons", "AF-OS35-Icons1-image2.png");
            var expectedSecondImage = PngReader.Read(File.OpenRead(expectedSecondImagePath));

            // act - read color icon
            await using var stream = File.OpenRead(Path.Combine("TestData", "DiskObjects", "ColorIcons", "AF-OS35-Icons1.readme.info"));
            await DiskObjectReader.Read(stream);
            var colorIcon = await ColorIconReader.Read(stream);

            // assert - image palette and pixels match
            var firstImage = colorIcon.Images[0];
            var secondImage = colorIcon.Images[1];
            AssertEqual(expectedFirstImage, firstImage);
            AssertEqual(expectedSecondImage, secondImage);
        }
    }
}