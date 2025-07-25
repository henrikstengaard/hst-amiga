using System.IO;
using System.Linq;
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
        var uaeMetafileBytes = PatchNewline(await File.ReadAllBytesAsync(
            Path.Combine("TestData", "UaeMetafiles", "file2%3c.uaem")));

        // act - read uae metafile
        var uaeMetafile = UaeMetafileReader.Read(uaeMetafileBytes);

        // act - write use metafile
        var writtenUaeMetafileBytes = UaeMetafileWriter.Build(uaeMetafile);

        // assert - uae metafile bytes matches written uae metafile bytes
        Assert.Equal(uaeMetafileBytes.Length, writtenUaeMetafileBytes.Length);
        Assert.Equal(uaeMetafileBytes, writtenUaeMetafileBytes);
    }
    
    private static byte[] PatchNewline(byte[] uaeMetafileBytes) 
    {
        if (uaeMetafileBytes[^2] == '\r' &&
            uaeMetafileBytes[^1] == '\n')
        {
            return uaeMetafileBytes.Take(uaeMetafileBytes.Length - 2).ToArray().Concat(new byte[]{ 0xa }).ToArray();
        }
        
        return uaeMetafileBytes;
    }
}