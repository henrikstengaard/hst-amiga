namespace Hst.Amiga.Tests.Pfs3Tests;

using System;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using Xunit;

public class GivenDirEntry
{
    [Fact]
    public void WhenCreateDirEntryThenCalculatedNextMatches()
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

        var name = "File";
        var comment = string.Empty;
        var date = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dirEntry = new direntry(0, Constants.ST_FILE, 1, 2, 3, date, name, comment, new extrafields(), g);
        
        var expectedNext = (SizeOf.DirEntry.Struct + name.Length + comment.Length) & 0xfffe;
        expectedNext += 2; // dir extension adds 2 extra to next
        Assert.Equal(expectedNext, dirEntry.Next);
        Assert.True(direntry.StartOfName + name.Length + comment.Length < dirEntry.Next);
    }
    
    [Fact]
    public void WhenCreateDirEntryWithCommentThenCalculatedNextMatches()
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

        var name = "File";
        var comment = "Comment";
        var date = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dirEntry = new direntry(0, Constants.ST_FILE, 1, 2, 3, date, name, comment, new extrafields(), g);
        
        var expectedNext = (SizeOf.DirEntry.Struct + name.Length + comment.Length) & 0xfffe;
        expectedNext += 2; // dir extension adds 2 extra to next
        Assert.Equal(expectedNext, dirEntry.Next);
        Assert.True(direntry.StartOfName + name.Length + comment.Length < dirEntry.Next);
    }

    [Fact]
    public void WhenCreateDirEntryWithCommentAndExtraFieldsThenCalculatedNextMatches()
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

        var name = "File";
        var comment = "Comment";
        var date = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dirEntry = new direntry(0, Constants.ST_FILE, 1, 2, 3, date, name, comment, new extrafields(1, 0, 0, 0, 0, 0, 0), g);
        
        var expectedNext = (SizeOf.DirEntry.Struct + name.Length + comment.Length) & 0xfffe;
        expectedNext += 2 + 2; // dir extension adds 2 bytes for extra fields link set to 1 and 2 bytes for flags to next
        Assert.Equal(expectedNext, dirEntry.Next);
        Assert.True(direntry.StartOfName + name.Length + comment.Length < dirEntry.Next);
    }
}