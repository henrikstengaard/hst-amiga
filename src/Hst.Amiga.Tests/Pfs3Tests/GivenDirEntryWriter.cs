namespace Hst.Amiga.Tests.Pfs3Tests;

using System;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using Xunit;

public class GivenDirEntryWriter
{
    [Fact]
    public void WhenWriteAndReadDirEntryThenDirEntryMatch()
    {
        // arrange - global data
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            },
            dirextension = true // indicate pfs3 disk uses dir extension and extra fields for dir entries
        };

        var blockBytes = new byte[g.RootBlock.ReservedBlksize];
        
        // arrange - create dir entry
        var dirEntry = CreateDirEntry("New File", string.Empty, new extrafields(), g);
        
        // act - write dir entry to offset 20
        DirEntryWriter.Write(blockBytes, 0x14, dirEntry.Next, dirEntry, g);

        // act - read dir entry from offset 20
        var actualDirEntry = DirEntryReader.Read(blockBytes, 0x14, g);

        // assert - dir entry matches
        Assert.Equal(dirEntry.Next, actualDirEntry.Next);
        Assert.Equal(dirEntry.type, actualDirEntry.type);
        Assert.Equal(dirEntry.anode, actualDirEntry.anode);
        Assert.Equal(dirEntry.fsize, actualDirEntry.fsize);
        Assert.Equal(dirEntry.Name, actualDirEntry.Name);
        Assert.Equal(dirEntry.comment, actualDirEntry.comment);
        Assert.Equal(dirEntry.CreationDate, actualDirEntry.CreationDate);
    }

    [Fact]
    public void WhenWriteAndReadDirEntryWithCommentAndRollPointerThenDirEntryMatch()
    {
        // arrange - global data
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            },
            dirextension = true // indicate disk uses dir extension and extra fields for dir entries
        };

        // arrange - block bytes to use for writing and reading dir entry
        var blockBytes = new byte[g.RootBlock.ReservedBlksize];

        // arrange - create dir entry with comment and extra fields roll pointer
        var dirEntry = CreateDirEntry("New File1", "Comment1", new extrafields(0, 0, 0, 0, 0, 500, 0), g);

        // act - write dir entry to offset 20
        DirEntryWriter.Write(blockBytes, 0x14, dirEntry.Next, dirEntry, g);

        // act - read dir entry from offset 20
        var actualDirEntry = DirEntryReader.Read(blockBytes, 0x14, g);

        // assert - dir entry matches
        Assert.Equal(dirEntry.Next, actualDirEntry.Next);
        Assert.Equal(dirEntry.type, actualDirEntry.type);
        Assert.Equal(dirEntry.anode, actualDirEntry.anode);
        Assert.Equal(dirEntry.fsize, actualDirEntry.fsize);
        Assert.Equal(dirEntry.Name, actualDirEntry.Name);
        Assert.Equal(dirEntry.comment, actualDirEntry.comment);
        Assert.NotEqual(0U, dirEntry.ExtraFields.rollpointer);
        Assert.Equal(dirEntry.ExtraFields.rollpointer, actualDirEntry.ExtraFields.rollpointer);
    }
    
    private static direntry CreateDirEntry(string name, string comment, extrafields extraFields, globaldata g)
    {
        var date = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return new direntry(0, Constants.ST_FILE, 0, 0, 0, date, name, comment, extraFields, g);
    }
}