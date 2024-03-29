﻿namespace Hst.Amiga.Tests.DiskObjectTests
{
    using DataTypes.DiskObjects;
    using Imaging;
    using Xunit;

    public class GivenImageDataDecoder : DiskObjectsTestBase
    {
        [Fact]
        public void WhenDecodeImageDataWith2DifferentColorsUsedThenRgbaPixelDataMatchesPixelSet()
        {
            // arrange - set dimension, depth, palette for image to encode
            var width = 2;
            var height = 2;
            var depth = 2;
            var palette = AmigaOsPalette.FourColors();

            // arrange - create expected pixel data
            var pixelData = TestDataHelper.CreatePixelData(width, height);

            // arrange - set pixel x = 1, y = 1 set to palette color 0
            pixelData[(width * 0) + 0] = 0;

            // arrange - set pixel x = 2, y = 1 set to palette color 0
            pixelData[(width * 0) + 1] = 0;

            // arrange - set pixel x = 1, y = 2 set to palette color 0
            pixelData[(width * 1) + 0] = 0;

            // arrange - set pixel x = 2, y = 2 set to palette color 3
            pixelData[(width * 1) + 1] = 3;

            // arrange - load image from pixel data
            var expectedImage = new Image(width, height, 8, palette, pixelData);

            // arrange - create image data
            var imageData = new ImageData
            {
                Width = (short)width,
                Height = (short)height,
                Depth = (short)depth,
                Data = TestDataHelper.CreateImageData(width, height, depth)
            };

            // arrange - set pixel x = 2, y = 2 set to palette color 3
            // note other pixels are set to 0 resulting in color 0
            TestDataHelper.SetImageDataPixel(imageData.Data, width, height, depth, 1, 1, 3);

            // act - decode image data
            var image = ImageDataDecoder.Decode(imageData, palette, false);

            // assert - image matches pixels set
            AssertEqual(expectedImage, image);
        }

        [Fact]
        public void WhenDecodeImageDataWith3DifferentColorsUsedThenRgbaPixelDataMatchesPixelSet()
        {
            // arrange - set dimension, depth, palette for image to encode
            var width = 2;
            var height = 2;
            var depth = 2;
            var palette = AmigaOsPalette.FourColors();
            var colors = palette.Colors;

            // arrange - create expected pixel data
            var pixelData = TestDataHelper.CreatePixelData(width, height);

            // arrange - set pixel x = 1, y = 1 set to palette color 0
            pixelData[(width * 0) + 0] = 0;

            // arrange - set pixel x = 2, y = 1 set to palette color 0
            pixelData[(width * 0) + 1] = 0;

            // arrange - set pixel x = 1, y = 2 set to palette color 2
            pixelData[(width * 1) + 0] = 2;

            // arrange - set pixel x = 2, y = 2 set to palette color 3
            pixelData[(width * 1) + 1] = 3;

            // arrange - load image from pixel data
            var expectedImage = new Image(width, height, 8, palette, pixelData);

            // arrange - create image data
            // note all pixels in image data are set to 0 resulting in color 0
            var imageData = new ImageData
            {
                Width = (short)width,
                Height = (short)height,
                Depth = (short)depth,
                Data = TestDataHelper.CreateImageData(width, height, depth)
            };

            // arrange - set pixel x = 1, y = 2 set to palette color 2
            TestDataHelper.SetImageDataPixel(imageData.Data, width, height, depth, 0, 1, 2);

            // arrange - set pixel x = 2, y = 2 set to palette color 3
            TestDataHelper.SetImageDataPixel(imageData.Data, width, height, depth, 1, 1, 3);

            // act - decode image data
            var image = ImageDataDecoder.Decode(imageData, palette, false);

            // assert - image matches pixels set
            AssertEqual(expectedImage, image);
        }
    }
}