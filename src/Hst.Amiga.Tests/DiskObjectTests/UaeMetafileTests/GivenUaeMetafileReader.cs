using System;
using System.IO;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.UaeMetafiles;
using Xunit;

namespace Hst.Amiga.Tests.DiskObjectTests.UaeMetafileTests;

public class GivenUaeMetafileReader
{
    [Fact]
    public async Task When_ReadUaeMetafileWithoutComment_Then_UaeMetafileMatch()
    {
        // arrange - uae metafile bytes to read
        var uaeMetafileBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "UaeMetafiles", "file1%2a.uaem"));

        // act - read uae metafile
        var uaeMetafile = UaeMetafileReader.Read(uaeMetafileBytes);

        // assert - protection bits match
        Assert.Equal("-s--rwed", uaeMetafile.ProtectionBits);

        // assert - date match
        var expectedDate = new DateTime(2024, 3, 15, 19, 1, 2, 500, DateTimeKind.Local);
        Assert.Equal(expectedDate, uaeMetafile.Date);

        // assert - comment match
        Assert.Equal(string.Empty, uaeMetafile.Comment);
    }
    
    [Fact]
    public async Task When_ReadUaeMetafileWithComment_Then_UaeMetafileMatch()
    {
        // arrange - uae metafile bytes to read
        var uaeMetafileBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "UaeMetafiles", "file2%3c.uaem"));

        // act - read uae metafile
        var uaeMetafile = UaeMetafileReader.Read(uaeMetafileBytes);

        // assert - protection bits match
        Assert.Equal("----rwed", uaeMetafile.ProtectionBits);
        
        // assert - date match
        var expectedDate = new DateTime(2024, 3, 15, 19, 1, 13, 840, DateTimeKind.Local);
        Assert.Equal(expectedDate, uaeMetafile.Date);

        // assert - comment match
        Assert.Equal("comment on file2", uaeMetafile.Comment);
    }
}