namespace Hst.Amiga.Tests.FastFileSystemTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Extensions;
using FileSystems;
using FileSystems.Exceptions;
using FileSystems.FastFileSystem;
using RigidDiskBlocks;
using Xunit;
using Constants = FileSystems.FastFileSystem.Constants;
using Directory = FileSystems.FastFileSystem.Directory;
using FileMode = FileSystems.FileMode;

public class GivenFormattedFastFileSystemPartition : FastFileSystemTestBase
{
    [Fact]
    public async Task WhenCreateAndList100FilesInRootDirectoryThenFilesExists()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(DiskSize100Mb, dosType: Dos3DosType);
        
        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);
        
        // act - create 100 files in root directory
        var expectedEntries = Enumerable.Range(0, 100).Select(x => $"New File{x}").OrderBy(x => x)
            .ToList();
        for (var i = 0; i < 100; i++)
        {
            await ffsVolume.CreateFile(expectedEntries[i]);
        }

        // assert - list entries contains files in root directory
        var entries = (await ffsVolume.ListEntries())
            .OrderBy(x => x.Name).ToList();
        Assert.Equal(100, entries.Count);
        Assert.Equal(100, entries.Count(x => x.Type == EntryType.File));
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(expectedEntries[i], entries[i].Name);
        }
    }
    
    [Fact]
    public async Task WhenCreate100DirectoriesFilesAndOverwrite100FilesInRootDirectoryThenDirectoriesFilesExists()
    {
        var data = new byte[844];
        Array.Fill<byte>(data, 1);
        
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(DiskSize100Mb, dosType: Dos3DosType);
        
        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - create 100 directories in root directory
        var dirEntries = Enumerable.Range(0, 100).Select(x => $"New Dir{x}").OrderBy(x => x)
            .ToList();
        foreach (var dirEntry in dirEntries)
        {
            await ffsVolume.CreateDirectory(dirEntry);
        }

        // act - create 100 files in root directory
        var fileEntries = Enumerable.Range(0, 100).Select(x => $"New File{x}").OrderBy(x => x)
            .ToList();
        foreach (var fileEntry in fileEntries)
        {
            await ffsVolume.CreateFile(fileEntry, true, true);
            
            // act - write data
            await using (var entryStream = await ffsVolume.OpenFile(fileEntry, FileMode.Append))
            {
                await entryStream.WriteAsync(data, 0, data.Length);
            }
        }

        data = new byte[388];
        Array.Fill<byte>(data, 2);
        
        // act - overwrite 100 existing files in root directory
        foreach (var fileEntry in fileEntries)
        {
            await ffsVolume.CreateFile(fileEntry, true, true);
            
            // act - write data
            await using (var entryStream = await ffsVolume.OpenFile(fileEntry, FileMode.Append))
            {
                await entryStream.WriteAsync(data, 0, data.Length);
            }
        }

        // assert - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();
        Assert.Equal(200, entries.Count);
        
        // assert - 100 directories exist
        var actualDirEntries = entries.Where(x => x.Type == EntryType.Dir)
            .ToDictionary(key => key.Name, value => value);
        Assert.Equal(100, actualDirEntries.Count);
        foreach (var dirEntry in dirEntries)
        {
            Assert.Equal(dirEntry, actualDirEntries[dirEntry].Name);
        }
        
        // assert - 100 files exist
        var actualFileEntries = entries.Where(x => x.Type == EntryType.File)
            .ToDictionary(key => key.Name, value => value);
        Assert.Equal(100, actualFileEntries.Count);
        foreach (var fileEntry in fileEntries)
        {
            Assert.Equal(fileEntry, actualFileEntries[fileEntry].Name);
        }
    }

    [Fact]
    public async Task WhenCreateAndList100DirectoriesInRootDirectoryThenDirectoriesExists()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(DiskSize100Mb, dosType: Dos3DosType);
        
        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);
        
        // act - create 100 directories in root directory
        var expectedEntries = Enumerable.Range(0, 100).Select(x => $"New Dir{x}").OrderBy(x => x)
            .ToList();
        for (var i = 0; i < 100; i++)
        {
            await ffsVolume.CreateDirectory(expectedEntries[i]);
        }

        // assert - list entries contains directories in root directory
        var entries = (await ffsVolume.ListEntries())
            .OrderBy(x => x.Name).ToList();
        Assert.Equal(100, entries.Count);
        Assert.Equal(100, entries.Count(x => x.Type == EntryType.Dir));
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(expectedEntries[i], entries[i].Name);
        }
    }

    [Fact]
    public async Task WhenCreateAndSearchFor100DirectoriesInRootDirectoryThenDirectoriesAreFound()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(DiskSize100Mb, dosType: Dos3DosType);

        // arrange - read rigid disk block
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

        // arrange - get first partition
        var partition = rigidDiskBlock.PartitionBlocks.FirstOrDefault();
        
        // assert - partition is not null
        Assert.NotNull(partition);

        // arrange - mount volume
        var volume = await FastFileSystemHelper.Mount(stream, partition.LowCyl, partition.HighCyl, partition.Surfaces,
            partition.BlocksPerTrack,
            partition.Reserved, partition.BlockSize, partition.FileSystemBlockSize);
        
        // arrange - create fast file system volume
        await using var ffsVolume = new FastFileSystemVolume(volume, volume.RootBlockOffset);

        // act - create 100 directories in root directory
        var expectedEntries = Enumerable.Range(0, 100).Select(x => $"New Dir{x}").OrderBy(x => x)
            .ToList();
        for (var i = 0; i < 100; i++)
        {
            await ffsVolume.CreateDirectory(expectedEntries[i]);
        }

        // act - search for directories created in root directory
        for (var i = 0; i < 100; i++)
        {
            // act - find sub dir entry
            var result = await Directory.FindEntry(volume.RootBlockOffset, $"New Dir{i}", volume);
        
            // assert - entry is found, if no parts not found
            Assert.Empty(result.PartsNotFound);
        }
    }
    
    [Fact]
    public async Task WhenCreateFindSubdirectoryThenSubDirectoryExists()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(DiskSize100Mb, dosType: Dos3DosType);

        // arrange - read rigid disk block
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

        // arrange - get first partition
        var partition = rigidDiskBlock.PartitionBlocks.FirstOrDefault();
        
        // assert - partition is not null
        Assert.NotNull(partition);

        // arrange - mount volume
        var volume = await FastFileSystemHelper.Mount(stream, partition.LowCyl, partition.HighCyl, partition.Surfaces,
            partition.BlocksPerTrack,
            partition.Reserved, partition.BlockSize, partition.FileSystemBlockSize);
        
        // arrange - create fast file system volume
        await using var ffsVolume = new FastFileSystemVolume(volume, volume.RootBlockOffset);
        
        // arrange - create "New Dir" in root directory
        await ffsVolume.CreateDirectory("New Dir");

        // arrange - change directory to "New Dir", create "Sub Dir" directory
        await ffsVolume.ChangeDirectory("New Dir");
        await ffsVolume.CreateDirectory("Sub Dir");

        // arrange - change directory to root directory
        await ffsVolume.ChangeDirectory("/");

        // act - find sub dir entry
        var result = await Directory.FindEntry(volume.RootBlockOffset, "New Dir/Sub Dir", volume);
        
        // assert - entry is found, no parts not found
        Assert.Empty(result.PartsNotFound);
        
        // assert - entry name is equal to sub dir
        Assert.Equal("Sub Dir", result.Name);
    }

    [Fact]
    public async Task WhenFindExistingEntryThenEntryExists()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(DiskSize100Mb, dosType: Dos3DosType);
        
        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);
        
        // arrange - create "New Dir" in root directory
        await ffsVolume.CreateDirectory("New Dir");

        // act - find entry
        var result = await ffsVolume.FindEntry("New Dir");
        
        // assert - entry exists and is equal
        Assert.NotNull(result);
        Assert.Empty(result.PartsNotFound);
        Assert.NotNull(result.Entry);
        Assert.Equal("New Dir", result.Entry.Name);
        Assert.Equal(EntryType.Dir, result.Entry.Type);
        Assert.Equal(0, result.Entry.Size);
    }

    [Fact]
    public async Task WhenFindNonExistingEntryThenPartsNotFoundContainsName()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(DiskSize100Mb, dosType: Dos3DosType);
        
        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);
        
        // act - find entry
        var result = await ffsVolume.FindEntry("New Dir");
        
        // assert - result has new dir in parts not found and entry is null
        Assert.NotNull(result);
        Assert.Single(result.PartsNotFound);
        Assert.Equal(new List<string>{ "New Dir" }, result.PartsNotFound);
        Assert.Null(result.Entry);
    }
    
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

    [Fact]
    public async Task WhenOpenFileInWriteModeAndFileExistsThenExceptionIsThrown()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - create file in root directory
        await ffsVolume.OpenFile("New File", FileMode.Write);

        // assert - open same file for writing in root directory throws exception as file exists
        await Assert.ThrowsAsync<PathAlreadyExistsException>(async () => await ffsVolume.OpenFile("New File", FileMode.Write));
    }

    [Fact]
    public async Task WhenOpenFileInAppendModeAndFileExistsThenFileIsOpened()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - open file in root directory in write mode, creates new file
        await ffsVolume.OpenFile("New File", FileMode.Write);

        // act - open file in root directory in append mode
        await ffsVolume.OpenFile("New File", FileMode.Append);

        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();
        
        // assert - root directory contains file created
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "New File" && x.Type == EntryType.File));
    }
    
    [Fact]
    public async Task WhenOpenFileInReadModeAndFileDoesntExistsThenExceptionIsThrown()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - open file in root directory in read mode, creates new file
        await Assert.ThrowsAsync<PathNotFoundException>(async () => await ffsVolume.OpenFile("New File", FileMode.Read));
    }
    
    [Fact]
    public async Task WhenCreateAndOverwriteFileWithSmallerOneThenLessBytesAreFree()
    {
        // arrange - data to write
        var data = new byte[10000];
        Array.Fill<byte>(data, 1);
        
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        var freeBytesAtStart = ffsVolume.Free;
        
        // act - create file
        await ffsVolume.CreateFile("New File");
        
        // act - write data
        await using (var entryStream = await ffsVolume.OpenFile("New File", FileMode.Append))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }
        
        // assert - free bytes after writing 1st time is smaller than at start
        var freeBytesAfter1StFile = ffsVolume.Free;
        Assert.True(freeBytesAfter1StFile < freeBytesAtStart);

        // arrange - data to write
        data = new byte[1000];
        Array.Fill<byte>(data, 1);
        
        // act - create file and overwrite existing
        await ffsVolume.CreateFile("New File", true);

        // act - write data
        await using (var entryStream = await ffsVolume.OpenFile("New File", FileMode.Append))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        // assert - free bytes after writing 2nd time is larger 1st time (overwritten file is smaller)
        var freeBytesAfter2NdFile = ffsVolume.Free;
        Assert.True(freeBytesAfter2NdFile > freeBytesAfter1StFile);
        
        // act - read data
        int bytesRead;
        byte[] dataRead;
        await using (var entryStream = await ffsVolume.OpenFile("New File", FileMode.Read))
        {
            dataRead = new byte[entryStream.Length];
            bytesRead = await entryStream.ReadAsync(dataRead, 0, dataRead.Length);
        }
        
        // assert - data read matches data written
        Assert.Equal(data.Length, bytesRead);
        Assert.Equal(data.Length, dataRead.Length);
        Assert.Equal(data, dataRead);
    }
    
    [Fact]
    public async Task WhenOpenFileWithoutReadBitThenExceptionIsThrown()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();
        
        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create file
        await ffsVolume.CreateFile("New File");

        // arrange - file only has delete protection bit set
        await ffsVolume.SetProtectionBits("New File", ProtectionBits.Delete);
        
        // act - open file in read mode then exception is thrown
        await Assert.ThrowsAsync<FileSystemException>(async () => await ffsVolume.OpenFile("New File", FileMode.Read));
    }

    [Fact]
    public async Task WhenOpenFileWithoutReadBitAndIgnoreProtectionBitsThenFileOpened()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();
        
        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create file
        await ffsVolume.CreateFile("New File");

        // arrange - file only has delete protection bit set
        await ffsVolume.SetProtectionBits("New File", ProtectionBits.Delete);
        
        // act - open file in read mode then exception is thrown
        await using var entryStream = await ffsVolume.OpenFile("New File", FileMode.Read, ignoreProtectionBits: true);
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

        // act - write data
        await using (var entryStream = await ffsVolume.OpenFile("New File", FileMode.Write))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        // act - read data
        int bytesRead;
        byte[] dataRead;
        await using (var entryStream = await ffsVolume.OpenFile("New File", FileMode.Read))
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

        // act - create file and write data
        await using (var entryStream = await ffsVolume.OpenFile("New File", FileMode.Write))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        // act - read data
        int bytesRead;
        byte[] dataRead;
        long seekPosition;
        long readPosition;
        await using (var entryStream = await ffsVolume.OpenFile("New File", FileMode.Read))
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
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(450000)]
    public async Task WhenWriteDataLargerThanBlockSizeToNewFileThenWhenReadDataFromFileDataMatches(int fileSize)
    {
        // arrange - data to write
        var data = new byte[fileSize];
        for (var i = 0; i < fileSize; i++)
        {
            data[i] = (byte)(i % 255);
        }
        
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - write data
        await using (var entryStream = await ffsVolume.OpenFile("New File", FileMode.Write))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        // act - read data
        int bytesRead;
        byte[] dataRead;
        await using (var entryStream = await ffsVolume.OpenFile("New File", FileMode.Read))
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

    [Fact]
    public async Task WhenCreateFileWithNameLongerThan30AndWriteDataThenFileExistsLimitedToLengthOf30()
    {
        // arrange - file name and expected file name
        const string fileName = "1234567890123456789012345678901234567890";
        var expectedName = fileName[..Constants.MAXNAMELEN];
        
        // arrange - data to write
        var data = new byte[10];
        Array.Fill<byte>(data, 1);
        
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(DiskSize100Mb, Dos3DosType);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);
        
        // act - create file
        await ffsVolume.CreateFile(fileName);
        
        // act - write data
        await using (var entryStream = await ffsVolume.OpenFile(fileName, FileMode.Append))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        // act - list entries in root directory
        var entries = (await ffsVolume.ListEntries()).ToList();

        // assert - root directory contains file created
        Assert.Single(entries);
        var dirEntry = entries.FirstOrDefault(x => x.Name == expectedName && x.Type == EntryType.File);
        Assert.NotNull(dirEntry);
        
        // act - read data
        int bytesRead;
        byte[] dataRead;
        await using (var entryStream = await ffsVolume.OpenFile(fileName, FileMode.Read))
        {
            dataRead = new byte[entryStream.Length];
            bytesRead = await entryStream.ReadAsync(dataRead, 0, dataRead.Length);
        }

        // assert - data read matches data written
        Assert.Equal(data.Length, bytesRead);
        Assert.Equal(data.Length, dataRead.Length);
        Assert.Equal(data, dataRead);
    }
    
    [Fact]
    public async Task WhenFindDirectoryAndFileEntryThenEntryIsReturned()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(DiskSize100Mb, Dos3DosType);
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - create directory
        await ffsVolume.CreateDirectory("New Dir");

        // act - change directory
        await ffsVolume.ChangeDirectory("New Dir");
        
        // act - create file
        await ffsVolume.CreateFile("New File");

        // act - change directory
        await ffsVolume.ChangeDirectory("/");

        // act - find new dir entry
        var result = await ffsVolume.FindEntry("New Dir");
        
        // assert - file entry is found and matches
        Assert.NotNull(result);
        Assert.Empty(result.PartsNotFound);
        Assert.NotNull(result.Entry);
        Assert.Equal("New Dir", result.Entry.Name);
        Assert.Equal(EntryType.Dir, result.Entry.Type);
        
        // act - change directory
        await ffsVolume.ChangeDirectory("New Dir");
        
        // act - find new file entry
        result = await ffsVolume.FindEntry("New File");
        
        // assert - file entry is found and matches
        Assert.NotNull(result);
        Assert.Empty(result.PartsNotFound);
        Assert.NotNull(result.Entry);
        Assert.Equal("New File", result.Entry.Name);
        Assert.Equal(EntryType.File, result.Entry.Type);
    }

    [Fact]
    public async Task WhenFindEntryWithDirectorySeparatorInNameThenExceptionIsThrown()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(DiskSize100Mb, Dos3DosType);
        
        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act & assert - find entry with directory separator throws exception
        await Assert.ThrowsAsync<ArgumentException>(async () => await ffsVolume.FindEntry("New Dir/New File"));
    }

    [Fact]
    public async Task WhenCreateDirectoryAndFileWithSameNameThenExceptionIsThrown()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - create directory
        await ffsVolume.CreateDirectory("Dir");
        
        // act & assert - create and file with same name as directory throws exception
        await Assert.ThrowsAsync<PathAlreadyExistsException>(async () => await ffsVolume.CreateFile("Dir", ignoreProtectionBits: true));
    }
    
    [Fact]
    public async Task WhenCreateDirectoryAndOverwriteAsFileWithSameNameThenExceptionIsThrown()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();
        
        // act - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - create directory
        await ffsVolume.CreateDirectory("Dir");
        
        // act & assert - create and overwrite file with same name as directory throws exception
        await Assert.ThrowsAsync<NotAFileException>(async () => await ffsVolume.CreateFile("Dir", true, true));
    }

    [Fact]
    public async Task When_GetCurrentPathFromRoot_Then_CurrentPathIsCorrect()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();
        
        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // act - get the current path
        var currentPath = await ffsVolume.GetCurrentPath();
        
        // assert - the current path is correct
        Assert.Equal("/", currentPath);
    }

    [Fact]
    public async Task When_GetCurrentPathFromSubDirectory_Then_CurrentPathIsCorrect()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();
        
        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - create directories
        await ffsVolume.CreateDirectory("dir1");
        await ffsVolume.ChangeDirectory("dir1");
        await ffsVolume.CreateDirectory("dir2");
        await ffsVolume.ChangeDirectory("dir2");

        // act - get the current path
        var currentPath = await ffsVolume.GetCurrentPath();
        
        // assert - the current path is correct
        Assert.Equal("/dir1/dir2", currentPath);
    }

    [Fact]
    public async Task When_ChangeDirectoryToExistingFile_Then_ExceptionIsThrownAndCurrentDirectoryBlockNumberIsNotChanged()
    {
        // arrange - create fast file system formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();
        
        // arrange - mount fast file system volume
        await using var ffsVolume = await MountVolume(stream);

        // arrange - get current directory block number
        var currentDirectoryBlockNumber = ffsVolume.CurrentDirectoryBlockNumber;
        
        // act - create file
        await ffsVolume.CreateFile("file1.txt");

        // assert - current directory block number is pfs3 root directory
        Assert.Equal(currentDirectoryBlockNumber, ffsVolume.CurrentDirectoryBlockNumber);

        // act - change directory to file
        await Assert.ThrowsAsync<PathNotFoundException>(async () => await ffsVolume.ChangeDirectory("file1.txt"));

        // assert - current directory block number is pfs3 root directory
        Assert.Equal(currentDirectoryBlockNumber, ffsVolume.CurrentDirectoryBlockNumber);
    }
}