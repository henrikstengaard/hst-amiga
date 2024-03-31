using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.UaeFsDbs;
using Xunit;

namespace Hst.Amiga.Tests.DiskObjectTests.UaeFsDbTests;

public class GivenUaeFsDbReaderAndWriter
{
    [Fact]
    public async Task When_ReadAndWriteUaeFsDbNodeVersion1_Then_BytesMatch()
    {
        // arrange - uaefsdb bytes to read
        var uaeFsDbBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "UaeFsDbs", "FAT", "_UAEFSDB.___"));

        // act - read node
        var uaeFsDbNode = UaeFsDbReader.Read(uaeFsDbBytes);

        // act - write node bytes
        var writtenUaeFsFbBytes = UaeFsDbWriter.Build(uaeFsDbNode);
        
        // assert - uaefsdb bytes matches written uaefsdb bytes
        Assert.Equal(Constants.UaeFsDbNodeVersion1Size, writtenUaeFsFbBytes.Length);
        Assert.Equal(uaeFsDbBytes.Take(Constants.UaeFsDbNodeVersion1Size), writtenUaeFsFbBytes);
    }
    
    [Fact]
    public async Task When_ReadAndWriteUaeFsDbNodeVersion2_Then_BytesMatch()
    {
        // arrange - uaefsdb bytes to read
        var uaeFsDbBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "UaeFsDbs", "NTFS", "_UAEFSDB1.___"));

        // act - read node
        var uaeFsDbNode = UaeFsDbReader.Read(uaeFsDbBytes, 0, UaeFsDbNode.NodeVersion.Version2);

        // act - write node bytes
        var writtenUaeFsFbBytes = UaeFsDbWriter.Build(uaeFsDbNode);
        
        // assert - uaefsdb bytes matches written uaefsdb bytes
        Assert.Equal(Constants.UaeFsDbNodeVersion2Size, writtenUaeFsFbBytes.Length);
        Assert.Equal(uaeFsDbBytes, writtenUaeFsFbBytes);
    }
}