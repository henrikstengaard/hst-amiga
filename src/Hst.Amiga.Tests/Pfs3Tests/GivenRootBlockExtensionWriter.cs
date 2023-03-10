namespace Hst.Amiga.Tests.Pfs3Tests;

using System;
using Core.Converters;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using Xunit;

public class GivenRootBlockExtensionWriter
{
    [Fact]
    public void WhenWriteRootBlockExtensionThenBlockBytesMatch()
    {
        // arrange - create global data
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            }
        };

        // arrange - create root block extension to write
        var date = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var superIndexes = new uint[Constants.MAXSUPER + 1];
        Array.Fill<uint>(superIndexes, 14);
        var delDirs = new uint[32];
        Array.Fill<uint>(delDirs, 18);
        var rootBlockExtension = new rootblockextension
        {
            ext_options = 1,
            datestamp = 2,
            pfs2version = 3,
            RootDate = date,
            VolumeDate = date.AddDays(1),
            tobedone = new postponed_op
            {
                operation_id = 4,
                argument1 = 5,
                argument2 = 6,
                argument3 = 7
            },
            reserved_roving = 8,
            rovingbit = 9,
            curranseqnr = 10,
            deldirroving = 11,
            deldirsize = 12,
            fnsize = 13,
            superindex = superIndexes,
            dd_gid = 15,
            dd_uid = 16,
            dd_protection = 17,
            dd_creationdate = date.AddDays(2),
            deldir = delDirs
        };
        
        // act - build root block extension bytes
        var blockBytes = RootBlockExtensionWriter.BuildBlock(rootBlockExtension, g);

        // assert - block bytes match root block extension properties
        Assert.Equal(g.RootBlock.ReservedBlksize, blockBytes.Length);
        Assert.Equal(Constants.EXTENSIONID, BigEndianConverter.ConvertBytesToUInt16(blockBytes));
        Assert.Equal(rootBlockExtension.ext_options, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4));
        Assert.Equal(rootBlockExtension.datestamp, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8));
        Assert.Equal(rootBlockExtension.pfs2version, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc));
        Assert.Equal(rootBlockExtension.RootDate, DateHelper.ReadDate(blockBytes, 0x10));
        Assert.Equal(rootBlockExtension.VolumeDate, DateHelper.ReadDate(blockBytes, 0x16));
        Assert.Equal(rootBlockExtension.tobedone.operation_id, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1c));
        Assert.Equal(rootBlockExtension.tobedone.argument1, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x20));
        Assert.Equal(rootBlockExtension.tobedone.argument2, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x24));
        Assert.Equal(rootBlockExtension.tobedone.argument3, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x28));
        Assert.Equal(rootBlockExtension.reserved_roving, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x2c));
        Assert.Equal(rootBlockExtension.rovingbit, BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x30));
        Assert.Equal(rootBlockExtension.curranseqnr, BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x32));
        Assert.Equal(rootBlockExtension.deldirroving, BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x34));
        Assert.Equal(rootBlockExtension.deldirsize, BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x36));
        Assert.Equal(rootBlockExtension.fnsize, BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x38));
        
        var offset = 0x40;
        for (var i = 0; i < Constants.MAXSUPER + 1; i++)
        {
            Assert.Equal(rootBlockExtension.superindex[i], BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset));
            offset += Amiga.SizeOf.ULong;
        }
        
        Assert.Equal(rootBlockExtension.dd_uid, BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x80));
        Assert.Equal(rootBlockExtension.dd_gid, BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x82));
        Assert.Equal(rootBlockExtension.dd_protection, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x84));
        Assert.Equal(rootBlockExtension.dd_creationdate, DateHelper.ReadDate(blockBytes, 0x88));
        
        offset = 0x90;
        for (var i = 0; i < 32; i++)
        {
            Assert.Equal(rootBlockExtension.deldir[i], BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset));
            offset += Amiga.SizeOf.ULong;
        }
    }

    [Fact]
    public void WhenWriteAndReadRootBlockExtensionThenRootBlockExtensionMatch()
    {
        // arrange - create global data
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            }
        };

        // arrange - create root block extension to write
        var date = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var superIndexes = new uint[Constants.MAXSUPER + 1];
        Array.Fill<uint>(superIndexes, 14);
        var delDirs = new uint[32];
        Array.Fill<uint>(delDirs, 18);
        var rootBlockExtension = new rootblockextension
        {
            ext_options = 1,
            datestamp = 2,
            pfs2version = 3,
            RootDate = date,
            VolumeDate = date.AddDays(1),
            tobedone = new postponed_op
            {
                operation_id = 4,
                argument1 = 5,
                argument2 = 6,
                argument3 = 7
            },
            reserved_roving = 8,
            rovingbit = 9,
            curranseqnr = 10,
            deldirroving = 11,
            deldirsize = 12,
            fnsize = 13,
            superindex = superIndexes,
            dd_gid = 15,
            dd_uid = 16,
            dd_protection = 17,
            dd_creationdate = date.AddDays(2),
            deldir = delDirs
        };

        // act - build root block extension bytes
        var blockBytes = RootBlockExtensionWriter.BuildBlock(rootBlockExtension, g);
        
        // act - read root block extension bytes
        var actualRootBlockExtension = RootBlockExtensionReader.Parse(blockBytes);
        
        // assert - root block extension matches
        Assert.Equal(rootBlockExtension.ext_options, actualRootBlockExtension.ext_options);
        Assert.Equal(rootBlockExtension.datestamp, actualRootBlockExtension.datestamp);
        Assert.Equal(rootBlockExtension.pfs2version, actualRootBlockExtension.pfs2version);
        Assert.Equal(rootBlockExtension.RootDate, actualRootBlockExtension.RootDate);
        Assert.Equal(rootBlockExtension.VolumeDate, actualRootBlockExtension.VolumeDate);
        Assert.Equal(rootBlockExtension.tobedone.operation_id, actualRootBlockExtension.tobedone.operation_id);
        Assert.Equal(rootBlockExtension.tobedone.argument1, actualRootBlockExtension.tobedone.argument1);
        Assert.Equal(rootBlockExtension.tobedone.argument2, actualRootBlockExtension.tobedone.argument2);
        Assert.Equal(rootBlockExtension.tobedone.argument3, actualRootBlockExtension.tobedone.argument3);
        Assert.Equal(rootBlockExtension.reserved_roving, actualRootBlockExtension.reserved_roving);
        Assert.Equal(rootBlockExtension.rovingbit, actualRootBlockExtension.rovingbit);
        Assert.Equal(rootBlockExtension.curranseqnr, actualRootBlockExtension.curranseqnr);
        Assert.Equal(rootBlockExtension.deldirroving, actualRootBlockExtension.deldirroving);
        Assert.Equal(rootBlockExtension.deldirsize, actualRootBlockExtension.deldirsize);
        Assert.Equal(rootBlockExtension.fnsize, actualRootBlockExtension.fnsize);
        
        Assert.Equal(Constants.MAXSUPER + 1, actualRootBlockExtension.superindex.Length);
        Assert.Equal(rootBlockExtension.superindex, actualRootBlockExtension.superindex);

        Assert.Equal(rootBlockExtension.dd_uid, actualRootBlockExtension.dd_uid);
        Assert.Equal(rootBlockExtension.dd_gid, actualRootBlockExtension.dd_gid);
        Assert.Equal(rootBlockExtension.dd_protection, actualRootBlockExtension.dd_protection);
        Assert.Equal(rootBlockExtension.dd_creationdate, actualRootBlockExtension.dd_creationdate);
        
        Assert.Equal(32, actualRootBlockExtension.deldir.Length);
        Assert.Equal(rootBlockExtension.deldir, actualRootBlockExtension.deldir);
    }
}