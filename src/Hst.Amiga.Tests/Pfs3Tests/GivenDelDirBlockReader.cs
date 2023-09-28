namespace Hst.Amiga.Tests.Pfs3Tests;

using System;
using System.Linq;
using Core.Converters;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using Xunit;

public class GivenDelDirBlockReader
{
    [Fact]
    public void WhenReadBlockBytesThenDirBlockMatch()
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

        // arrange - create del dir block bytes to read
        var blockBytes = new byte[g.RootBlock.ReservedBlksize];

        // arrange - set bitmap block id, datestamp and seqnr in block bytes
        BigEndianConverter.ConvertUInt16ToBytes(Constants.DBLKID, blockBytes, 0); // id
        BigEndianConverter.ConvertUInt32ToBytes(1, blockBytes, 4); // datestamp
        BigEndianConverter.ConvertUInt32ToBytes(2, blockBytes, 0xc); // anodenr
        BigEndianConverter.ConvertUInt32ToBytes(3, blockBytes, 0x10); // parent

        // arrange - write file dir entry
        var dirEntryName = "File";
        var date = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dirEntry = new direntry(0, Constants.ST_FILE, 0, 0, 0, date, dirEntryName, string.Empty, new extrafields(),
            g);
        DirEntryWriter.Write(blockBytes, 0x14, dirEntry.Next, dirEntry, g);
            
        // act - parse dir block bytes
        var dirBlock = DirBlockReader.Parse(blockBytes, g);
            
        // assert - dir block matches
        Assert.Equal(Constants.DBLKID, dirBlock.id);
        Assert.Equal(1U, dirBlock.datestamp);
        Assert.Equal(2U, dirBlock.anodenr);
        Assert.Equal(3U, dirBlock.parent);

        // assert - entries in dir block match
        var dirEntries = dirBlock.DirEntries.ToList();
        var actualDirEntry = dirEntries.FirstOrDefault(x => x.type == Constants.ST_FILE && x.Name == dirEntryName);
        Assert.NotNull(actualDirEntry);
        Assert.Equal(Constants.ST_FILE, dirEntry.type);
        Assert.Equal(date, dirEntry.CreationDate);
    }
}