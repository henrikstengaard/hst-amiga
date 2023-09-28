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

    [Fact]
    public void When()
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
        var dirEntry = new direntry(0, Constants.ST_FILE, 1, 2, 3, date, name, comment, new extrafields(0, 0, 0, 0, 0, 0, 0), g);
        dirEntry.SetProtection(128);

        var extraFields = dirEntry.ExtraFields;
        extraFields.SetProtection(128);
        dirEntry.SetExtraFields(extraFields, g);
    }
    
    [Fact]
    public void When2()
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
        var dirEntry = new direntry(0, Constants.ST_FILE, 1, 2, 3, date, name, comment, new extrafields(0, 0, 0, 0, 0, 0, 0), g);


        var copyOfDirEntry = new direntry(dirEntry, g);
        
        Assert.Equal(dirEntry.Next, copyOfDirEntry.Next);
        Assert.Equal(dirEntry.type, copyOfDirEntry.type);
        Assert.Equal(dirEntry.anode, copyOfDirEntry.anode);
        Assert.Equal(dirEntry.fsize, copyOfDirEntry.fsize);
        Assert.Equal(dirEntry.CreationDate, copyOfDirEntry.CreationDate);
        Assert.Equal(dirEntry.protection, copyOfDirEntry.protection);
        Assert.Equal(dirEntry.comment, copyOfDirEntry.comment);

    }

    [Fact]
    public void When3()
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
        
        var name = "IconFile.info";
        var comment = string.Empty;
        var date = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dirEntry = new direntry(0, Constants.ST_FILE, 1, 2, 3, date, name, comment, new extrafields(0, 0, 0, 0, 0, 0, 0), g);
        dirEntry.SetProtection(128);

        var extrafields = new extrafields(dirEntry.ExtraFields);
        extrafields.SetProtection(128);
        
        var copyOfDirEntry = new direntry(0, dirEntry.type, dirEntry.anode, dirEntry.fsize, dirEntry.protection,
            dirEntry.CreationDate, dirEntry.Name, dirEntry.comment, extrafields, g);
        
        Assert.Equal(dirEntry.Next, copyOfDirEntry.Next);
    }
}