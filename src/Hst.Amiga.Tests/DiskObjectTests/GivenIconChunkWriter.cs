using Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons;
using Xunit;

namespace Hst.Amiga.Tests.DiskObjectTests;

public class GivenIconChunkWriter
{
    [Fact]
    public void When_WritingIconData_Then_IconDataIsWritten()
    {
        // arrange - create icon data with icon attribute tags, default tool and tool type
        var iconData = new IconData(new[]
        {
            new IconTag(Constants.IconAttributeTags.ATTR_ICONX, 22U),
            new IconTag(Constants.IconAttributeTags.ATTR_ICONY, 4U),
            new IconTag(Constants.IconAttributeTags.ATTR_STACKSIZE, 4096U),
            new IconTag(Constants.IconAttributeTags.ATTR_TYPE, 4U)
        }, "Multiview", null, null);

        // act - write icon data to icon chunk
        var iconChunkData = IconChunkWriter.WriteIconChunkData(iconData);
        
        // assert - icon chunk contain expected type and data
        var expectedIconChunkData = new byte[]
        {
            0x80, 0x00, 0x10, 0x01, // ATTR_ICONX tag
            0x00, 0x00, 0x00, 0x16, // ATTR_ICONX value
            0x80, 0x00, 0x10, 0x02, // ATTR_ICONY tag
            0x00, 0x00, 0x00, 0x04, // ATTR_ICONY value
            0x80, 0x00, 0x10, 0x09, // ATTR_STACKSIZE tag
            0x00, 0x00, 0x10, 0x00, // ATTR_STACKSIZE value
            0x80, 0x00, 0x10, 0x0f, // ATTR_TYPE tag
            0x00, 0x00, 0x00, 0x04, // ATTR_TYPE value
            0x80, 0x00, 0x10, 0x0a, // ATTR_DEFAULTTOOL tag
            (byte)'M', (byte)'u', (byte)'l', (byte)'t', (byte)'i', (byte)'v', (byte)'i', (byte)'e', (byte)'w', 0x0 // default tool "Multiview" with null terminator
        };
        Assert.Equal(expectedIconChunkData.Length, iconChunkData.Length);
        Assert.Equal(expectedIconChunkData, iconChunkData);
    }
}