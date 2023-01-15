namespace Hst.Amiga.Tests.Pfs3Tests;

using System;
using Core.Converters;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using Xunit;

public class GivenDirEntryReader
{
    [Fact]
    public void WhenReadDirEntryAtStartOfEntriesThenDirEntryIsReturned()
    {
        // arrange - create dir block
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            }
        };
        var dirBlock = new dirblock(g);
        
        // arrange - create dir entry
        dirBlock.entries[1] = 1; // type
        BigEndianConverter.ConvertUInt32ToBytes(2, dirBlock.entries, 2); // anode nr
        BigEndianConverter.ConvertUInt32ToBytes(3, dirBlock.entries, 6); // file size
        var date = DateTime.UtcNow.Date;
        DateHelper.WriteDate(date, dirBlock.entries, 10); // creation date
        dirBlock.entries[16] = 5; // protection
        var nameBytes = AmigaTextHelper.GetBytes("Test");
        dirBlock.entries[17] = (byte)nameBytes.Length; // name length
        Array.Copy(nameBytes, 0, dirBlock.entries, 18, nameBytes.Length); // name
        dirBlock.entries[0] = (byte)(17 + nameBytes.Length); // next
        
        // act - read dir entry at offset 0
        var dirEntry = DirEntryReader.Read(dirBlock.entries, 0);

        // assert - dir entry next is zero, not read
        Assert.NotNull(dirEntry);
        Assert.Equal(17 + nameBytes.Length, dirEntry.next);
        Assert.Equal(1, dirEntry.type);
        Assert.Equal(2U, dirEntry.anode);
        Assert.Equal(3U, dirEntry.fsize);
        Assert.Equal(date, dirEntry.CreationDate);
        Assert.Equal(5, dirEntry.protection);
        Assert.Equal(4, dirEntry.nlength);
        Assert.Equal("Test", dirEntry.Name);
        Assert.Equal(string.Empty, dirEntry.comment);
    }
    
    [Fact]
    public void WhenReadDirEntryAtOffsetLargerThanEntriesThenEmptyDirEntryIsReturned()
    {
        // arrange - create dir block
        var g = new globaldata
        {
            dirextension = true,
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            }
        };
        var dirBlock = new dirblock(g);

        // act - read dir entry at offset 2000
        var dirEntry = DirEntryReader.Read(dirBlock.entries, 2000);

        // assert - dir entry next is zero, not read
        Assert.NotNull(dirEntry);
        Assert.Equal(0, dirEntry.next);
    }
    
    [Fact]
    public void WhenReadDirEntryAtEndOfEntriesThenEmptyDirEntryIsReturned()
    {
        // arrange - create dir block
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            }
        };
        var dirBlock = new dirblock(g);

        // act - read dir entry at last offset
        var dirEntry = DirEntryReader.Read(dirBlock.entries, dirBlock.entries.Length - 1);

        // assert - dir entry next is zero, not read
        Assert.NotNull(dirEntry);
        Assert.Equal(0, dirEntry.next);
    }
    
    [Fact]
    public void WhenReadDirEntryWithNextLargerThanEntriesThenEmptyDirEntryIsReturned()
    {
        // arrange - create dir block
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            }
        };
        var dirBlock = new dirblock(g);

        dirBlock.entries[^18] = 17;

        // act - read dir entry at offset with next within bounds
        var dirEntry = DirEntryReader.Read(dirBlock.entries, dirBlock.entries.Length - 18);

        // assert - dir entry next is zero, not read
        Assert.NotNull(dirEntry);
        Assert.Equal(17, dirEntry.next);
        Assert.NotEqual(DateTime.MinValue, dirEntry.CreationDate);
    }
}