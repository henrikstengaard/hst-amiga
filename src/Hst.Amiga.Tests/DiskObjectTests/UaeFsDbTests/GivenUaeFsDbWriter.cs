using System.IO;
using System.Linq;
using System.Text;
using Hst.Amiga.DataTypes.UaeFsDbs;
using Hst.Amiga.FileSystems;
using Xunit;

namespace Hst.Amiga.Tests.DiskObjectTests.UaeFsDbTests;

public class GivenUaeFsDbWriter
{
    [Fact]
    public void When_WriteUaeFsDbNodeVersion1_Then_BytesMatch()
    {
        // arrange
        var node = new UaeFsDbNode
        {
            Version = UaeFsDbNode.NodeVersion.Version1,
            Mode = (uint)ProtectionBits.Script,
            AmigaName = "file1*",
            NormalName = "__uae___file1_",
            Comment = "just a simple comment to check if it works"
        };

        // act
        var bytes = UaeFsDbWriter.Build(node);
        
        // assert - version 1 is 600 bytes in size
        Assert.Equal(600, bytes.Length);
        
        // assert - valid is 1
        Assert.Equal(1, bytes[0]);
        
        // assert - mode matches script protection bit
        var expectedModeBytes = new byte[] { 0, 0, 0, (byte)ProtectionBits.Script };
        Assert.Equal(expectedModeBytes, bytes.Skip(1).Take(4));
        
        // assert - amiga name matches
        var expectedAmigaNameBytes = Encoding.ASCII.GetBytes(node.AmigaName).Concat(new byte[] { 0 });
        Assert.Equal(expectedAmigaNameBytes, bytes.Skip(0x5).Take(node.AmigaName.Length + 1));
        
        // assert - normal name matches
        var expectedNormalNameBytes = Encoding.ASCII.GetBytes(node.NormalName).Concat(new byte[] { 0 });
        Assert.Equal(expectedNormalNameBytes, bytes.Skip(0x106).Take(node.NormalName.Length + 1));
        
        // assert - comment matches
        var expectedCommentBytes = Encoding.ASCII.GetBytes(node.Comment).Concat(new byte[] { 0 });
        Assert.Equal(expectedCommentBytes, bytes.Skip(0x207).Take(node.Comment.Length + 1));
    }
    
    [Fact]
    public void When_WriteUaeFsDbNodeVersion2_Then_BytesMatch()
    {
        // arrange
        var node = new UaeFsDbNode
        {
            Version = UaeFsDbNode.NodeVersion.Version2,
            Mode = (uint)ProtectionBits.Script,
            AmigaName = "file2*",
            NormalName = "__uae___file2_",
            Comment = "just a simple comment to check if it works",
            WinMode = (uint)FileAttributes.Archive,
            AmigaNameUnicode = "file2*",
            NormalNameUnicode = "__uae___file2_",
        };

        // act
        var bytes = UaeFsDbWriter.Build(node);
        
        // assert - version 1 is 1632 bytes in size
        Assert.Equal(1632, bytes.Length);
        
        // assert - valid is 1
        Assert.Equal(1, bytes[0]);
        
        // assert - mode matches script protection bit
        var expectedModeBytes = new byte[] { 0, 0, 0, (byte)ProtectionBits.Script };
        Assert.Equal(expectedModeBytes, bytes.Skip(1).Take(4));
        
        // assert - amiga name matches
        var expectedAmigaNameBytes = Encoding.ASCII.GetBytes(node.AmigaName).Concat(new byte[] { 0 });
        Assert.Equal(expectedAmigaNameBytes, bytes.Skip(0x5).Take(node.AmigaName.Length + 1));
        
        // assert - normal name matches
        var expectedNormalNameBytes = Encoding.ASCII.GetBytes(node.NormalName).Concat(new byte[] { 0 });
        Assert.Equal(expectedNormalNameBytes, bytes.Skip(0x106).Take(node.NormalName.Length + 1));
        
        // assert - comment matches
        var expectedCommentBytes = Encoding.ASCII.GetBytes(node.Comment).Concat(new byte[] { 0 });
        Assert.Equal(expectedCommentBytes, bytes.Skip(0x207).Take(node.Comment.Length + 1));
        
        // assert - win mode matches archive file attribute
        var expectedWinModeBytes = new byte[] { 0, 0, 0, (byte)FileAttributes.Archive };
        Assert.Equal(expectedWinModeBytes, bytes.Skip(0x258).Take(4));
        
        // assert - amiga name unicode matches
        var expectedAmigaNameUnicodeBytes =
            Encoding.Unicode.GetBytes(node.AmigaNameUnicode).Concat(new byte[] { 0, 0 });
        Assert.Equal(expectedAmigaNameUnicodeBytes, bytes.Skip(0x25c).Take((node.AmigaNameUnicode.Length + 1) * 2));
        
        // assert - normal name unicode matches
        var expectedNormalNameUnicodeBytes =
            Encoding.Unicode.GetBytes(node.NormalNameUnicode).Concat(new byte[] { 0, 0 });
        Assert.Equal(expectedNormalNameUnicodeBytes, bytes.Skip(0x45e).Take((node.NormalNameUnicode.Length + 1) * 2));
    }
}