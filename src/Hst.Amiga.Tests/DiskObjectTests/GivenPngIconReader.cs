using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.DiskObjects.PngIcons;
using Xunit;

namespace Hst.Amiga.Tests.DiskObjectTests;

public class GivenPngIconReader
{
    [Fact]
    public async Task When_ReadingPngIcon_Then_PngIconsAreRead()
    {
        // arrange - read png bytes from file
        var pngBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "DiskObjects", "AmigaPngIcon.info"));
            
        // arrange - create memory stream from png bytes
        var pngStream = new MemoryStream(pngBytes);

        // act - read png icons
        var pngIcons = (await PngIconReader.Read(pngStream)).ToList();
        
        // assert - 2 png icons are read
        Assert.Equal(2, pngIcons.Count);
        
        // assert - png icon 1 contain expected chunks and chunk types
        var pngIcon1 = pngIcons[0];
        Assert.Equal(4, pngIcon1.Chunks.Length);
        Assert.Equal(Constants.PngChunkTypes.Ihdr, pngIcon1.Chunks[0].Type);
        Assert.Equal(Constants.PngChunkTypes.Idat, pngIcon1.Chunks[1].Type);
        Assert.Equal(Constants.PngChunkTypes.Icon, pngIcon1.Chunks[2].Type);
        Assert.Equal(Constants.PngChunkTypes.Iend, pngIcon1.Chunks[3].Type);
        
        // assert - png icon 2 contain expected chunks and chunk types
        var pngIcon2 = pngIcons[1];
        Assert.Equal(3, pngIcon2.Chunks.Length);
        Assert.Equal(Constants.PngChunkTypes.Ihdr, pngIcon2.Chunks[0].Type);
        Assert.Equal(Constants.PngChunkTypes.Idat, pngIcon2.Chunks[1].Type);
        Assert.Equal(Constants.PngChunkTypes.Iend, pngIcon2.Chunks[2].Type);
    }

    [Fact]
    public async Task When_ReadingIconDataPngIcon_Then_IconDataAreRead()
    {
        // arrange - read png bytes from file
        var pngBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "DiskObjects", "AmigaPngIcon.info"));

        // arrange - create memory stream from png bytes
        var pngStream = new MemoryStream(pngBytes);

        // act - read png icons
        var pngIcons = (await PngIconReader.Read(pngStream)).ToList();
        
        // assert - 2 png icons are read
        Assert.Equal(2, pngIcons.Count);

        // arrange - get icon chunk from png icon 1
        var pngIcon1 = pngIcons[0];
        var iconChunk = pngIcon1.Chunks.FirstOrDefault(c => c.Type.SequenceEqual(Constants.PngChunkTypes.Icon));
        Assert.NotNull(iconChunk);

        // act - read icon data from png icon 1
        var iconData = PngIconReader.ReadIconData(iconChunk.Data);
        Assert.NotNull(iconData);

        // assert - icon data contain 4 icon attribute tags
        Assert.Equal(4, iconData.IconAttributeTags.Count);
        
        // assert - iconx, icony, stacksize and type tags are present in icon data with expected values
        Assert.Equal(Constants.IconAttributeTags.ATTR_ICONX, iconData.IconAttributeTags[0].Tag);
        Assert.Equal(22U, iconData.IconAttributeTags[0].Value);
        Assert.Equal(Constants.IconAttributeTags.ATTR_ICONY, iconData.IconAttributeTags[1].Tag);
        Assert.Equal(4U, iconData.IconAttributeTags[1].Value);
        Assert.Equal(Constants.IconAttributeTags.ATTR_STACKSIZE, iconData.IconAttributeTags[2].Tag);
        Assert.Equal(4096U, iconData.IconAttributeTags[2].Value);
        Assert.Equal(Constants.IconAttributeTags.ATTR_TYPE, iconData.IconAttributeTags[3].Tag);
        Assert.Equal(4U, iconData.IconAttributeTags[3].Value);

        // assert - default tool is read from icon data
        Assert.Equal("Multiview", iconData.DefaultTool);
        
        // assert - tool type is not present in icon data
        Assert.Null(iconData.ToolType);
    }
}