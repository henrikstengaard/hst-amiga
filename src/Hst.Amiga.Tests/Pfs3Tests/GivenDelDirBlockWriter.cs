namespace Hst.Amiga.Tests.Pfs3Tests;

using System;
using System.Threading.Tasks;
using Core.Converters;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using Xunit;
using SizeOf = Amiga.SizeOf;

public class GivenDelDirBlockWriter
{
    [Fact]
    public void WhenWriteDelDirBlockThenBlockBytesMatch()
    {
        // arrange - create global data
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            }
        };

        // arrange - create del dir entries
        var date = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var delDirEntry1 = new deldirentry
        {
            anodenr = 1,
            fsize = 2,
            filename = "File1",
            fsizex = 3,
            CreationDate = date
        };
        var delDirEntry2 = new deldirentry
        {
            anodenr = 4,
            fsize = 5,
            filename = "File2",
            fsizex = 6,
            CreationDate = date
        };
        
        // arrange - create del dir block to write
        var delDirBlock = new deldirblock(g)
        {
            datestamp = 1,
            seqnr = 2,
            uid = 3,
            gid = 4,
            protection = 5,
            CreationDate = date,
            entries = new [] { delDirEntry1, delDirEntry2 }
        };
        
        // act - build del dir block bytes
        var blockBytes = DelDirBlockWriter.BuildBlock(delDirBlock, g);

        // assert - block bytes match reserved block size and del dir properties
        Assert.Equal(g.RootBlock.ReservedBlksize, blockBytes.Length);
        Assert.Equal(Constants.DELDIRID, BigEndianConverter.ConvertBytesToUInt16(blockBytes));
        Assert.Equal(delDirBlock.datestamp, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4));
        Assert.Equal(delDirBlock.seqnr, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8));
        Assert.Equal(delDirBlock.uid, BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x12));
        Assert.Equal(delDirBlock.gid, BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x14));
        Assert.Equal(delDirBlock.protection, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x16));
        Assert.Equal(delDirBlock.CreationDate, DateHelper.ReadDate(blockBytes, 0x1a));

        // assert - del dir entry 1 matches
        var actualDelDirEntry1 = DelDirEntryReader.Read(blockBytes, 0x20);
        Assert.NotNull(actualDelDirEntry1);
        Assert.Equal(delDirEntry1.anodenr, actualDelDirEntry1.anodenr);
        Assert.Equal(delDirEntry1.fsize, actualDelDirEntry1.fsize);
        Assert.Equal(delDirEntry1.filename, actualDelDirEntry1.filename);
        Assert.Equal(delDirEntry1.fsizex, actualDelDirEntry1.fsizex);
        Assert.Equal(delDirEntry1.CreationDate, actualDelDirEntry1.CreationDate);
        
        // assert - del dir entry 2 matches
        var actualDelDirEntry2 = DelDirEntryReader.Read(blockBytes, 0x20 + FileSystems.Pfs3.SizeOf.DelDirEntry.Struct);
        Assert.NotNull(actualDelDirEntry2);
        Assert.Equal(delDirEntry2.anodenr, actualDelDirEntry2.anodenr);
        Assert.Equal(delDirEntry2.fsize, actualDelDirEntry2.fsize);
        Assert.Equal(delDirEntry2.filename, actualDelDirEntry2.filename);
        Assert.Equal(delDirEntry2.fsizex, actualDelDirEntry2.fsizex);
        Assert.Equal(delDirEntry2.CreationDate, actualDelDirEntry2.CreationDate);
    }

    [Fact]
    public void WhenWriteAndReadDelDirBlockThenDelDirBlockMatch()
    {
        // arrange - create global data
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            }
        };

        // arrange - create del dir entries
        var date = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var delDirEntry1 = new deldirentry
        {
            anodenr = 1,
            fsize = 2,
            filename = "File1",
            fsizex = 3,
            CreationDate = date
        };
        var delDirEntry2 = new deldirentry
        {
            anodenr = 4,
            fsize = 5,
            filename = "File2",
            fsizex = 6,
            CreationDate = date
        };

        // arrange - create del dir block to write
        var delDirBlock = new deldirblock(g)
        {
            datestamp = 1,
            seqnr = 2,
            uid = 3,
            gid = 4,
            protection = 5,
            CreationDate = date,
            entries = new[] { delDirEntry1, delDirEntry2 }
        };

        // act - build del dir block bytes
        var blockBytes = DelDirBlockWriter.BuildBlock(delDirBlock, g);
        
        // act - read del dir block bytes
        var actualDelDirBlock = DelDirBlockReader.Parse(blockBytes, g);
        
        // assert - del dir block matches
        Assert.Equal(delDirBlock.datestamp, actualDelDirBlock.datestamp);
        Assert.Equal(delDirBlock.seqnr, actualDelDirBlock.seqnr);
        Assert.Equal(delDirBlock.uid, actualDelDirBlock.uid);
        Assert.Equal(delDirBlock.gid, actualDelDirBlock.gid);
        Assert.Equal(delDirBlock.protection, actualDelDirBlock.protection);
        Assert.Equal(delDirBlock.CreationDate, actualDelDirBlock.CreationDate);

        // assert - del dir entry 1 matches
        var actualDelDirEntry1 = actualDelDirBlock.entries[0];
        Assert.NotNull(actualDelDirEntry1);
        Assert.Equal(delDirEntry1.anodenr, actualDelDirEntry1.anodenr);
        Assert.Equal(delDirEntry1.fsize, actualDelDirEntry1.fsize);
        Assert.Equal(delDirEntry1.filename, actualDelDirEntry1.filename);
        Assert.Equal(delDirEntry1.fsizex, actualDelDirEntry1.fsizex);
        Assert.Equal(delDirEntry1.CreationDate, actualDelDirEntry1.CreationDate);
        
        // assert - del dir entry 2 matches
        var actualDelDirEntry2 = actualDelDirBlock.entries[1];
        Assert.NotNull(actualDelDirEntry2);
        Assert.Equal(delDirEntry2.anodenr, actualDelDirEntry2.anodenr);
        Assert.Equal(delDirEntry2.fsize, actualDelDirEntry2.fsize);
        Assert.Equal(delDirEntry2.filename, actualDelDirEntry2.filename);
        Assert.Equal(delDirEntry2.fsizex, actualDelDirEntry2.fsizex);
        Assert.Equal(delDirEntry2.CreationDate, actualDelDirEntry2.CreationDate);
    }
}