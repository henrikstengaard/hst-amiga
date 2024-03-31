using System.IO;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.UaeFsDbs;
using Xunit;

namespace Hst.Amiga.Tests.DiskObjectTests.UaeFsDbTests;

public class GivenUaeFsDbReader
{
    [Fact]
    public async Task When_ReadNodesFromFatUaeFsDb_Then_NodesMatch()
    {
        // arrange - uaefsdb bytes to read
        var uaeFsDbBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "UaeFsDbs", "FAT", "_UAEFSDB.___"));

        // act - read node 1
        var node1 = UaeFsDbReader.Read(uaeFsDbBytes);

        // assert - node 1 match
        Assert.Equal(64U, node1.Mode); // --s-rwed
        Assert.Equal("file1*", node1.AmigaName);
        Assert.Equal("__uae___file1_", node1.NormalName);
        Assert.Equal("", node1.Comment);

        // act - read node 2
        var node2 = UaeFsDbReader.Read(uaeFsDbBytes, 600);

        // assert - node 2 match
        Assert.Equal(0U, node2.Mode); // ----rwed
        Assert.Equal("file2<", node2.AmigaName);
        Assert.Equal("__uae___file2_", node2.NormalName);
        Assert.Equal("comment on file2", node2.Comment);
    }

    [Fact]
    public async Task When_ReadNodesFromNtfsUaeFsDb_Then_NodeMatch()
    {
        // arrange - uaefsdb bytes to read
        var uaeFsDbBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "UaeFsDbs", "NTFS", "_UAEFSDB1.___"));

        // act - read node 1
        var node1 = UaeFsDbReader.Read(uaeFsDbBytes, 0, UaeFsDbNode.NodeVersion.Version2);

        // assert - node 1 match
        Assert.Equal(64U, node1.Mode); // -s------
        Assert.Equal("file1*", node1.AmigaName);
        Assert.Equal("__uae___file1_", node1.NormalName);
        Assert.Equal("", node1.Comment);
        Assert.Equal(32U, node1.WinMode); // --s-----
        Assert.Equal("file1*", node1.AmigaNameUnicode);
        Assert.Equal("__uae___file1_", node1.NormalNameUnicode);
    }
}