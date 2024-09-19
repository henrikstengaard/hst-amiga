using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.UaeFsDbs;
using Xunit;

namespace Hst.Amiga.Tests.UaeFsDbTests;

public class GivenUaeFsDbReader
{
    [Fact]
    public async Task When_ReadNodesFromFatUaeFsDbBytes_Then_NodesMatch()
    {
        // arrange - uaefsdb bytes to read
        var uaeFsDbBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "UaeFsDbs", "FAT", "_UAEFSDB.___"));

        // act - read all nodes from uaefsdb
        var uaeFsDbNodes = new List<UaeFsDbNode>();
        var offset = 0;
        while (offset + Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbNodeVersion1Size <= uaeFsDbBytes.Length)
        {
            uaeFsDbNodes.Add(UaeFsDbReader.ReadFromBytes(uaeFsDbBytes, offset));
            offset += Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbNodeVersion1Size;
        }
        
        // assert - uaefsdb nodes match
        Assert.Equal(6, uaeFsDbNodes.Count);
        
        // assert - node 1 match
        var node1 = uaeFsDbNodes[0];
        Assert.Equal(0U, node1.Mode); // ----rwed
        Assert.Equal("file5..", node1.AmigaName);
        Assert.Equal("__uae___file5__", node1.NormalName);
        Assert.Equal("", node1.Comment);

        // assert - node 2 match
        var node2 = uaeFsDbNodes[1];
        Assert.Equal(0U, node2.Mode); // ----rwed
        Assert.Equal("file4.", node2.AmigaName);
        Assert.Equal("__uae___file4_", node2.NormalName);
        Assert.Equal("", node2.Comment);

        // assert - node 3 match
        var node3 = uaeFsDbNodes[2];
        Assert.Equal(0U, node3.Mode); // ----rwed
        Assert.Equal("file2<", node3.AmigaName);
        Assert.Equal("__uae___file2_", node3.NormalName);
        Assert.Equal("comment on file2", node3.Comment);

        // assert - node 4 match
        var node4 = uaeFsDbNodes[3];
        Assert.Equal(64U, node4.Mode); // --s-rwed
        Assert.Equal("file1*", node4.AmigaName);
        Assert.Equal("__uae___file1_", node4.NormalName);
        Assert.Equal("", node4.Comment);
        
        // assert - node 5 match
        var node5 = uaeFsDbNodes[4];
        Assert.Equal(64U, node5.Mode); // --s-rwed
        Assert.Equal("dir2", node5.AmigaName);
        Assert.Equal("dir2", node5.NormalName);
        Assert.Equal("", node5.Comment);

        // assert - node 6 match
        var node6 = uaeFsDbNodes[5];
        Assert.Equal(0U, node6.Mode); // ----rwed
        Assert.Equal("dir1*", node6.AmigaName);
        Assert.Equal("__uae___dir1_", node6.NormalName);
        Assert.Equal("", node6.Comment);
    }

    [Fact]
    public async Task When_ReadNodesFromFatUaeFsDbStream_Then_NodesMatch()
    {
        // arrange - uaefsdb path to read
        var uaeFsDbPath = Path.Combine("TestData", "UaeFsDbs", "FAT", "_UAEFSDB.___");

        // act - read uaefsdb nodes from stream
        List<UaeFsDbNode> uaeFsDbNodes;
        using (var stream = File.OpenRead(uaeFsDbPath))
        {
            uaeFsDbNodes = (await UaeFsDbReader.ReadFromStream(stream)).ToList();
        }

        // assert - uaefsdb nodes match
        Assert.Equal(6, uaeFsDbNodes.Count);
        
        // assert - node 1 match
        var node1 = uaeFsDbNodes[0];
        Assert.Equal(0U, node1.Mode); // ----rwed
        Assert.Equal("file5..", node1.AmigaName);
        Assert.Equal("__uae___file5__", node1.NormalName);
        Assert.Equal("", node1.Comment);

        // assert - node 2 match
        var node2 = uaeFsDbNodes[1];
        Assert.Equal(0U, node2.Mode); // ----rwed
        Assert.Equal("file4.", node2.AmigaName);
        Assert.Equal("__uae___file4_", node2.NormalName);
        Assert.Equal("", node2.Comment);

        // assert - node 3 match
        var node3 = uaeFsDbNodes[2];
        Assert.Equal(0U, node3.Mode); // ----rwed
        Assert.Equal("file2<", node3.AmigaName);
        Assert.Equal("__uae___file2_", node3.NormalName);
        Assert.Equal("comment on file2", node3.Comment);

        // assert - node 4 match
        var node4 = uaeFsDbNodes[3];
        Assert.Equal(64U, node4.Mode); // --s-rwed
        Assert.Equal("file1*", node4.AmigaName);
        Assert.Equal("__uae___file1_", node4.NormalName);
        Assert.Equal("", node4.Comment);
        
        // assert - node 5 match
        var node5 = uaeFsDbNodes[4];
        Assert.Equal(64U, node5.Mode); // --s-rwed
        Assert.Equal("dir2", node5.AmigaName);
        Assert.Equal("dir2", node5.NormalName);
        Assert.Equal("", node5.Comment);

        // assert - node 6 match
        var node6 = uaeFsDbNodes[5];
        Assert.Equal(0U, node6.Mode); // ----rwed
        Assert.Equal("dir1*", node6.AmigaName);
        Assert.Equal("__uae___dir1_", node6.NormalName);
        Assert.Equal("", node6.Comment);
    }

    [Fact]
    public async Task When_ReadNodesFromFatUaeFsDbFile_Then_NodesMatch()
    {
        // arrange - uaefsdb path to read
        var uaeFsDbPath = Path.Combine("TestData", "UaeFsDbs", "FAT", "_UAEFSDB.___");

        // act - read uaefsdb nodes from stream
        var uaeFsDbNodes = (await UaeFsDbReader.ReadFromFile(uaeFsDbPath)).ToList();

        // assert - uaefsdb nodes match
        Assert.Equal(6, uaeFsDbNodes.Count);
        
        // assert - node 1 match
        var node1 = uaeFsDbNodes[0];
        Assert.Equal(0U, node1.Mode); // ----rwed
        Assert.Equal("file5..", node1.AmigaName);
        Assert.Equal("__uae___file5__", node1.NormalName);
        Assert.Equal("", node1.Comment);

        // assert - node 2 match
        var node2 = uaeFsDbNodes[1];
        Assert.Equal(0U, node2.Mode); // ----rwed
        Assert.Equal("file4.", node2.AmigaName);
        Assert.Equal("__uae___file4_", node2.NormalName);
        Assert.Equal("", node2.Comment);

        // assert - node 3 match
        var node3 = uaeFsDbNodes[2];
        Assert.Equal(0U, node3.Mode); // ----rwed
        Assert.Equal("file2<", node3.AmigaName);
        Assert.Equal("__uae___file2_", node3.NormalName);
        Assert.Equal("comment on file2", node3.Comment);

        // assert - node 4 match
        var node4 = uaeFsDbNodes[3];
        Assert.Equal(64U, node4.Mode); // --s-rwed
        Assert.Equal("file1*", node4.AmigaName);
        Assert.Equal("__uae___file1_", node4.NormalName);
        Assert.Equal("", node4.Comment);
        
        // assert - node 5 match
        var node5 = uaeFsDbNodes[4];
        Assert.Equal(64U, node5.Mode); // --s-rwed
        Assert.Equal("dir2", node5.AmigaName);
        Assert.Equal("dir2", node5.NormalName);
        Assert.Equal("", node5.Comment);

        // assert - node 6 match
        var node6 = uaeFsDbNodes[5];
        Assert.Equal(0U, node6.Mode); // ----rwed
        Assert.Equal("dir1*", node6.AmigaName);
        Assert.Equal("__uae___dir1_", node6.NormalName);
        Assert.Equal("", node6.Comment);
    }

    [Fact]
    public async Task When_ReadNodesFromNtfsUaeFsDb_Then_NodeMatch()
    {
        // arrange - dir 1 uaefsdb bytes to read
        var dir1UaeFsDbPath = Path.Combine("TestData", "UaeFsDbs", "NTFS", "_UAEFSDB.___dir1_");

        // act - read dir 1 uaefsdb node
        var dir1UaeFsDbBytes = await File.ReadAllBytesAsync(dir1UaeFsDbPath);
        var dir1Node = UaeFsDbReader.ReadFromBytes(dir1UaeFsDbBytes, 0, UaeFsDbNode.NodeVersion.Version2);

        // assert - dir 1 uaefsdb node matches
        Assert.Equal(0U, dir1Node.Mode); // -s------
        Assert.Equal("dir1*", dir1Node.AmigaName);
        Assert.Equal("__uae___dir1_", dir1Node.NormalName);
        Assert.Equal("", dir1Node.Comment);
        Assert.Equal(32U, dir1Node.WinMode); // -a--
        Assert.Equal(FileAttributes.Archive, (FileAttributes)dir1Node.WinMode);
        Assert.Equal("dir1*", dir1Node.AmigaNameUnicode);
        Assert.Equal("__uae___dir1_", dir1Node.NormalNameUnicode);

        // arrange - dir 2 uaefsdb bytes to read
        var dir2UaeFsDbPath = Path.Combine("TestData", "UaeFsDbs", "NTFS", "_UAEFSDB.___dir2");

        // act - read dir 2 uaefsdb node
        var dir2UaeFsDbBytes = await File.ReadAllBytesAsync(dir2UaeFsDbPath);
        var dir2Node = UaeFsDbReader.ReadFromBytes(dir2UaeFsDbBytes, 0, UaeFsDbNode.NodeVersion.Version2);

        // assert - dir 2 uaefsdb node matches
        Assert.Equal(64U, dir2Node.Mode); // -s------
        Assert.Equal("dir2", dir2Node.AmigaName);
        Assert.Equal("dir2", dir2Node.NormalName);
        Assert.Equal("", dir2Node.Comment);
        Assert.Equal(32U, dir2Node.WinMode); // -a--
        Assert.Equal(FileAttributes.Archive, (FileAttributes)dir2Node.WinMode);
        Assert.Equal("dir2", dir2Node.AmigaNameUnicode);
        Assert.Equal("dir2", dir2Node.NormalNameUnicode);

        // arrange - file 1 uaefsdb bytes to read
            var file1UaeFsDbPath = Path.Combine("TestData", "UaeFsDbs", "NTFS", "_UAEFSDB.___file1_");

        // act - read file 1 uaefsdb node
        var file1UaeFsDbBytes = await File.ReadAllBytesAsync(file1UaeFsDbPath);
        var file1Node = UaeFsDbReader.ReadFromBytes(file1UaeFsDbBytes, 0, UaeFsDbNode.NodeVersion.Version2);

        // assert - file 1 uaefsdb node matches
        Assert.Equal(64U, file1Node.Mode); // -s------
        Assert.Equal("file1*", file1Node.AmigaName);
        Assert.Equal("__uae___file1_", file1Node.NormalName);
        Assert.Equal("", file1Node.Comment);
        Assert.Equal(32U, file1Node.WinMode); // -a--
        Assert.Equal(FileAttributes.Archive, (FileAttributes)file1Node.WinMode);
        Assert.Equal("file1*", file1Node.AmigaNameUnicode);
        Assert.Equal("__uae___file1_", file1Node.NormalNameUnicode);
        
        // arrange - file 2 uaefsdb bytes to read
        var file2UaeFsDbPath = Path.Combine("TestData", "UaeFsDbs", "NTFS", "_UAEFSDB.___file2_");

        // act - read file 2 uaefsdb node
        var file2UaeFsDbBytes = await File.ReadAllBytesAsync(file2UaeFsDbPath);
        var file2Node = UaeFsDbReader.ReadFromBytes(file2UaeFsDbBytes, 0, UaeFsDbNode.NodeVersion.Version2);

        // assert - file 2 uaefsdb node matches
        Assert.Equal(0U, file2Node.Mode); // -s------
        Assert.Equal("file2<", file2Node.AmigaName);
        Assert.Equal("__uae___file2_", file2Node.NormalName);
        Assert.Equal("comment on file2", file2Node.Comment);
        Assert.Equal(32U, file2Node.WinMode); // -a--
        Assert.Equal(FileAttributes.Archive, (FileAttributes)file2Node.WinMode);
        Assert.Equal("file2<", file2Node.AmigaNameUnicode);
        Assert.Equal("__uae___file2_", file2Node.NormalNameUnicode);

        // arrange - file 4 uaefsdb bytes to read
        var file4UaeFsDbPath = Path.Combine("TestData", "UaeFsDbs", "NTFS", "_UAEFSDB.___file4_");

        // act - read file 4 uaefsdb node
        var file4UaeFsDbBytes = await File.ReadAllBytesAsync(file4UaeFsDbPath);
        var file4Node = UaeFsDbReader.ReadFromBytes(file4UaeFsDbBytes, 0, UaeFsDbNode.NodeVersion.Version2);

        // assert - file 4 uaefsdb node matches
        Assert.Equal(0U, file4Node.Mode); // -s------
        Assert.Equal("file4.", file4Node.AmigaName);
        Assert.Equal("__uae___file4_", file4Node.NormalName);
        Assert.Equal("", file4Node.Comment);
        Assert.Equal(32U, file4Node.WinMode); // -a--
        Assert.Equal(FileAttributes.Archive, (FileAttributes)file4Node.WinMode);
        Assert.Equal("file4.", file4Node.AmigaNameUnicode);
        Assert.Equal("__uae___file4_", file4Node.NormalNameUnicode);
        
        // arrange - file 5 uaefsdb bytes to read
        var file5UaeFsDbPath = Path.Combine("TestData", "UaeFsDbs", "NTFS", "_UAEFSDB.___file5__");

        // act - read file 5 uaefsdb node
        var file5UaeFsDbBytes = await File.ReadAllBytesAsync(file5UaeFsDbPath);
        var file5Node = UaeFsDbReader.ReadFromBytes(file5UaeFsDbBytes, 0, UaeFsDbNode.NodeVersion.Version2);

        // assert - file 5 uaefsdb node matches
        Assert.Equal(0U, file5Node.Mode); // -s------
        Assert.Equal("file5..", file5Node.AmigaName);
        Assert.Equal("__uae___file5__", file5Node.NormalName);
        Assert.Equal("", file5Node.Comment);
        Assert.Equal(32U, file5Node.WinMode); // -a--
        Assert.Equal(FileAttributes.Archive, (FileAttributes)file5Node.WinMode);
        Assert.Equal("file5..", file5Node.AmigaNameUnicode);
        Assert.Equal("__uae___file5__", file5Node.NormalNameUnicode);
    }
}