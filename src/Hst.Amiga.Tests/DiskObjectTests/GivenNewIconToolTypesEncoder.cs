namespace Hst.Amiga.Tests.DiskObjectTests
{
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using DataTypes.DiskObjects;
    using DataTypes.DiskObjects.NewIcons;
    using Imaging;
    using Xunit;

    public class GivenNewIconToolTypesEncoder
    {
        [Fact]
        public void WhenEncodeNewIconSized2X2PixelsWith2ColorsThenToolTypesMatchExpectedToolTypes()
        {
            // arrange - create expected text datas
            var expectedTextDatas = NewIconSized2X2PixelsWith2ColorsTestHelper.CreateTextDatas().ToList();

            // act - encode palette, image pixels and get tool types
            var textDatas = NewIconToolTypesEncoder.Encode(
                NewIconSized2X2PixelsWith2ColorsTestHelper.ImageNumber,
                NewIconSized2X2PixelsWith2ColorsTestHelper.NewIcon).ToList();

            // assert - text datas are equal 
            Assert.Equal(expectedTextDatas.Count, textDatas.Count);
            for (var i = 0; i < expectedTextDatas.Count; i++)
            {
                Assert.Equal(expectedTextDatas[i].Size, textDatas[i].Size);
                Assert.Equal(expectedTextDatas[i].Data, textDatas[i].Data);
            }
        }
        
        [Fact]
        public async Task WhenEncodeNewIconToToolTypesThenToolTypesMatchIConverterCreatedToolTypes()
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

            // arrange - load iconverter created newicon info
            await using var stream = File.OpenRead(Path.Combine("TestData", "DiskObjects",
                "Drawer-NewIcon-IConverter.info"));
            var diskObject = await DiskObjectReader.Read(stream);

            // arrange - get image number 1 tool types
            var expectedToolTypes =
                diskObject.ToolTypes.TextDatas.Where(
                    x => x.Size >= 4 && Encoding.ASCII.GetString(x.Data, 0, 4) == $"IM{imageNumber}=").ToList();

            // act - encode new icon to tool types
            var toolTypes = NewIconToolTypesEncoder.Encode(imageNumber, newIcon).ToList();
            
            // assert - encoded tool types are equal to iconverter tool types 
            Assert.Equal(expectedToolTypes.Count, toolTypes.Count);
            for (var i = 0; i < expectedToolTypes.Count; i++)
            {
                Assert.Equal(expectedToolTypes[i].Size, toolTypes[i].Size);
                Assert.Equal(expectedToolTypes[i].Data, toolTypes[i].Data);
            }
        }
    }
}