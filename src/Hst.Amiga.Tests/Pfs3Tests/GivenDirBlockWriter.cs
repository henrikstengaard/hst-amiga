namespace Hst.Amiga.Tests.Pfs3Tests;

using System;
using System.Collections.Generic;
using Core.Converters;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using Xunit;

public class GivenDirBlockWriter
{
    [Fact]
    public void WhenWriteDirBlockThenBlockBytesMatch()
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

        // arrange - create dir entry
        var dirEntryName = "File";
        var date = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dirEntry = new direntry(0, Constants.ST_FILE, 0, 0, 0, date, dirEntryName, string.Empty, new extrafields(),
            g);
        
        // arrange - create dir block to write
        var dirBlock = new dirblock(g)
        {
            id = Constants.DBLKID,
            datestamp = 1,
            anodenr = 2,
            parent = 3,
            DirEntries = new List<direntry>
            {
                dirEntry
            }
        };
        
        // act - build dir block bytes
        var blockBytes = DirBlockWriter.BuildBlock(dirBlock, g);

        // assert - block bytes match reserved block size and id, datestamp and seqnr match
        Assert.Equal(g.RootBlock.ReservedBlksize, blockBytes.Length);
        Assert.Equal(Constants.DBLKID, BigEndianConverter.ConvertBytesToUInt16(blockBytes));
        Assert.Equal(dirBlock.datestamp, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4));
        Assert.Equal(dirBlock.anodenr, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc));
        Assert.Equal(dirBlock.parent, BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10));

        // assert - dir entry matches
        var actualDirEntry = DirEntryReader.Read(blockBytes, 0x14, g);
        Assert.NotNull(actualDirEntry);
        Assert.Equal(dirEntry.type, actualDirEntry.type);
        Assert.Equal(dirEntry.Name, actualDirEntry.Name);
        Assert.Equal(dirEntry.CreationDate, actualDirEntry.CreationDate);
    }
}