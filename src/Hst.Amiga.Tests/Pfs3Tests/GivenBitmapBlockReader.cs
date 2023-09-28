namespace Hst.Amiga.Tests.Pfs3Tests;

using Core.Converters;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using Xunit;

public class GivenBitmapBlockReader
{
    [Fact]
    public void WhenReadBlockBytesThenBitmapBlockMatch()
    {
        // arrange - create global data
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            }
        };
        g.glob_allocdata.longsperbmb = (uint)g.RootBlock.LongsPerBmb;

        // arrange - create bitmap block bytes to read
        var blockBytes = new byte[g.RootBlock.ReservedBlksize];

        // arrange - set bitmap block id, datestamp and seqnr in block bytes
        BigEndianConverter.ConvertUInt16ToBytes(1, blockBytes, 0); // id
        BigEndianConverter.ConvertUInt32ToBytes(2, blockBytes, 4); // datestamp
        BigEndianConverter.ConvertUInt32ToBytes(3, blockBytes, 8); // seqnr

        // arrange - set bitmaps in block bytes
        var offset = 0xc;
        for (var i = 0; i < g.glob_allocdata.longsperbmb; i++)
        {
            BigEndianConverter.ConvertUInt32ToBytes((uint)(i + 1), blockBytes, offset);
            offset += Amiga.SizeOf.ULong;
        }
        
        // act - parse bitmap block bytes
        var bitmapBlock = BitmapBlockReader.Parse(blockBytes, (int)g.glob_allocdata.longsperbmb);
        
        // assert - bitmap block id, datestamp and seqnr match
        Assert.Equal(1, bitmapBlock.id);
        Assert.Equal(2U, bitmapBlock.datestamp);
        Assert.Equal(3U, bitmapBlock.seqnr);

        // assert - bitmaps match
        for (var i = 0; i < bitmapBlock.bitmap.Length; i++)
        {
            Assert.Equal((uint)(i + 1), bitmapBlock.bitmap[i]);
        }
    }
}