using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.UaeFsDbs;
using Hst.Amiga.FileSystems;
using Xunit;

namespace Hst.Amiga.Tests.UaeFsDbTests;

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

    [Fact]
    public async Task When_WriteToStream_With_Two_Version1Nodes_Then_Stream_Contains_Both()
    {
        // arrange
        var node1 = new UaeFsDbNode
        {
            Version = UaeFsDbNode.NodeVersion.Version1,
            Mode = (uint)ProtectionBits.Script,
            AmigaName = "file1*",
            NormalName = "__uae___file1_",
            Comment = "comment file1"
        };
        var node2 = new UaeFsDbNode
        {
            Version = UaeFsDbNode.NodeVersion.Version1,
            Mode = (uint)ProtectionBits.Script,
            AmigaName = "file2*",
            NormalName = "__uae___file2_",
            Comment = "comment file2"
        };

        // act - write 2 nodes
        byte[] uaeFsDbBytes;
        using(var stream = new MemoryStream())
        {
            await UaeFsDbWriter.WriteToStream(stream, node1);
            await UaeFsDbWriter.WriteToStream(stream, node2);

            uaeFsDbBytes = stream.ToArray();
        }

        // assert - uaefsdb bytes matches size of 2 uaefsdb version 1 nodes
        Assert.Equal(Constants.UaeFsDbNodeVersion1Size * 2, uaeFsDbBytes.Length);

        // assert - uaefsdb bytes matches node2
        var node = UaeFsDbReader.ReadFromBytes(uaeFsDbBytes, 0, UaeFsDbNode.NodeVersion.Version1);
        Assert.Equal(node1.AmigaName, node.AmigaName);
        node = UaeFsDbReader.ReadFromBytes(uaeFsDbBytes, Constants.UaeFsDbNodeVersion1Size, UaeFsDbNode.NodeVersion.Version1);
        Assert.Equal(node2.AmigaName, node.AmigaName);
    }

    [Fact]
    public async Task When_WriteToStream_With_Same_Version1Node_Then_Stream_Contains_One()
    {
        // arrange
        var node = new UaeFsDbNode
        {
            Version = UaeFsDbNode.NodeVersion.Version1,
            Mode = (uint)ProtectionBits.Script,
            AmigaName = "file1*",
            NormalName = "__uae___file1_",
            Comment = "comment file1"
        };

        // act - write the same node twice
        byte[] uaeFsDbBytes;
        using(var stream = new MemoryStream())
        {
            await UaeFsDbWriter.WriteToStream(stream, node);
            await UaeFsDbWriter.WriteToStream(stream, node);

            uaeFsDbBytes = stream.ToArray();
        }

        // assert - uaefsdb bytes matches size of 1 uaefsdb version 1 node
        Assert.Equal(Constants.UaeFsDbNodeVersion1Size, uaeFsDbBytes.Length);

        // assert - uaefsdb bytes contains node
        var actualNode = UaeFsDbReader.ReadFromBytes(uaeFsDbBytes, 0, UaeFsDbNode.NodeVersion.Version1);
        Assert.Equal(actualNode.AmigaName, node.AmigaName);
    }

    [Fact]
    public async Task When_WriteToStream_With_Version1_And_2_Nodes_Then_Exception_Is_Thrown()
    {
        // arrange
        var node1 = new UaeFsDbNode
        {
            Version = UaeFsDbNode.NodeVersion.Version1,
            Mode = (uint)ProtectionBits.Script,
            AmigaName = "file1*",
            NormalName = "__uae___file1_",
            Comment = "comment file1"
        };
        var node2 = new UaeFsDbNode
        {
            Version = UaeFsDbNode.NodeVersion.Version2,
            Mode = (uint)ProtectionBits.Script,
            AmigaName = "file2*",
            NormalName = "__uae___file2_",
            Comment = "comment file2",
            WinMode = (uint)FileAttributes.Archive,
            AmigaNameUnicode = "file2*",
            NormalNameUnicode = "__uae___file2_",
        };

        // act
        using(var stream = new MemoryStream())
        {
            await UaeFsDbWriter.WriteToStream(stream, node1);

            // assert - writing version 2 to version 1 uaefsdb stream throws exception
            await Assert.ThrowsAsync<ArgumentException>(async () => 
                await UaeFsDbWriter.WriteToStream(stream, node2));
        }
    }

    [Fact]
    public async Task When_WriteToStream_With_Two_Version2Node_Then_Stream_Contains_Last_One()
    {
        // arrange
        var node1 = new UaeFsDbNode
        {
            Version = UaeFsDbNode.NodeVersion.Version2,
            Mode = (uint)ProtectionBits.Script,
            AmigaName = "file1*",
            NormalName = "__uae___file1_",
            Comment = "comment file1",
            WinMode = (uint)FileAttributes.Archive,
            AmigaNameUnicode = "file1*",
            NormalNameUnicode = "__uae___file1_",
        };
        var node2 = new UaeFsDbNode
        {
            Version = UaeFsDbNode.NodeVersion.Version2,
            Mode = (uint)ProtectionBits.Script,
            AmigaName = "file2*",
            NormalName = "__uae___file2_",
            Comment = "comment file2",
            WinMode = (uint)FileAttributes.Archive,
            AmigaNameUnicode = "file2*",
            NormalNameUnicode = "__uae___file2_",
        };

        // act
        byte[] uaeFsDbBytes;
        using(var stream = new MemoryStream())
        {
            await UaeFsDbWriter.WriteToStream(stream, node1);
            await UaeFsDbWriter.WriteToStream(stream, node2);

            uaeFsDbBytes = stream.ToArray();
        }

        // assert - uaefsdb bytes matches uaefsdb version 2 size
        Assert.Equal(Hst.Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbNodeVersion2Size, uaeFsDbBytes.Length);

        // assert - uaefsdb bytes matches node2
        var node = UaeFsDbReader.ReadFromBytes(uaeFsDbBytes, 0, UaeFsDbNode.NodeVersion.Version2);
        Assert.Equal(node2.AmigaName, node.AmigaName);
    }

    [Fact]
    public async Task When_WriteToStream_With_Version2_And_1_Nodes_Then_Exception_Is_Thrown()
    {
        // arrange
        var node1 = new UaeFsDbNode
        {
            Version = UaeFsDbNode.NodeVersion.Version2,
            Mode = (uint)ProtectionBits.Script,
            AmigaName = "file1*",
            NormalName = "__uae___file1_",
            Comment = "comment file1",
            WinMode = (uint)FileAttributes.Archive,
            AmigaNameUnicode = "file1*",
            NormalNameUnicode = "__uae___file1_",
        };
        var node2 = new UaeFsDbNode
        {
            Version = UaeFsDbNode.NodeVersion.Version1,
            Mode = (uint)ProtectionBits.Script,
            AmigaName = "file2*",
            NormalName = "__uae___file2_",
            Comment = "comment file2"
        };

        // act
        using(var stream = new MemoryStream())
        {
            await UaeFsDbWriter.WriteToStream(stream, node1);

            // assert - writing version 1 to version 2 uaefsdb stream throws exception
            await Assert.ThrowsAsync<ArgumentException>(async () => 
                await UaeFsDbWriter.WriteToStream(stream, node2));
        }
    }
}