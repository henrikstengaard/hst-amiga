namespace Hst.Amiga.Tests.DiskObjectTests
{
    using System.IO;
    using System.Threading.Tasks;
    using DataTypes.DiskObjects;
    using DataTypes.DiskObjects.NewIcons;
    using Imaging.Bitmaps;
    using Xunit;

    public class GivenNewIconToolTypesDecoder : DiskObjectsTestBase
    {
        [Fact]
        public async Task WhenDecodingNewIconForFirstImageThenImageMatches()
        {
            // arrange - paths
            var newIconPath = Path.Combine("TestData", "DiskObjects", "Flashback.newicon");
            var expectedBitmapImagePath = Path.Combine("TestData", "DiskObjects", "Flashback-image1.bmp");
            var imageNumber = 1;

            // arrange - read bitmap image
            await using var imageStream = File.OpenRead(expectedBitmapImagePath);
            var image = BitmapReader.Read(imageStream);

            // arrange - read disk object with new icon
            var diskObject = await DiskObjectReader.Read(File.OpenRead(newIconPath));

            // act - create new icon tool types decoder
            var decoder = new NewIconToolTypesDecoder(diskObject.ToolTypes.TextDatas);

            // act - decode image number 1
            var decodedNewIcon = decoder.Decode(imageNumber);

            // assert - bitmap image is equal to new icon image
            var decodedImage = NewIconConverter.ToImage(decodedNewIcon);
            AssertEqual(image, decodedImage);
        }

        [Fact]
        public async Task WhenDecodingNewIconForSecondImageThenImageMatches()
        {
            // arrange - paths
            var newIconPath = Path.Combine("TestData", "DiskObjects", "Flashback.newicon");
            var expectedBitmapImagePath = Path.Combine("TestData", "DiskObjects", "Flashback-image2.bmp");
            var imageNumber = 2;

            // arrange - read bitmap image
            await using var imageStream = File.OpenRead(expectedBitmapImagePath);
            var image = BitmapReader.Read(imageStream);

            // arrange - read disk object with new icon
            var diskObject = await DiskObjectReader.Read(File.OpenRead(newIconPath));

            // act - create new icon tool types decoder
            var decoder = new NewIconToolTypesDecoder(diskObject.ToolTypes.TextDatas);

            // act - decode image number 2
            var decodedNewIcon = decoder.Decode(imageNumber);

            // assert - bitmap image is equal to new icon image
            var decodedImage = NewIconConverter.ToImage(decodedNewIcon);
            AssertEqual(image, decodedImage);
        }
    }
}