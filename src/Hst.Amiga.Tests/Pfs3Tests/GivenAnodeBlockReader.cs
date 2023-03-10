namespace Hst.Amiga.Tests.Pfs3Tests;

using Core.Converters;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using Xunit;

public class GivenAnodeBlockReader
{
    [Fact]
    public void WhenReadBlockBytesThenAnodeBlockMatch()
    {
        // arrange - create global data
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            }
        };

        // arrange - create anode block bytes to read
        var blockBytes = new byte[g.RootBlock.ReservedBlksize];

        // arrange - set anode block id, datestamp and seqnr in block bytes
        BigEndianConverter.ConvertUInt16ToBytes(Constants.ABLKID, blockBytes, 0x0); // id
        BigEndianConverter.ConvertUInt32ToBytes(1, blockBytes, 0x4); // datestamp
        BigEndianConverter.ConvertUInt32ToBytes(2, blockBytes, 0x8); // seqnr

        // arrange - set anode entry 1
        BigEndianConverter.ConvertUInt32ToBytes(3, blockBytes, 0x10); // clustersize
        BigEndianConverter.ConvertUInt32ToBytes(4, blockBytes, 0x14); // blocknr
        BigEndianConverter.ConvertUInt32ToBytes(5, blockBytes, 0x18); // next

        // arrange - set anode entry 2
        BigEndianConverter.ConvertUInt32ToBytes(6, blockBytes, 0x1c); // clustersize
        BigEndianConverter.ConvertUInt32ToBytes(7, blockBytes, 0x20); // blocknr
        BigEndianConverter.ConvertUInt32ToBytes(8, blockBytes, 0x24); // next
        
        // act - parse anode block bytes
        var actualAnodeBlock = AnodeBlockReader.Parse(blockBytes, g);
            
        // assert - anode block matches
        Assert.Equal(Constants.ABLKID, actualAnodeBlock.id);
        Assert.Equal(1U, actualAnodeBlock.datestamp);
        Assert.Equal(2U, actualAnodeBlock.seqnr);

        // assert - node 1 match
        var node1 = actualAnodeBlock.nodes[0];
        Assert.Equal(3U, node1.clustersize);
        Assert.Equal(4U, node1.blocknr);
        Assert.Equal(5U, node1.next);
        
        // assert - node 2 match
        var node2 = actualAnodeBlock.nodes[1];
        Assert.Equal(6U, node2.clustersize);
        Assert.Equal(7U, node2.blocknr);
        Assert.Equal(8U, node2.next);
    }
}