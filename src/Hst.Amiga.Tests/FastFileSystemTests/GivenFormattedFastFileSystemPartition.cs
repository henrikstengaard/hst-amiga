﻿namespace Hst.Amiga.Tests.FastFileSystemTests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Extensions;
using FileSystems;
using Xunit;

public class GivenFormattedFastFileSystemPartition : FastFileSystemTestBase
{
    [Theory]
    [InlineData(DiskSize100Mb, 512)]
    [InlineData(DiskSize100Mb, 1024)]
    [InlineData(DiskSize4Gb, 512)]
    [InlineData(DiskSize16Gb, 512)]
    public async Task WhenCreateDirectoryInRootDirectoryThenDirectoryExist(long diskSize, int fileSystemBlockSize)
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(diskSize, dosType: Dos3DosType, fileSystemBlockSize);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);
        
        // act - create "New Dir" in root directory
        await ffsVolume.CreateDirectory("New Dir");
        
        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory contains directory created
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "New Dir" && x.Type == EntryType.Dir));
    }
    
    [Theory]
    [InlineData(DiskSize100Mb, 512)]
    [InlineData(DiskSize100Mb, 1024)]
    [InlineData(DiskSize4Gb, 512)]
    [InlineData(DiskSize16Gb, 512)]
    public async Task WhenCreateMultipleDirectoriesInRootDirectoryThenDirectoriesExist(long diskSize, int fileSystemBlockSize)
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(diskSize, dosType: Dos3DosType, fileSystemBlockSize);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - create "New Dir1", "New Dir2", "New Dir3" in root directory
        await ffsVolume.CreateDirectory("New Dir1");
        await ffsVolume.CreateDirectory("New Dir2");
        await ffsVolume.CreateDirectory("New Dir3");

        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory contains directories created
        Assert.Equal(3, entries.Count);
        Assert.Equal(1, entries.Count(x => x.Name == "New Dir1" && x.Type == EntryType.Dir));
        Assert.Equal(1, entries.Count(x => x.Name == "New Dir2" && x.Type == EntryType.Dir));
        Assert.Equal(1, entries.Count(x => x.Name == "New Dir3" && x.Type == EntryType.Dir));
    }

    [Theory]
    [InlineData(DiskSize100Mb, 512)]
    [InlineData(DiskSize100Mb, 1024)]
    [InlineData(DiskSize4Gb, 512)]
    [InlineData(DiskSize16Gb, 512)]
    public async Task WhenCreateDirectoryInSubDirectoryThenDirectoryExist(long diskSize, int fileSystemBlockSize)
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(diskSize, dosType: Dos3DosType, fileSystemBlockSize);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - create "New Dir1" in root directory
        await ffsVolume.CreateDirectory("New Dir");
        
        // act - change directory to "New Dir", create "Sub Dir" directory
        await ffsVolume.ChangeDirectory("New Dir");
        await ffsVolume.CreateDirectory("Sub Dir");

        // act - change to root directory and list entries
        await ffsVolume.ChangeDirectory("/");
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory contains directory created
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "New Dir" && x.Type == EntryType.Dir));
        
        await ffsVolume.ChangeDirectory("New Dir");
        entries = (await ffsVolume.ListEntries()).ToList();

        // assert - "New Dir" directory contains directory created
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "Sub Dir" && x.Type == EntryType.Dir));
    }
    
    [Theory]
    [InlineData(DiskSize100Mb, 512)]
    [InlineData(DiskSize100Mb, 1024)]
    [InlineData(DiskSize4Gb, 512)]
    [InlineData(DiskSize16Gb, 512)]
    public async Task WhenCreateNewFileInRootThenFileExist(long diskSize, int fileSystemBlockSize)
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(diskSize, dosType: Dos3DosType, fileSystemBlockSize);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - create file in root directory
        await ffsVolume.CreateFile("New File");
        
        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory contains file created
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "New File" && x.Type == EntryType.File));
    }
    
    [Theory]
    [InlineData(DiskSize100Mb, 512)]
    [InlineData(DiskSize100Mb, 1024)]
    [InlineData(DiskSize4Gb, 512)]
    [InlineData(DiskSize16Gb, 512)]
    public async Task WhenCreateWriteDataToNewFileThenWhenReadDataFromFileDataMatches(long diskSize, int fileSystemBlockSize)
    {
        // arrange - data to write
        var data = AmigaTextHelper.GetBytes("New file with some text.");
        
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(diskSize, dosType: Dos3DosType, fileSystemBlockSize);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - create file in root directory
        //await ffsVolume.CreateFile("New File");

        // act - write data
        await using (var entryStream = await ffsVolume.OpenFile("New File", true))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        // act - read data
        int bytesRead;
        byte[] dataRead;
        await using (var entryStream = await ffsVolume.OpenFile("New File", false))
        {
            dataRead = new byte[entryStream.Length];
            bytesRead = await entryStream.ReadAsync(dataRead, 0, dataRead.Length);
        }
        
        // assert - data read matches data written
        Assert.Equal(data.Length, bytesRead);
        Assert.Equal(data.Length, dataRead.Length);
        Assert.Equal(data, dataRead);
    }
    
    [Theory]
    [InlineData(DiskSize100Mb, 512)]
    [InlineData(DiskSize100Mb, 1024)]
    [InlineData(DiskSize4Gb, 512)]
    [InlineData(DiskSize16Gb, 512)]
    public async Task WhenCreateWriteDataToNewFileThenWhenSeekAndReadDataMatches(long diskSize, int fileSystemBlockSize)
    {
        // arrange - data to write
        var data = AmigaTextHelper.GetBytes("New file with some text.");
        
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(diskSize, dosType: Dos3DosType, fileSystemBlockSize);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - create file in root directory
        //await ffsVolume.CreateFile("New File");

        // act - create file and write data
        await using (var entryStream = await ffsVolume.OpenFile("New File", true))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        // act - read data
        int bytesRead;
        byte[] dataRead;
        long seekPosition;
        long readPosition;
        await using (var entryStream = await ffsVolume.OpenFile("New File", false))
        {
            seekPosition = entryStream.Seek(10, SeekOrigin.Begin);
            dataRead = new byte[10];
            bytesRead = await entryStream.ReadAsync(dataRead, 0, 10);
            readPosition = entryStream.Position;
        }

        // assert - stream seek resulted in position is equal to 10
        Assert.Equal(10, seekPosition);

        // assert - stream read of 10 bytes resulted in position is equal to 20
        Assert.Equal(20, readPosition);
        
        // assert - 10 bytes of data read matches 10 bytes from data written
        Assert.Equal(10, bytesRead);
        var expectedDataRead = data.Skip(10).Take(10).ToArray();
        Assert.Equal(expectedDataRead.Length, dataRead.Length);
        Assert.Equal(expectedDataRead, dataRead);
    }
    
    [Theory]
    [InlineData(DiskSize100Mb, 512)]
    [InlineData(DiskSize100Mb, 1024)]
    [InlineData(DiskSize4Gb, 512)]
    [InlineData(DiskSize16Gb, 512)]
    public async Task WhenCreateAndDeleteFileInRootThenFileDoesntExist(long diskSize, int fileSystemBlockSize)
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(diskSize, dosType: Dos3DosType, fileSystemBlockSize);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);
        
        // act - create file in root directory
        await ffsVolume.CreateFile("New File");

        // act - delete file from root directory
        await ffsVolume.Delete("New File");
        
        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory is empty
        Assert.Empty(entries);
    }
    
    [Theory]
    [InlineData(DiskSize100Mb, 512)]
    [InlineData(DiskSize100Mb, 1024)]
    [InlineData(DiskSize4Gb, 512)]
    [InlineData(DiskSize16Gb, 512)]
    public async Task WhenCreateTwoFilesAndDeleteOneFileInRootThenOneFileExists(long diskSize, int fileSystemBlockSize)
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(diskSize, dosType: Dos3DosType, fileSystemBlockSize);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);
        
        // act - create file in root directory
        await ffsVolume.CreateFile("New File 1");
        await ffsVolume.CreateFile("New File 2");

        // act - delete file from root directory
        await ffsVolume.Delete("New File 1");
        
        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory contains new file 1
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "New File 2" && x.Type == EntryType.File));
    }
    
    [Theory]
    [InlineData(DiskSize100Mb, 512)]
    [InlineData(DiskSize100Mb, 1024)]
    [InlineData(DiskSize4Gb, 512)]
    [InlineData(DiskSize16Gb, 512)]
    public async Task WhenCreateAndDeleteDirectoryInRootDirectoryThenDirectoryDoesntExist(long diskSize, int fileSystemBlockSize)
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(diskSize, dosType: Dos3DosType, fileSystemBlockSize);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);
        
        // act - create "New Dir" in root directory
        await ffsVolume.CreateDirectory("New Dir");
        
        // act - delete directory from root directory
        await ffsVolume.Delete("New Dir");
        
        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory is empty
        Assert.Empty(entries);
    }
    
    [Theory]
    [InlineData(DiskSize100Mb, 512)]
    [InlineData(DiskSize100Mb, 1024)]
    [InlineData(DiskSize4Gb, 512)]
    [InlineData(DiskSize16Gb, 512)]
    public async Task WhenCreateAndRenameFileInRootThenFileIsRenamed(long diskSize, int fileSystemBlockSize)
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(diskSize, dosType: Dos3DosType, fileSystemBlockSize);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);
        
        // act - create file in root directory
        await ffsVolume.CreateFile("New File");

        // act - rename file in root directory
        await ffsVolume.Rename("New File", "Renamed File");
        
        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory contains file created
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "Renamed File" && x.Type == EntryType.File));
    }
    
    [Theory]
    [InlineData(DiskSize100Mb, 512)]
    [InlineData(DiskSize100Mb, 1024)]
    [InlineData(DiskSize4Gb, 512)]
    [InlineData(DiskSize16Gb, 512)]
    public async Task WhenMoveFileFromRootDirectoryToSubdirectoryThenFileIsLocatedInSubdirectory(long diskSize, int fileSystemBlockSize)
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(diskSize, dosType: Dos3DosType, fileSystemBlockSize);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);
        
        // act - create "New Dir" in root directory
        await ffsVolume.CreateDirectory("New Dir");
        
        // act - create file in root directory
        await ffsVolume.CreateFile("New File");

        // act - move file from root directory to subdirectory
        await ffsVolume.Rename("New File", "New Dir/Moved File");
        
        // act - mount pfs3 volume, list entries in root directory and unmount pfs3 volume
        var rootEntries = (await ffsVolume.ListEntries()).ToList();
        await ffsVolume.ChangeDirectory("New Dir");
        var subDirEntries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory contains directory
        Assert.Single(rootEntries);
        Assert.Equal(1, rootEntries.Count(x => x.Name == "New Dir" && x.Type == EntryType.Dir));
        
        // assert - sub directory contains moved file
        Assert.Single(subDirEntries);
        Assert.Equal(1, subDirEntries.Count(x => x.Name == "Moved File" && x.Type == EntryType.File));
    }
    
    [Theory]
    [InlineData(DiskSize100Mb, 512)]
    [InlineData(DiskSize100Mb, 1024)]
    [InlineData(DiskSize4Gb, 512)]
    [InlineData(DiskSize16Gb, 512)]
    public async Task WhenSetCommentForFileInRootThenCommentIsChanged(long diskSize, int fileSystemBlockSize)
    {
        // arrange - comment to set
        var comment = "Comment for file";

        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(diskSize, dosType: Dos3DosType, fileSystemBlockSize);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);
        
        // act - create file in root directory
        await ffsVolume.CreateFile("New File");

        // act - set comment for file in root directory
        await ffsVolume.SetComment("New File", comment);
        
        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory contains file created
        Assert.Single(entries);
        var dirEntry = entries.FirstOrDefault(x => x.Name == "New File" && x.Type == EntryType.File);
        Assert.NotNull(dirEntry);
        Assert.Equal(comment, dirEntry.Comment);
    }
    
    [Theory]
    [InlineData(DiskSize100Mb, 512)]
    [InlineData(DiskSize100Mb, 1024)]
    [InlineData(DiskSize4Gb, 512)]
    [InlineData(DiskSize16Gb, 512)]
    public async Task WhenSetProtectionBitsForFileInRootThenProtectionBitsAreChanged(long diskSize, int fileSystemBlockSize)
    {
        // arrange - protection bits to set
        var protectionBits = ProtectionBits.Delete | ProtectionBits.Executable | ProtectionBits.Write | ProtectionBits.Read | ProtectionBits.HeldResident | ProtectionBits.Archive | ProtectionBits.Pure | ProtectionBits.Script;
        
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(diskSize, dosType: Dos3DosType, fileSystemBlockSize);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);
        
        // act - create file in root directory
        await ffsVolume.CreateFile("New File");

        // act - set protection bits for file in root directory
        await ffsVolume.SetProtectionBits("New File", protectionBits);
        
        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory contains file created
        Assert.Single(entries);
        var dirEntry = entries.FirstOrDefault(x => x.Name == "New File" && x.Type == EntryType.File);
        Assert.NotNull(dirEntry);
        Assert.Equal(protectionBits, dirEntry.ProtectionBits);
    }

    [Theory]
    [InlineData(DiskSize100Mb, 512)]
    [InlineData(DiskSize100Mb, 1024)]
    [InlineData(DiskSize4Gb, 512)]
    [InlineData(DiskSize16Gb, 512)]
    public async Task WhenSetDateForFileInRootThenCreationDateIsChanged(long diskSize, int fileSystemBlockSize)
    {
        // arrange - date to set
        var date = DateTime.Now.AddDays(-10).Trim(TimeSpan.TicksPerSecond);
        
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(diskSize, dosType: Dos3DosType, fileSystemBlockSize);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);
        
        // act - create file in root directory
        await ffsVolume.CreateFile("New File");

        // act - set date for file in root directory
        await ffsVolume.SetDate("New File", date);
        
        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory contains file created
        Assert.Single(entries);
        var dirEntry = entries.FirstOrDefault(x => x.Name == "New File" && x.Type == EntryType.File);
        Assert.NotNull(dirEntry);
        Assert.Equal(date, dirEntry.Date);
    }
}