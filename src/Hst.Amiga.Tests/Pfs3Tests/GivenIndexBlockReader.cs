namespace Hst.Amiga.Tests.Pfs3Tests;

using Core.Converters;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using Xunit;

public class GivenIndexBlockReader
{
    [Fact]
    public void WhenReadBlockBytesThenIndexBlockMatch()
    {
        // arrange - create global data
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            }
        };

        // arrange - create index block bytes to read
        var blockBytes = new byte[g.RootBlock.ReservedBlksize];

        // arrange - set index block properties in block bytes
        BigEndianConverter.ConvertUInt16ToBytes(Constants.IBLKID, blockBytes, 0x0); // id
        BigEndianConverter.ConvertUInt32ToBytes(1, blockBytes, 0x4); // datestamp
        BigEndianConverter.ConvertUInt32ToBytes(2, blockBytes, 0x8); // seqnr

        var offset = 0xc;
        var indexes = (g.RootBlock.ReservedBlksize - Amiga.SizeOf.UWord * 2 - Amiga.SizeOf.ULong * 2) /
                      Amiga.SizeOf.Long;
        for (uint i = 1; i <= indexes; i++)
        {
            BigEndianConverter.ConvertUInt32ToBytes(i, blockBytes, offset);
            offset += Amiga.SizeOf.ULong;
        }
        
        // act - parse index block bytes
        var actualIndexBlock = IndexBlockReader.Parse(blockBytes, g);
            
        // assert - index block matches
        Assert.Equal(Constants.IBLKID, actualIndexBlock.id);
        Assert.Equal(1U, actualIndexBlock.datestamp);
        Assert.Equal(2U, actualIndexBlock.seqnr);
        Assert.Equal(indexes, actualIndexBlock.index.Length);
        for (var i = 0; i < indexes; i++)
        {
            Assert.Equal(i + 1, actualIndexBlock.index[i]);
        }
    }
}