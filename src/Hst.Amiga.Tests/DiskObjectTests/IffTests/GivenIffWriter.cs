using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.Iffs;
using Hst.Core.Converters;
using Xunit;

namespace Hst.Amiga.Tests.DiskObjectTests.IffTests;

public class GivenIffWriter
{
    [Fact]
    public async Task WhenBeginChunkAndAddEvenSizeData_ThenChunkDataIsWrittenAsIs()
    {
        // arrange
        await using var memoryStream = new MemoryStream();
        var iffWriter = new IffWriter(memoryStream);

        // act
        var chk1 = iffWriter.BeginChunk("CHK1");
        chk1.AddData(new byte[]{ 1, 2 });
        await iffWriter.EndChunk();

        // assert
        var expectedBytes = Encoding.ASCII.GetBytes("CHK1")
            .Concat(BigEndianConverter.ConvertUInt32ToBytes(2))
            .Concat(new byte[] { 1, 2 })
            .ToArray();
        var actualBytes = memoryStream.ToArray();
        Assert.Equal(expectedBytes, actualBytes);
    }

    [Fact]
    public async Task WhenBeginChunkAndAddOddSizeData_ThenChunkDataIsZeroPadded()
    {
        // arrange
        await using var memoryStream = new MemoryStream();
        var iffWriter = new IffWriter(memoryStream);

        // act
        var chk1 = iffWriter.BeginChunk("CHK1");
        chk1.AddData(new byte[]{ 1, 2, 3 });
        await iffWriter.EndChunk();

        // assert
        var expectedBytes = Encoding.ASCII.GetBytes("CHK1")
            .Concat(BigEndianConverter.ConvertUInt32ToBytes(4))
            .Concat(new byte[] { 1, 2, 3, 0 })
            .ToArray();
        var actualBytes = memoryStream.ToArray();
        Assert.Equal(expectedBytes, actualBytes);
    }

    [Fact]
    public async Task WhenBeginChunkMultipleTimes_ThenNestedChunksAreCreated()
    {
        // arrange
        await using var memoryStream = new MemoryStream();
        var iffWriter = new IffWriter(memoryStream);

        // act
        iffWriter.BeginChunk("CHK1");
        var chk2 = iffWriter.BeginChunk("CHK2");
        chk2.AddData(new byte[]{ 1, 2, 3, 4 });
        await iffWriter.EndChunk();
        await iffWriter.EndChunk();

        // assert
        var expectedBytes = Encoding.ASCII.GetBytes("CHK1")
            .Concat(BigEndianConverter.ConvertUInt32ToBytes(12))
            .Concat(Encoding.ASCII.GetBytes("CHK2"))
            .Concat(BigEndianConverter.ConvertUInt32ToBytes(4))
            .Concat(new byte[] { 1, 2, 3, 4 })
            .ToArray();
        var actualBytes = memoryStream.ToArray();
        Assert.Equal(expectedBytes, actualBytes);
    }
}