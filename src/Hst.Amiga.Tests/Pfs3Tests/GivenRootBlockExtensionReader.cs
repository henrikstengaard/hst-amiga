namespace Hst.Amiga.Tests.Pfs3Tests;

using System;
using Core.Converters;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using Xunit;

public class GivenRootBlockExtensionReader
{
    [Fact]
    public void WhenReadBlockBytesThenRootExtensionBlockMatch()
    {
        // arrange - create global data
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            }
        };

        // arrange - create root block extension bytes to read
        var blockBytes = new byte[g.RootBlock.ReservedBlksize];

        // arrange - root block extension properties in block bytes
        var date = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        BigEndianConverter.ConvertUInt16ToBytes(Constants.EXTENSIONID, blockBytes, 0x0); // id
        BigEndianConverter.ConvertUInt32ToBytes(1, blockBytes, 0x4); // ext_options
        BigEndianConverter.ConvertUInt32ToBytes(2, blockBytes, 0x8); // datestamp
        BigEndianConverter.ConvertUInt32ToBytes(3, blockBytes, 0xc); // pfs2version
        DateHelper.WriteDate(date, blockBytes, 0x10); // RootDate
        DateHelper.WriteDate(date.AddDays(1), blockBytes, 0x16); // VolumeDate
        
        BigEndianConverter.ConvertUInt32ToBytes(4, blockBytes, 0x1c); // operation_id
        BigEndianConverter.ConvertUInt32ToBytes(5, blockBytes, 0x20); // argument1
        BigEndianConverter.ConvertUInt32ToBytes(6, blockBytes, 0x24); // argument2
        BigEndianConverter.ConvertUInt32ToBytes(7, blockBytes, 0x28); // argument3
        
        BigEndianConverter.ConvertUInt32ToBytes(8, blockBytes, 0x2c); // reserved_roving
        BigEndianConverter.ConvertUInt16ToBytes(9, blockBytes, 0x30); // rovingbit
        BigEndianConverter.ConvertUInt16ToBytes(10, blockBytes, 0x32); // curranseqnr
        BigEndianConverter.ConvertUInt16ToBytes(11, blockBytes, 0x34); // deldirroving
        BigEndianConverter.ConvertUInt16ToBytes(12, blockBytes, 0x36); // deldirsize
        BigEndianConverter.ConvertUInt16ToBytes(13, blockBytes, 0x38); // fnsize

        var offset = 0x40;
        for (var i = 0; i < Constants.MAXSUPER + 1; i++)
        {
            BigEndianConverter.ConvertUInt32ToBytes(14, blockBytes, offset); // superindex
            offset += Amiga.SizeOf.ULong;
        }

        BigEndianConverter.ConvertUInt16ToBytes(15, blockBytes, 0x80); // dd_uid
        BigEndianConverter.ConvertUInt16ToBytes(16, blockBytes, 0x82); // dd_gid
        BigEndianConverter.ConvertUInt32ToBytes(17, blockBytes, 0x84); // dd_protection
        DateHelper.WriteDate(date.AddDays(2), blockBytes, 0x88); // dd_creationdate
        
        offset = 0x90;
        for (var i = 0; i < 32; i++)
        {
            BigEndianConverter.ConvertUInt32ToBytes(18, blockBytes, offset); // deldir
            offset += Amiga.SizeOf.ULong;
        }
        
        // act - parse root block extension block bytes
        var actualRootBlockExtension = RootBlockExtensionReader.Parse(blockBytes);
            
        // assert - root block extension matches
        Assert.Equal(Constants.EXTENSIONID, actualRootBlockExtension.id);
        Assert.Equal(1U, actualRootBlockExtension.ext_options);
        Assert.Equal(2U, actualRootBlockExtension.datestamp);
        Assert.Equal(3U, actualRootBlockExtension.pfs2version);
        Assert.Equal(date, actualRootBlockExtension.RootDate);
        Assert.Equal(date.AddDays(1), actualRootBlockExtension.VolumeDate);
        Assert.Equal(4U, actualRootBlockExtension.tobedone.operation_id);
        Assert.Equal(5U, actualRootBlockExtension.tobedone.argument1);
        Assert.Equal(6U, actualRootBlockExtension.tobedone.argument2);
        Assert.Equal(7U, actualRootBlockExtension.tobedone.argument3);
        Assert.Equal(8U, actualRootBlockExtension.reserved_roving);
        Assert.Equal(9U, actualRootBlockExtension.rovingbit);
        Assert.Equal(10U, actualRootBlockExtension.curranseqnr);
        Assert.Equal(11U, actualRootBlockExtension.deldirroving);
        Assert.Equal(12U, actualRootBlockExtension.deldirsize);
        Assert.Equal(13U, actualRootBlockExtension.fnsize);
        
        for (var i = 0; i < Constants.MAXSUPER + 1; i++)
        {
            Assert.Equal(14U, actualRootBlockExtension.superindex[i]);
        }

        Assert.Equal(15U, actualRootBlockExtension.dd_uid);
        Assert.Equal(16U, actualRootBlockExtension.dd_gid);
        Assert.Equal(17U, actualRootBlockExtension.dd_protection);
        Assert.Equal(date.AddDays(2), actualRootBlockExtension.dd_creationdate);
        
        for (var i = 0; i < 32; i++)
        {
            Assert.Equal(18U, actualRootBlockExtension.deldir[i]);
        }
    }
}