﻿namespace Hst.Amiga.Tests.DiskObjectTests
{
    using System.IO;
    using System.Threading.Tasks;
    using DataTypes.DiskObjects;
    using Imaging.Bitmaps;
    using Xunit;

    public class GivenNewIconToolTypesDecoder : InfoTestBase
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
            var bitmapImage = BitmapReader.Read(imageStream);

            // arrange - read disk object with new icon
            var diskObject = await DiskObjectReader.Read(File.OpenRead(newIconPath));

            // act - create new icon tool types decoder
            var decoder = new NewIconToolTypesDecoder(diskObject.ToolTypes.TextDatas);

            // act - decode image number 1
            var newIcon = decoder.Decode(imageNumber);

            // assert - bitmap image is equal to new icon image
            AssertEqual(bitmapImage, newIcon.Image);
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
            var bitmapImage = BitmapReader.Read(imageStream);

            // arrange - read disk object with new icon
            var diskObject = await DiskObjectReader.Read(File.OpenRead(newIconPath));

            // act - create new icon tool types decoder
            var decoder = new NewIconToolTypesDecoder(diskObject.ToolTypes.TextDatas);

            // act - decode image number 2
            var newIcon = decoder.Decode(imageNumber);

            // assert - bitmap image is equal to new icon image
            AssertEqual(bitmapImage, newIcon.Image);
        }
    }
}