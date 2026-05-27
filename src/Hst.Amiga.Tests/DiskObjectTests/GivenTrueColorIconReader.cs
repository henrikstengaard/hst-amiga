using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons;
using Xunit;

namespace Hst.Amiga.Tests.DiskObjectTests;

public class GivenTrueColorIconReader
{
    [Fact]
    public async Task When_ReadingTrueColorIcon_Then_TrueColorIconsAreRead()
    {
        // arrange - read true color icon bytes from file
        var iconBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "DiskObjects", "AmigaPngIcon.info"));
            
        // arrange - create memory stream from true color icon bytes
        var iconStream = new MemoryStream(iconBytes);

        // act - read true color icons
        var trueColorIcons = (await TrueColorIconReader.ReadTrueColorIcons(iconStream)).ToList();
        
        // assert - 2 true color icons are read
        Assert.Equal(2, trueColorIcons.Count);
        
        // assert - true color icon 1 contain expected chunks and chunk types
        var trueColorIcon1 = trueColorIcons[0];
        Assert.Equal(4, trueColorIcon1.Chunks.Length);
        Assert.Equal(Constants.PngChunkTypes.Ihdr, trueColorIcon1.Chunks[0].Type);
        Assert.Equal(Constants.PngChunkTypes.Idat, trueColorIcon1.Chunks[1].Type);
        Assert.Equal(Constants.PngChunkTypes.Icon, trueColorIcon1.Chunks[2].Type);
        Assert.Equal(Constants.PngChunkTypes.Iend, trueColorIcon1.Chunks[3].Type);
        
        // assert - true color icon 2 contain expected chunks and chunk types
        var trueColorIcon2 = trueColorIcons[1];
        Assert.Equal(3, trueColorIcon2.Chunks.Length);
        Assert.Equal(Constants.PngChunkTypes.Ihdr, trueColorIcon2.Chunks[0].Type);
        Assert.Equal(Constants.PngChunkTypes.Idat, trueColorIcon2.Chunks[1].Type);
        Assert.Equal(Constants.PngChunkTypes.Iend, trueColorIcon2.Chunks[2].Type);
    }

    [Fact]
    public async Task When_ReadingIconDataPngIcon_Then_IconDataAreRead()
    {
        // arrange - read true color icon bytes from file
        var iconBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "DiskObjects", "AmigaPngIcon.info"));

        // arrange - create memory stream from true color icon bytes
        var iconStream = new MemoryStream(iconBytes);

        // act - read true color icons
        var trueColorIcons = (await TrueColorIconReader.ReadTrueColorIcons(iconStream)).ToList();
        
        // assert - 2 true color icons are read
        Assert.Equal(2, trueColorIcons.Count);

        // arrange - get icon chunk from true color icon 1
        var trueColorIcon1 = trueColorIcons[0];
        var iconChunk = trueColorIcon1.Chunks.FirstOrDefault(c => c.Type.SequenceEqual(Constants.PngChunkTypes.Icon));
        Assert.NotNull(iconChunk);

        // act - read icon data from true color icon 1
        var iconData = IconChunkReader.ReadIconChunkData(iconChunk.Data);
        Assert.NotNull(iconData);

        // assert - icon data contain 4 icon attribute tags
        Assert.Equal(4, iconData.IconTags.Count);
        
        // assert - iconx, icony, stacksize and type tags are present in icon data with expected values
        Assert.Equal(Constants.IconAttributeTags.ATTR_ICONX, iconData.IconTags[0].Tag);
        Assert.Equal(22U, iconData.IconTags[0].Value);
        Assert.Equal(Constants.IconAttributeTags.ATTR_ICONY, iconData.IconTags[1].Tag);
        Assert.Equal(4U, iconData.IconTags[1].Value);
        Assert.Equal(Constants.IconAttributeTags.ATTR_STACKSIZE, iconData.IconTags[2].Tag);
        Assert.Equal(4096U, iconData.IconTags[2].Value);
        Assert.Equal(Constants.IconAttributeTags.ATTR_TYPE, iconData.IconTags[3].Tag);
        Assert.Equal(4U, iconData.IconTags[3].Value);

        // assert - default tool is read from icon data
        Assert.Equal("Multiview", iconData.DefaultTool);
        
        // assert - tool type is not present in icon data
        Assert.Null(iconData.ToolType);
    }
}