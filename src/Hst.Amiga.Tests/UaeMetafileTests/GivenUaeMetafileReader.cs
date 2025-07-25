using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.UaeMetafiles;
using Xunit;

namespace Hst.Amiga.Tests.UaeMetafileTests;

public class GivenUaeMetafileReader
{
    private readonly Encoding iso88591Encoding;
    private const int MaxCommentLength = 80;

    public GivenUaeMetafileReader()
    {
        iso88591Encoding = Encoding.GetEncoding("ISO-8859-1");
    }

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

    [Fact]
    public void When_ReadUaeMetafileWithCommentLargerThan79Chars_ThenCommentIsTruncatedTo79Chars()
    {
        // arrange
        var comment = new string('A', 85);
        var uaeMetafileBytes = iso88591Encoding.GetBytes("----rwed 2024-03-15 19:01:13.84 ")
            .Concat(iso88591Encoding.GetBytes(comment))
            .Concat(iso88591Encoding.GetBytes("\n")).ToArray();

        // act - read uae metafile
        var uaeMetafile = UaeMetafileReader.Read(uaeMetafileBytes);

        // assert - protection bits match
        Assert.Equal("----rwed", uaeMetafile.ProtectionBits);

        // assert - date match
        var expectedDate = new DateTime(2024, 3, 15, 19, 1, 13, 840, DateTimeKind.Local);
        Assert.Equal(expectedDate, uaeMetafile.Date);

        // assert - comment match
        Assert.Equal(comment.Substring(0, MaxCommentLength), uaeMetafile.Comment);
    }
    
    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("")]
    public void When_ReadUaeMetafileWithCommentEmptyOrEndsWithNewline_ThenCommentIsEmpty(string comment)
    {
        // arrange
        var uaeMetafileBytes = iso88591Encoding.GetBytes("----rwed 2024-03-15 19:01:13.84 ")
            .Concat(iso88591Encoding.GetBytes(comment)).ToArray();

        // act - read uae metafile
        var uaeMetafile = UaeMetafileReader.Read(uaeMetafileBytes);

        // assert - protection bits match
        Assert.Equal("----rwed", uaeMetafile.ProtectionBits);

        // assert - date match
        var expectedDate = new DateTime(2024, 3, 15, 19, 1, 13, 840, DateTimeKind.Local);
        Assert.Equal(expectedDate, uaeMetafile.Date);

        // assert - comment match
        Assert.Equal(string.Empty, uaeMetafile.Comment);
    }
}