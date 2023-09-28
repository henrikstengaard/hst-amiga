namespace Hst.Amiga.Tests.Pfs3Tests;

using System;
using System.IO;
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
        var entries = new byte[SizeOf.DirBlock.Entries(g)];
        
        // arrange - create dir entry
        entries[1] = 1; // type
        BigEndianConverter.ConvertUInt32ToBytes(2, entries, 2); // anode nr
        BigEndianConverter.ConvertUInt32ToBytes(3, entries, 6); // file size
        var date = DateTime.UtcNow.Date;
        DateHelper.WriteDate(date, entries, 10); // creation date
        entries[16] = 5; // protection
        var nameBytes = AmigaTextHelper.GetBytes("Test");
        entries[17] = (byte)nameBytes.Length; // name length
        Array.Copy(nameBytes, 0, entries, 18, nameBytes.Length); // name
        entries[0] = (byte)((SizeOf.DirEntry.Struct + nameBytes.Length) & 0xfffe);
        
         // act - read dir entry at offset 0
         var dirEntry = DirEntryReader.Read(entries, 0, g);

        // assert - dir entry next is zero, not read
        Assert.NotNull(dirEntry);
        Assert.Equal((SizeOf.DirEntry.Struct + nameBytes.Length & 0xfffe), dirEntry.Next);
        Assert.Equal(1, dirEntry.type);
        Assert.Equal(2U, dirEntry.anode);
        Assert.Equal(3U, dirEntry.fsize);
        Assert.Equal(date, dirEntry.CreationDate);
        Assert.Equal(5, dirEntry.protection);
        Assert.Equal(4, dirEntry.Name.Length);
        Assert.Equal("Test", dirEntry.Name);
        Assert.Equal(string.Empty, dirEntry.comment);
    }

    [Fact]
    public void WhenReadDirEntryWithCommentThenDirEntryIsReturned()
    {
        // arrange - create dir block
        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            },
            dirextension = true
        };
        var entries = new byte[SizeOf.DirBlock.Entries(g)];
        
        // arrange - create dir entry
        entries[1] = 1; // type
        BigEndianConverter.ConvertUInt32ToBytes(2, entries, 2); // anode nr
        BigEndianConverter.ConvertUInt32ToBytes(3, entries, 6); // file size
        var date = DateTime.UtcNow.Date;
        DateHelper.WriteDate(date, entries, 10); // creation date
        entries[16] = 5; // protection

        var nameBytes = AmigaTextHelper.GetBytes("File");
        entries[17] = (byte)nameBytes.Length; // name length
        Array.Copy(nameBytes, 0, entries, 18, nameBytes.Length); // name

        var commentBytes = AmigaTextHelper.GetBytes("Comment");
        entries[17 + 1 + nameBytes.Length] = (byte)commentBytes.Length; // comment length
        Array.Copy(commentBytes, 0, entries, 17 + 1 + nameBytes.Length + 1, commentBytes.Length); // comment
        
        entries[0] = (byte)(((SizeOf.DirEntry.Struct + nameBytes.Length + commentBytes.Length) & 0xfffe) + 2);
        
        // act - read dir entry at offset 0
        var dirEntry = DirEntryReader.Read(entries, 0, g);

        // assert - dir entry next is zero, not read
        Assert.NotNull(dirEntry);
        Assert.Equal(((SizeOf.DirEntry.Struct + nameBytes.Length + commentBytes.Length) & 0xfffe) + 2, dirEntry.Next);
        Assert.Equal(1, dirEntry.type);
        Assert.Equal(2U, dirEntry.anode);
        Assert.Equal(3U, dirEntry.fsize);
        Assert.Equal(date, dirEntry.CreationDate);
        Assert.Equal(5, dirEntry.protection);
        Assert.Equal(4, dirEntry.Name.Length);
        Assert.Equal("File", dirEntry.Name);
        Assert.Equal("Comment", dirEntry.comment);

        var blockBytes = new byte[entries.Length];
        DirEntryWriter.Write(blockBytes, 0, dirEntry.Next, dirEntry, g);
        
        Assert.Equal(entries, blockBytes);
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
        var entries = new byte[SizeOf.DirBlock.Entries(g)];

        // act - read dir entry at offset 2000
        var dirEntry = DirEntryReader.Read(entries, 2000, g);

        // assert - dir entry next is zero, not read
        Assert.NotNull(dirEntry);
        Assert.Equal(0, dirEntry.Next);
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
        var entries = new byte[SizeOf.DirBlock.Entries(g)];

        // act - read dir entry at last offset
        var dirEntry = DirEntryReader.Read(entries, entries.Length - 1, g);

        // assert - dir entry next is zero, not read
        Assert.NotNull(dirEntry);
        Assert.Equal(0, dirEntry.Next);
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
        var entries = new byte[SizeOf.DirBlock.Entries(g)];

        entries[^18] = 17;

        // act - read dir entry at offset with next outside bounds throws exception
        Assert.Throws<IOException>(() => DirEntryReader.Read(entries, entries.Length - 18, g));
    }
}