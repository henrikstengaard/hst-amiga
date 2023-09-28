namespace Hst.Amiga.Tests.Pfs3Tests;

using System;
using Core.Converters;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using Xunit;
using DateHelper = DateHelper;

public class GivenRootBlockReader
{
    [Fact]
    public void WhenReadBlockBytesThenRootBlockMatches()
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

        // arrange - create root block bytes to read
        var blockBytes = new byte[g.RootBlock.ReservedBlksize];

        // arrange - create root block bytes
        BigEndianConverter.ConvertUInt32ToBytes(Constants.ID_PFS_DISK, blockBytes, 0); // disk type
        BigEndianConverter.ConvertUInt32ToBytes(1, blockBytes, 4); // options
        BigEndianConverter.ConvertUInt32ToBytes(2, blockBytes, 8); // datestamp
        BigEndianConverter.ConvertUInt16ToBytes(3, blockBytes, 0xc); // creation date days since 1 jan 1978
        BigEndianConverter.ConvertUInt16ToBytes(4, blockBytes, 0xe); // creation date minutes past midnight
        BigEndianConverter.ConvertUInt16ToBytes(5, blockBytes, 0x10); // creation date ticks past minute
        BigEndianConverter.ConvertUInt16ToBytes(6, blockBytes, 0x12); // protection

        var diskName = "UnitTest";
        blockBytes[0x14] = (byte)diskName.Length; // disk name length
        var diskNameBytes = AmigaTextHelper.GetBytes(diskName);
        Array.Copy(diskNameBytes, 0, blockBytes, 0x15, diskNameBytes.Length);

        BigEndianConverter.ConvertUInt32ToBytes(7, blockBytes, 0x34); // last reserved
        BigEndianConverter.ConvertUInt32ToBytes(8, blockBytes, 0x38); // first reserved
        BigEndianConverter.ConvertUInt32ToBytes(9, blockBytes, 0x3c); // reserved free
        BigEndianConverter.ConvertUInt16ToBytes(1024, blockBytes, 0x40); // reserved block size
        BigEndianConverter.ConvertUInt16ToBytes(11, blockBytes, 0x42); // reserved block cluster
        BigEndianConverter.ConvertUInt32ToBytes(12, blockBytes, 0x44); // blocks free
        BigEndianConverter.ConvertUInt32ToBytes(13, blockBytes, 0x48); // always free
        BigEndianConverter.ConvertUInt32ToBytes(14, blockBytes, 0x4c); // roving ptr
        BigEndianConverter.ConvertUInt32ToBytes(15, blockBytes, 0x50); // deldir location
        BigEndianConverter.ConvertUInt32ToBytes(16, blockBytes, 0x54); // disk size in sectors
        BigEndianConverter.ConvertUInt32ToBytes(17, blockBytes, 0x58); // root block extension

        // arrange - set root block idx
        var offset = 0x60;
        for (var i = 0; i < SizeOf.RootBlock.IdxUnion; i++)
        {
            BigEndianConverter.ConvertUInt32ToBytes((uint)(i + 1), blockBytes, offset);
            offset += Amiga.SizeOf.ULong;
        }        

        // act - parse bitmap block bytes
        var rootBlock = RootBlockReader.Parse(blockBytes);

        // assert - root block matches
        Assert.Equal(RootBlock.DiskOptionsEnum.MODE_HARDDISK, rootBlock.Options);
        Assert.Equal(2U, rootBlock.Datestamp);
        Assert.Equal(DateHelper.ConvertToDate(3, 4, 5), rootBlock.CreationDate);
        Assert.Equal(6U, rootBlock.Protection);
        Assert.Equal(diskName, rootBlock.DiskName);
        Assert.Equal(7U, rootBlock.LastReserved);
        Assert.Equal(8U, rootBlock.FirstReserved);
        Assert.Equal(9U, rootBlock.ReservedFree);
        Assert.Equal(1024U, rootBlock.ReservedBlksize);
        Assert.Equal(11U, rootBlock.RblkCluster);
        Assert.Equal(12U, rootBlock.BlocksFree);
        Assert.Equal(13U, rootBlock.AlwaysFree);
        Assert.Equal(14U, rootBlock.RovingPtr);
        Assert.Equal(15U, rootBlock.DelDir);
        Assert.Equal(16U, rootBlock.DiskSize);
        Assert.Equal(17U, rootBlock.Extension);
        
        // assert - root block idx matches
        Assert.Equal(SizeOf.RootBlock.IdxUnion, rootBlock.idx.union.Length);
        for (var i = 0; i < SizeOf.RootBlock.IdxUnion; i++)
        {
            Assert.Equal((uint)(i + 1), rootBlock.idx.union[i]);
        }        
    }
}