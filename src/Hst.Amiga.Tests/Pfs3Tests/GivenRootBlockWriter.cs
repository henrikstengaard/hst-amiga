namespace Hst.Amiga.Tests.Pfs3Tests;

using Core.Converters;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using Xunit;
using DateHelper = DateHelper;

public class GivenRootBlockWriter
{
    [Fact]
    public void WhenWriteRootBlockThenBlockBytesMatch()
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

        // arrange - create root block to write
        var rootBlock = new RootBlock
        {
            DiskType = Constants.ID_PFS_DISK,
            Options = RootBlock.DiskOptionsEnum.MODE_HARDDISK,
            Datestamp = 2,
            CreationDate = DateHelper.ConvertToDate(3, 4, 5),
            Protection = 6,
            DiskName = "UnitTest",
            LastReserved = 7,
            FirstReserved = 8,
            ReservedFree = 9,
            ReservedBlksize = 1024,
            RblkCluster = 11,
            BlocksFree = 12,
            AlwaysFree = 13,
            RovingPtr = 14,
            DelDir = 15,
            DiskSize = 16,
            Extension = 17
        };
        
        for (var i = 0; i < SizeOf.RootBlock.IdxUnion; i++)
        {
            rootBlock.idx.union[i] = (uint)(i + 1);
        }

        // act - build root block bytes
        var blockBytes = RootBlockWriter.BuildBlock(rootBlock, g);

        // assert - block bytes match root block
        Assert.Equal(512, blockBytes.Length);
        Assert.Equal(rootBlock.DiskType, BigEndianConverter.ConvertBytesToInt32(blockBytes, 0));
        Assert.Equal((uint)rootBlock.Options, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 4));
        Assert.Equal(rootBlock.Datestamp, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 8));
        Assert.Equal(rootBlock.Protection, BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x12));
        Assert.Equal((byte)rootBlock.DiskName.Length, blockBytes[0x14]);
        Assert.Equal(rootBlock.DiskName, AmigaTextHelper.GetString(blockBytes, 0x15, rootBlock.DiskName.Length));
        Assert.Equal(rootBlock.LastReserved, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x34));
        Assert.Equal(rootBlock.FirstReserved, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x38));
        Assert.Equal(rootBlock.ReservedFree, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x3c));
        Assert.Equal(rootBlock.ReservedBlksize, BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x40));
        Assert.Equal(rootBlock.RblkCluster, BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x42));
        Assert.Equal(rootBlock.BlocksFree, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x44));
        Assert.Equal(rootBlock.AlwaysFree, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x48));
        Assert.Equal(rootBlock.RovingPtr, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4c));
        Assert.Equal(rootBlock.DelDir, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x50));
        Assert.Equal(rootBlock.DiskSize, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x54));
        Assert.Equal(rootBlock.Extension, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x58));

        // assert - root block idx match
        var offset = 0x60;
        for (var i = 0; i < SizeOf.RootBlock.IdxUnion; i++)
        {
            Assert.Equal((uint)(i + 1), BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset));
            offset += Amiga.SizeOf.ULong;
        }
    }
}