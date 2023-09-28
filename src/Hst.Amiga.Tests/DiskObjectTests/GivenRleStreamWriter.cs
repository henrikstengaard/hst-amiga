namespace Hst.Amiga.Tests.DiskObjectTests;

using System;
using System.Collections.Generic;
using System.IO;
using DataTypes.DiskObjects.ColorIcons;
using Xunit;

public class GivenRleStreamWriter
{
    [Fact]
    public void WhenWritingRepeatedDataThenRleCompressedDataMatches()
    {
        // arrange - data to rle compress
        const int bitDepth = 8;
        var data = new byte[] { 1, 255, 1, 1, 1, 1, 1, 1, 1, 7, 7, 7 };
        
        // arrange - expected rle compressed data
        var expectedRleCompressedData = new List<byte>
        {
            2, // rle block: copy 2 bytes
            1, // data
            255, // data
            1, // data
            256 - 6 + 1, // rle block: repeat 7 bytes
            1, // data
            256 - 3 + 1, // rle block: repeat 3 bytes
            7 // data
        };
        
        // act - write data
        var stream = new MemoryStream();
        var rleWriter = new RleStreamWriter(stream, bitDepth);
        foreach (var d in data)
        {
            rleWriter.Write(d);
        }
        rleWriter.Finish();
        
        // assert - rle compressed data matches expected
        var rleCompressedData = stream.ToArray();
        Assert.Equal(expectedRleCompressedData.Count, rleCompressedData.Length);
        Assert.Equal(expectedRleCompressedData, rleCompressedData);
    }
    
    [Fact]
    public void WhenWriting200RepeatedDataThenRleCompressedDataMatches()
    {
        // arrange - data to rle compress
        const int bitDepth = 8;
        var data = new byte[200];
        Array.Fill<byte>(data, 1);
        
        // arrange - expected rle compressed data
        var expectedRleCompressedData = new List<byte>
        {
            256 - 127 + 1, // rle block: repeat 127 bytes
            1, // data
            256 - 73 + 1, // rle block: repeat 73 bytes
            1 // data
        };
        
        // act - write data
        var stream = new MemoryStream();
        var rleWriter = new RleStreamWriter(stream, bitDepth);
        foreach (var d in data)
        {
            rleWriter.Write(d);
        }
        rleWriter.Finish();
        
        // assert - rle compressed data matches expected
        var rleCompressedData = stream.ToArray();
        Assert.Equal(expectedRleCompressedData.Count, rleCompressedData.Length);
        Assert.Equal(expectedRleCompressedData, rleCompressedData);
    }
    
    [Fact]
    public void WhenWriting128RepeatedDataThenRleCompressedDataMatches()
    {
        // arrange - data to rle compress
        const int bitDepth = 8;
        var data = new byte[128];
        Array.Fill<byte>(data, 1);
        
        // arrange - expected rle compressed data
        var expectedRleCompressedData = new List<byte>
        {
            256 - 127 + 1, // rle block: repeat 127 bytes
            1, // data
            0, // rle block: copy 1 byte
            1 // data
        };
        
        // act - write data
        var stream = new MemoryStream();
        var rleWriter = new RleStreamWriter(stream, bitDepth);
        foreach (var d in data)
        {
            rleWriter.Write(d);
        }
        rleWriter.Finish();
        
        // assert - rle compressed data matches expected
        var rleCompressedData = stream.ToArray();
        Assert.Equal(expectedRleCompressedData.Count, rleCompressedData.Length);
        Assert.Equal(expectedRleCompressedData, rleCompressedData);
    }
    
    [Fact]
    public void WhenWriting129RepeatedDataThenRleCompressedDataMatches()
    {
        // arrange - data to rle compress
        const int bitDepth = 8;
        var data = new byte[129];
        Array.Fill<byte>(data, 1);
        
        // arrange - expected rle compressed data
        var expectedRleCompressedData = new List<byte>
        {
            256 - 127 + 1, // rle block: repeat 127 bytes
            1, // data
            256 - 2 + 1, // rle block: repeat 2 bytes
            1 // data
        };
        
        // act - write data
        var stream = new MemoryStream();
        var rleWriter = new RleStreamWriter(stream, bitDepth);
        foreach (var d in data)
        {
            rleWriter.Write(d);
        }
        rleWriter.Finish();
        
        // assert - rle compressed data matches expected
        var rleCompressedData = stream.ToArray();
        Assert.Equal(expectedRleCompressedData.Count, rleCompressedData.Length);
        Assert.Equal(expectedRleCompressedData, rleCompressedData);
    }
}