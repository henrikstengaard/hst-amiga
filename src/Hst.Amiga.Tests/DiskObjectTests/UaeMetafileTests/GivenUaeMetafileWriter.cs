using System;
using System.Text;
using Hst.Amiga.DataTypes.UaeMetafiles;
using Xunit;

namespace Hst.Amiga.Tests.DiskObjectTests.UaeMetafileTests;

public class GivenUaeMetafileWriter
{
    [Fact]
    public void When_WritingUaeMetafileWithoutComment_ThenBytesMatch()
    {
        // arrange
        var uaeMetafile = new UaeMetafile
        {
            ProtectionBits = "----rwed",
            Date = new DateTime(2024, 3, 15, 19, 1, 13, 840, DateTimeKind.Local),
            Comment = string.Empty
        };
        
        // act
        var uaeMetafileBytes = UaeMetafileWriter.Build(uaeMetafile);

        // assert
        Assert.Equal("----rwed 2024-03-15 19:01:13.84 \n",
            Encoding.UTF8.GetString(uaeMetafileBytes));
    }
    
    [Fact]
    public void When_WritingUaeMetafileWithComment_ThenBytesMatch()
    {
        // arrange
        var uaeMetafile = new UaeMetafile
        {
            ProtectionBits = "----rwed",
            Date = new DateTime(2024, 3, 15, 19, 1, 13, 840, DateTimeKind.Local),
            Comment = "comment on file2"
        };
        
        // act
        var uaeMetafileBytes = UaeMetafileWriter.Build(uaeMetafile);

        // assert
        Assert.Equal("----rwed 2024-03-15 19:01:13.84 comment on file2\n",
            Encoding.UTF8.GetString(uaeMetafileBytes));
    }
}