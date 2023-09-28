namespace Hst.Amiga.Tests.Pfs3Tests;

using Core.Converters;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using Xunit;

public class GivenIndexBlockWriter
{
    [Fact]
    public void WhenWriteIndexBlockThenBlockBytesMatch()
    {
        // arrange - create global data
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            }
        };

        // arrange - create index block to write
        var indexCount = (g.RootBlock.ReservedBlksize - Amiga.SizeOf.UWord * 2 - Amiga.SizeOf.ULong * 2) /
                      Amiga.SizeOf.Long;
        var indexes = new int[indexCount];
        for (var i = 0; i < indexCount; i++)
        {
            indexes[i] = i + 1;
        }
        var indexBlock = new indexblock(g)
        {
            id = Constants.IBLKID,
            datestamp = 1,
            seqnr = 2,
            index = indexes
        };
        
        // act - build index block bytes
        var blockBytes = IndexBlockWriter.BuildBlock(indexBlock, g);

        // assert - block bytes match reserved block size and anode block properties
        Assert.Equal(g.RootBlock.ReservedBlksize, blockBytes.Length);
        Assert.Equal(Constants.IBLKID, BigEndianConverter.ConvertBytesToUInt16(blockBytes));
        Assert.Equal(indexBlock.datestamp, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4));
        Assert.Equal(indexBlock.seqnr, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8));

        var offset = 0xc;
        for (var i = 0; i < indexCount; i++)
        {
            Assert.Equal(indexBlock.index[i], BigEndianConverter.ConvertBytesToInt32(blockBytes, offset));
            offset += Amiga.SizeOf.Long;
        }
    }

    [Fact]
    public void WhenWriteAndReadIndexBlockThenIndexBlockMatch()
    {
        // arrange - create global data
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            }
        };

        // arrange - create index block to write
        var indexCount = (g.RootBlock.ReservedBlksize - Amiga.SizeOf.UWord * 2 - Amiga.SizeOf.ULong * 2) /
                         Amiga.SizeOf.Long;
        var indexes = new int[indexCount];
        for (var i = 0; i < indexCount; i++)
        {
            indexes[i] = i + 1;
        }
        var indexBlock = new indexblock(g)
        {
            id = Constants.IBLKID,
            datestamp = 1,
            seqnr = 2,
            index = indexes
        };

        // act - build index block bytes
        var blockBytes = IndexBlockWriter.BuildBlock(indexBlock, g);
        
        // act - read index block bytes
        var actualIndexBlock = IndexBlockReader.Parse(blockBytes, g);
        
        // assert - index block matches
        Assert.Equal(Constants.IBLKID, actualIndexBlock.id);
        Assert.Equal(indexBlock.datestamp, actualIndexBlock.datestamp);
        Assert.Equal(indexBlock.seqnr, actualIndexBlock.seqnr);
        Assert.Equal(indexCount, actualIndexBlock.index.Length);
        Assert.Equal(indexBlock.index, actualIndexBlock.index);
    }
}