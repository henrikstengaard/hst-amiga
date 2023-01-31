namespace Hst.Amiga.Tests.Pfs3Tests;

using Core.Converters;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using Xunit;

public class GivenBitmapBlockWriter
{
    [Fact]
    public void WhenWriteBitmapBlockThenBlockBytesMatch()
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

        // arrange - create bitmap block to write
        var bitmapBlock = new BitmapBlock(g.RootBlock.LongsPerBmb)
        {
            id = 1,
            datestamp = 2,
            seqnr = 3
        };
        for (var i = 0; i < bitmapBlock.bitmap.Length; i++)
        {
            bitmapBlock.bitmap[i] = (uint)(i + 1);
        }

        // act - build bitmap block bytes
        var blockBytes = BitmapBlockWriter.BuildBlock(bitmapBlock, g);

        // assert - block bytes match reserved block size and id, datestamp and seqnr match
        Assert.Equal(g.RootBlock.ReservedBlksize, blockBytes.Length);
        Assert.Equal(bitmapBlock.id, BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0));
        Assert.Equal(bitmapBlock.datestamp, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 4));
        Assert.Equal(bitmapBlock.seqnr, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 8));

        // assert - bitmaps match
        var offset = 0xc;
        foreach (var bitmap in bitmapBlock.bitmap)
        {
            Assert.Equal(bitmap, BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset));
            offset += Amiga.SizeOf.ULong;
        }
    }
}