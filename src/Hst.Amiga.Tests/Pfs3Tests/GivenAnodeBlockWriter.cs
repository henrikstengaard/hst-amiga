namespace Hst.Amiga.Tests.Pfs3Tests;

using Core.Converters;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using Xunit;

public class GivenAnodeBlockWriter
{
    [Fact]
    public void WhenWriteAnodeBlockThenBlockBytesMatch()
    {
        // arrange - create global data
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            }
        };

        // arrange - create anode entries
        var anode1 = new anode
        {
            clustersize = 3,
            blocknr = 4,
            next = 5
        };
        var anode2 = new anode
        {
            clustersize = 6,
            blocknr = 7,
            next = 8
        };
        
        // arrange - create anode block to write
        var anodeBlock = new anodeblock(g)
        {
            datestamp = 1,
            seqnr = 2,
            nodes = new [] { anode1, anode2 }
        };
        
        // act - build anode block bytes
        var blockBytes = AnodeBlockWriter.BuildBlock(anodeBlock, g);

        // assert - block bytes match reserved block size and anode block properties
        Assert.Equal(g.RootBlock.ReservedBlksize, blockBytes.Length);
        Assert.Equal(Constants.ABLKID, BigEndianConverter.ConvertBytesToUInt16(blockBytes));
        Assert.Equal(anodeBlock.datestamp, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4));
        Assert.Equal(anodeBlock.seqnr, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8));

        // assert - anode 1 matches
        Assert.Equal(anode1.clustersize, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10));
        Assert.Equal(anode1.blocknr, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x14));
        Assert.Equal(anode1.next, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x18));
        
        // assert - anode 2 matches
        Assert.Equal(anode2.clustersize, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1c));
        Assert.Equal(anode2.blocknr, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x20));
        Assert.Equal(anode2.next, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x24));
    }

    [Fact]
    public void WhenWriteAndReadAnodeBlockThenAnodeBlockMatch()
    {
        // arrange - create global data
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            }
        };

        // arrange - create anode entries
        var anode1 = new anode
        {
            clustersize = 3,
            blocknr = 4,
            next = 5
        };
        var anode2 = new anode
        {
            clustersize = 6,
            blocknr = 7,
            next = 8
        };
        
        // arrange - create anode block to write
        var anodeBlock = new anodeblock(g)
        {
            datestamp = 1,
            seqnr = 2,
            nodes = new [] { anode1, anode2 }
        };

        // act - build anode block bytes
        var blockBytes = AnodeBlockWriter.BuildBlock(anodeBlock, g);
        
        // act - read anode block bytes
        var actualAnodeBlock = AnodeBlockReader.Parse(blockBytes, g);
        
        // assert - anode block matches
        Assert.Equal(anodeBlock.datestamp, actualAnodeBlock.datestamp);
        Assert.Equal(anodeBlock.seqnr, actualAnodeBlock.seqnr);

        // assert - anode 1 matches
        var actualAnode1 = actualAnodeBlock.nodes[0];
        Assert.NotNull(actualAnode1);
        Assert.Equal(anode1.clustersize, actualAnode1.clustersize);
        Assert.Equal(anode1.blocknr, actualAnode1.blocknr);
        Assert.Equal(anode1.next, actualAnode1.next);
        
        // assert - anode 2 matches
        var actualAnode2 = actualAnodeBlock.nodes[1];
        Assert.NotNull(actualAnode2);
        Assert.Equal(anode2.clustersize, actualAnode2.clustersize);
        Assert.Equal(anode2.blocknr, actualAnode2.blocknr);
        Assert.Equal(anode2.next, actualAnode2.next);
    }
}