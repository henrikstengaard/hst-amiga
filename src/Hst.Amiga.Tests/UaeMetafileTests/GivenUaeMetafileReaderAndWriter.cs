using System.IO;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.UaeMetafiles;
using Xunit;

namespace Hst.Amiga.Tests.UaeMetafileTests;

public class GivenUaeMetafileReaderAndWriter
{
    [Fact]
    public async Task When_ReadAndWriteUaeMetafile_Then_BytesMatch()
    {
        // arrange - uae metafile bytes to read
        var uaeMetafileBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "UaeMetafiles", "file2%3c.uaem"));

        // act - read uae metafile
        var uaeMetafile = UaeMetafileReader.Read(uaeMetafileBytes);

        // act - write use metafile
        var writtenUaeMetafileBytes = UaeMetafileWriter.Build(uaeMetafile);

        // assert - uae metafile bytes matches written uae metafile bytes
        Assert.Equal(uaeMetafileBytes.Length, writtenUaeMetafileBytes.Length);
        Assert.Equal(uaeMetafileBytes, writtenUaeMetafileBytes);
        
        
    }
}