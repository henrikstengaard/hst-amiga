namespace Hst.Amiga.Tests.DiskObjectTests
{
    using System.IO;
    using System.Linq;
    using DataTypes.DiskObjects.NewIcons;
    using Imaging;
    using Imaging.Pngcs;
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
            var image = PngReader.Read(File.OpenRead(Path.Combine("TestData", "DiskObjects", imagePath)));

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
            Assert.Equal(newIcon.Palette.Length, decodedNewIcon.Palette.Length);
            for (var i = 0; i < newIcon.Palette.Length; i++)
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

        [Fact]
        public void When8X8ImageWith4ColorsIsEncodedAndDecodedThenImageDataIsEqual()
        {
            // arrange - image number and new icon
            const int imageNumber = 1;
            const int width = 8;
            const int height = 8;
            const int depth = 2;
            var newIcon = new NewIcon
            {
                Width = width,
                Height = height,
                Depth = depth,
                Palette = new []
                {
                    new Color(170, 170, 170),
                    new Color(0, 0, 0),
                    new Color(255, 255, 255),
                    new Color(102, 136, 187),
                },
                Data = new byte[]
                {
                    0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1,
                    2, 2, 2, 2, 3, 3, 3, 3,
                    2, 2, 2, 2, 3, 3, 3, 3,
                    2, 2, 2, 2, 3, 3, 3, 3,
                    2, 2, 2, 2, 3, 3, 3, 3
                }
            };

            // act - encode new icon to tool types
            var textDatas = NewIconToolTypesEncoder.Encode(imageNumber, newIcon).ToList();

            var decoder = new NewIconToolTypesDecoder(textDatas);
            var image = decoder.Decode(imageNumber);
            
            Assert.Equal(newIcon.Data.Length, image.Data.Length);
            Assert.Equal(newIcon.Data, image.Data);
        }
        
        [Fact]
        public void When48X6ImageWith8ColorsIsEncodedAndDecodedThenImageDataIsEqual()
        {
            // arrange - image number and new icon
            const int imageNumber = 1;
            const int width = 48;
            const int height = 6;
            const int depth = 3;
            var newIcon = new NewIcon
            {
                Width = width,
                Height = height,
                Depth = depth,
                Palette = new []
                {
                    new Color(170, 170, 170),
                    new Color(0, 0, 0),
                    new Color(255, 255, 255),
                    new Color(102, 136, 187),
                    new Color(170, 170, 170),
                    new Color(0, 0, 0),
                    new Color(255, 255, 255),
                    new Color(102, 136, 187)
                },
                Data = new byte[]
                {
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    2, 2, 2, 6, 7, 3, 3, 3, 2, 2, 2, 6, 7, 3, 3, 3, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    2, 2, 2, 2, 3, 3, 3, 3, 2, 2, 2, 2, 3, 3, 3, 3,0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1
                }
            };

            // act - encode new icon to tool types
            var textDatas = NewIconToolTypesEncoder.Encode(imageNumber, newIcon).ToList();

            var decoder = new NewIconToolTypesDecoder(textDatas);
            var image = decoder.Decode(imageNumber);

            Assert.Equal(newIcon.Palette.Length, image.Palette.Length);
            for (var i = 0; i < image.Palette.Length; i++)
            {
                Assert.Equal(newIcon.Palette[i].R, image.Palette[i].R);
                Assert.Equal(newIcon.Palette[i].G, image.Palette[i].G);
                Assert.Equal(newIcon.Palette[i].B, image.Palette[i].B);
            }
            
            Assert.Equal(newIcon.Data.Length, image.Data.Length);
            Assert.Equal(newIcon.Data, image.Data);
        }

        [Fact]
        public void When48X24ImageWith8ColorsIsEncodedAndDecodedThenImageDataIsEqual()
        {
            // arrange - image number and new icon
            const int imageNumber = 1;
            const int width = 48;
            const int height = 24;
            const int depth = 3;
            var newIcon = new NewIcon
            {
                Width = width,
                Height = height,
                Depth = depth,
                Palette = new []
                {
                    new Color(170, 170, 170),
                    new Color(0, 0, 0),
                    new Color(255, 255, 255),
                    new Color(102, 136, 187),
                    new Color(170, 170, 170),
                    new Color(0, 0, 0),
                    new Color(255, 255, 255),
                    new Color(102, 136, 187)
                },
                Data = new byte[]
                {
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    2, 2, 2, 6, 7, 3, 3, 3, 2, 2, 2, 6, 7, 3, 3, 3, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    2, 2, 2, 2, 3, 3, 3, 3, 2, 2, 2, 2, 3, 3, 3, 3,0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    2, 2, 2, 6, 7, 3, 3, 3, 2, 2, 2, 6, 7, 3, 3, 3, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    2, 2, 2, 2, 3, 3, 3, 3, 2, 2, 2, 2, 3, 3, 3, 3,0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    2, 2, 2, 6, 7, 3, 3, 3, 2, 2, 2, 6, 7, 3, 3, 3, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    2, 2, 2, 2, 3, 3, 3, 3, 2, 2, 2, 2, 3, 3, 3, 3,0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    2, 2, 2, 6, 7, 3, 3, 3, 2, 2, 2, 6, 7, 3, 3, 3, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                    2, 2, 2, 2, 3, 3, 3, 3, 2, 2, 2, 2, 3, 3, 3, 3,0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
                }
            };

            // act - encode new icon to tool types
            var textDatas = NewIconToolTypesEncoder.Encode(imageNumber, newIcon).ToList();

            var decoder = new NewIconToolTypesDecoder(textDatas);
            var image = decoder.Decode(imageNumber);

            for (var i = 0; i < image.Palette.Length; i++)
            {
                Assert.Equal(newIcon.Palette[i].R, image.Palette[i].R);
                Assert.Equal(newIcon.Palette[i].G, image.Palette[i].G);
                Assert.Equal(newIcon.Palette[i].B, image.Palette[i].B);
            }
            
            Assert.Equal(newIcon.Data.Length, image.Data.Length);
            Assert.Equal(newIcon.Data, image.Data);
        }
        
        [Fact]
        public void When53X23ImageWith8ColorsIsEncodedAndDecodedThenImageDataIsEqual()
        {
            // arrange - image number and new icon
            const int imageNumber = 1;
            const int width = 53;
            const int height = 23;
            const int depth = 3;
            var newIcon = new NewIcon
            {
                Width = width,
                Height = height,
                Depth = depth,
                Palette = new []
                {
                    new Color(170, 170, 170),
                    new Color(0, 0, 0),
                    new Color(255, 255, 255),
                    new Color(102, 136, 187),
                    new Color(170, 170, 170),
                    new Color(0, 0, 0),
                    new Color(255, 255, 255),
                    new Color(102, 136, 187)
                },
                Data = new byte[]
                {
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    2, 2, 2, 6, 7, 3, 3, 3, 2, 2, 2, 6, 7, 3, 3, 3, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    2, 2, 2, 2, 3, 3, 3, 3, 2, 2, 2, 2, 3, 3, 3, 3,0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    2, 2, 2, 6, 7, 3, 3, 3, 2, 2, 2, 6, 7, 3, 3, 3, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    2, 2, 2, 2, 3, 3, 3, 3, 2, 2, 2, 2, 3, 3, 3, 3,0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    2, 2, 2, 6, 7, 3, 3, 3, 2, 2, 2, 6, 7, 3, 3, 3, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    2, 2, 2, 2, 3, 3, 3, 3, 2, 2, 2, 2, 3, 3, 3, 3,0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 4, 5, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1,
                    2, 2, 2, 6, 7, 3, 3, 3, 2, 2, 2, 6, 7, 3, 3, 3, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1
                }
            };

            // act - encode new icon to tool types
            var textDatas = NewIconToolTypesEncoder.Encode(imageNumber, newIcon).ToList();

            var decoder = new NewIconToolTypesDecoder(textDatas);
            var image = decoder.Decode(imageNumber);

            for (var i = 0; i < image.Palette.Length; i++)
            {
                Assert.Equal(newIcon.Palette[i].R, image.Palette[i].R);
                Assert.Equal(newIcon.Palette[i].G, image.Palette[i].G);
                Assert.Equal(newIcon.Palette[i].B, image.Palette[i].B);
            }
            
            Assert.Equal(newIcon.Data.Length, image.Data.Length);
            Assert.Equal(newIcon.Data, image.Data);
        }
    }
}