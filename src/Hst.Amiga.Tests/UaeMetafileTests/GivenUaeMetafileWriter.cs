using System;
using System.Linq;
using System.Text;
using Hst.Amiga.DataTypes.UaeMetafiles;
using Xunit;

namespace Hst.Amiga.Tests.UaeMetafileTests;

public class GivenUaeMetafileWriter
{
    private readonly Encoding iso88591Encoding;

    public GivenUaeMetafileWriter()
    {
        iso88591Encoding = Encoding.GetEncoding("ISO-8859-1");
    }

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
            iso88591Encoding.GetString(uaeMetafileBytes));
    }

    [Fact]
    public void When_WritingUaeMetafileWithSpecialCharsInComment_ThenBytesMatch()
    {
        // arrange
        var comment = iso88591Encoding.GetString(new byte[] { 0xa0, 0xa0, 0xa0 });
        var uaeMetafile = new UaeMetafile
        {
            ProtectionBits = "----rwed",
            Date = new DateTime(2024, 3, 15, 19, 1, 13, 840, DateTimeKind.Local),
            Comment = comment
        };

        // act
        var uaeMetafileBytes = UaeMetafileWriter.Build(uaeMetafile);

        // assert
        var expectedUaeMetafileBytes = iso88591Encoding.GetBytes("----rwed 2024-03-15 19:01:13.84 ")
            .Concat(new byte[] { 0xa0, 0xa0, 0xa0 })
            .Concat(iso88591Encoding.GetBytes("\n"));
        Assert.Equal(expectedUaeMetafileBytes, uaeMetafileBytes);
    }

    [Fact]
    public void When_WritingUaeMetafileWithCommentLargerThan80Chars_ThenCommentIsTruncatedTo80Chars()
    {
        // arrange
        var comment = new string('A', 85);
        var uaeMetafile = new UaeMetafile
        {
            ProtectionBits = "----rwed",
            Date = new DateTime(2024, 3, 15, 19, 1, 13, 840, DateTimeKind.Local),
            Comment = comment
        };

        // act
        var uaeMetafileBytes = UaeMetafileWriter.Build(uaeMetafile);

        // assert
        var expectedUaeMetafileBytes = iso88591Encoding.GetBytes("----rwed 2024-03-15 19:01:13.84 ")
            .Concat(iso88591Encoding.GetBytes(comment.Substring(0, 80)))
            .Concat(iso88591Encoding.GetBytes("\n"));
        Assert.Equal(expectedUaeMetafileBytes, uaeMetafileBytes);
    }
}