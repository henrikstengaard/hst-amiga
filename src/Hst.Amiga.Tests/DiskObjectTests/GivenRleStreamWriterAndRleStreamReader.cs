namespace Hst.Amiga.Tests.DiskObjectTests;

using System.Collections.Generic;
using System.IO;
using DataTypes.DiskObjects.ColorIcons;
using Xunit;

public class GivenRleStreamWriterAndRleStreamReader
{
    [Fact]
    public void WhenWritingRepeatedDataThenRleCompressedDataMatches()
    {
        // arrange - data to rle compress
        const int bitDepth = 8;
        var expectedData = new byte[] { 1, 255, 1, 1, 1, 1, 1, 1, 1, 7, 7, 7 };
        
        // act - write data
        var stream = new MemoryStream();
        var rleWriter = new RleStreamWriter(stream, bitDepth);
        foreach (var d in expectedData)
        {
            rleWriter.Write(d);
        }   
        rleWriter.Finish();
        
        // act - read data
        stream.Position = 0;
        var rleReader = new RleStreamReader(stream, bitDepth, expectedData.Length);
        var actualData = new List<byte>();
        for (var i = 0; i < expectedData.Length; i++)
        {
            actualData.Add(rleReader.ReadData8());
        }
        
        // assert - data matches expected
        Assert.Equal(expectedData.Length, actualData.Count);
        Assert.Equal(expectedData, actualData);
    }
    
    [Fact]
    public void WhenWriting2000RepeatedDataThenRleCompressedDataMatches()
    {
        // arrange - data to rle compress
        const int bitDepth = 8;
        var expectedData = new byte[2000];
        
        // act - write data
        var stream = new MemoryStream();
        var rleWriter = new RleStreamWriter(stream, bitDepth);
        foreach (var d in expectedData)
        {
            rleWriter.Write(d);
        }   
        rleWriter.Finish();
        
        // act - read data
        stream.Position = 0;
        var rleReader = new RleStreamReader(stream, bitDepth, expectedData.Length);
        var actualData = new List<byte>();
        for (var i = 0; i < expectedData.Length; i++)
        {
            actualData.Add(rleReader.ReadData8());
        }
        
        // assert - data matches expected
        Assert.Equal(expectedData.Length, actualData.Count);
        Assert.Equal(expectedData, actualData);
    }
}