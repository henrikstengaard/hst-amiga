namespace Hst.Amiga.Tests.Pfs3Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Extensions;
using FileSystems;
using FileSystems.Exceptions;
using FileSystems.Pfs3;
using Xunit;
using Constants = FileSystems.Pfs3.Constants;
using Directory = FileSystems.Pfs3.Directory;
using FileMode = FileSystems.FileMode;

public class GivenFormattedPfs3Disk : Pfs3TestBase
{
    [Fact]
    public async Task WhenFindExistingEntryThenEntryExists()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - create "New Dir" in root directory
        await pfs3Volume.CreateDirectory("New Dir");

        // act - find entry
        var result = await pfs3Volume.FindEntry("New Dir");

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
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - find entry
        var result = await pfs3Volume.FindEntry("New Dir");

        // assert - result has new dir in parts not found and entry is null
        Assert.NotNull(result);
        Assert.Single(result.PartsNotFound);
        Assert.Equal(new List<string>{ "New Dir" }, result.PartsNotFound);
        Assert.Null(result.Entry);
    }

    [Theory]
    [InlineData(DiskSize100Mb)]
    [InlineData(DiskSize4Gb)]
    [InlineData(DiskSize16Gb)]
    public async Task WhenCreateDirectoryInRootDirectoryThenDirectoryExist(long diskSize)
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk(diskSize);

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create "New Dir" in root directory
        await pfs3Volume.CreateDirectory("New Dir");

        // act - list entries in root directory
        var entries = (await pfs3Volume.ListEntries()).ToList();
        await Pfs3Helper.Flush(pfs3Volume.g);

        // assert - root directory contains directory created
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "New Dir" && x.Type == EntryType.Dir));
    }

    [Fact]
    public async Task WhenCreate100DirectoriesWith100FilesInEachThenDirectoriesAndFilesExist()
    {
        // arrange - data to write
        var data = new byte[1000];
        Array.Fill<byte>(data, 1);
        
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);
        
        // create 100 directories with each 100 files
        IList<Entry> entries;
        for (var d = 0; d < 100; d++)
        {
            // act - change to root directory
            await pfs3Volume.ChangeDirectory("/");

            // act - create "New Dir" in root directory
            var dirName = $"New Dir{d}";
            await pfs3Volume.CreateDirectory(dirName);

            // act - change to "New Dir" directory
            await pfs3Volume.ChangeDirectory(dirName);

            // act - create "Extra Dir" in "New Dir" directory
            await pfs3Volume.CreateDirectory("Extra");

            // act - change to "Extra Dir" directory
            await pfs3Volume.ChangeDirectory("Extra");
            
            // create 100 files in directory
            for (var f = 0; f < 100; f++)
            {
                // act - create file
                var fileName = $"New File{f}";
                await pfs3Volume.CreateFile(fileName);

                // act - write data
                await using var entryStream = await pfs3Volume.OpenFile(fileName, FileMode.Append);
                await entryStream.WriteAsync(data, 0, data.Length);
            }
            
            // arrange - flush changes
            await pfs3Volume.Flush();
            
            // act - list entries
            entries = (await pfs3Volume.ListEntries())
                .OrderBy(x => x.Name).ToList();

            // assert - 100 files exist
            Assert.Equal(100, entries.Count);
            Assert.Equal(100, entries.Count(x => x.Type == EntryType.File));
            for (var f = 0; f < 100; f++)
            {
                var expectedFileName = $"New File{f}";
                var entry = entries.FirstOrDefault(x => x.Name == expectedFileName)?.Name ?? string.Empty;
                Assert.Equal(expectedFileName, entry);
            }
        }
        
        // act - change to root directory
        await pfs3Volume.ChangeDirectory("/");

        // act - list entries
        entries = (await pfs3Volume.ListEntries())
            .OrderBy(x => x.Name).ToList();

        // assert - 100 directories exist
        Assert.Equal(100, entries.Count);
        Assert.Equal(100, entries.Count(x => x.Type == EntryType.Dir));
        for (var d = 0; d < 100; d++)
        {
            var expectedFileName = $"New Dir{d}";
            var entry = entries.FirstOrDefault(x => x.Name == expectedFileName)?.Name ?? string.Empty;
            Assert.Equal(expectedFileName, entry);
            
        }
    }
    
    [Fact]
    [Trait("Category", "PFS3")]
    public async Task WhenCreateAndList100DirectoriesInRootDirectoryThenDirectoriesExist()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create 100 directories in root directory
        var expectedEntries = Enumerable.Range(0, 100).Select(x => $"New Dir{x}").OrderBy(x => x)
            .ToList();
        for (var i = 0; i < 100; i++)
        {
            await pfs3Volume.CreateDirectory(expectedEntries[i]);
        }

        // assert - list entries contains directories in root directory
        var entries = (await pfs3Volume.ListEntries())
            .OrderBy(x => x.Name).ToList();
        Assert.Equal(100, entries.Count);
        Assert.Equal(100, entries.Count(x => x.Type == EntryType.Dir));
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(expectedEntries[i], entries[i].Name);
        }
    }

    [Fact]
    [Trait("Category", "PFS3")]
    public async Task WhenCreateAndList100FilesInRootDirectoryThenFilesExist()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create 100 files in root directory
        var expectedEntries = Enumerable.Range(0, 100).Select(x => $"New File{x}").OrderBy(x => x)
            .ToList();
        for (var i = 0; i < 100; i++)
        {
            await pfs3Volume.CreateFile(expectedEntries[i]);
        }

        // assert - list entries contains files in root directory
        var entries = (await pfs3Volume.ListEntries())
            .OrderBy(x => x.Name).ToList();
        Assert.Equal(100, entries.Count);
        Assert.Equal(100, entries.Count(x => x.Type == EntryType.File));
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(expectedEntries[i], entries[i].Name);
        }
    }

    [Fact]
    [Trait("Category", "PFS3")]
    public async Task WhenCreateAndSearchFor100DirectoriesInRootDirectoryThenDirectoriesAreFound()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create 100 directories in root directory
        for (var i = 1; i <= 100; i++)
        {
            await pfs3Volume.CreateDirectory($"New Dir{i}");
        }

        // act - search for directories created in root directory
        var objectInfo = new objectinfo();

        for (var i = 1; i <= 100; i++)
        {
            var name = $"New Dir{i}";
            
            // act - search for directory created in root directory
            var result = await Directory.SearchInDir(Constants.ANODE_ROOTDIR, name, objectInfo, pfs3Volume.g);

            // assert - search returned true, directory exists
            Assert.Equal(name, result ? name : string.Empty);
        }
    }

    [Theory]
    [InlineData(DiskSize100Mb)]
    [InlineData(DiskSize4Gb)]
    [InlineData(DiskSize16Gb)]
    public async Task WhenCreateMultipleDirectoriesInRootDirectoryThenDirectoriesExist(long diskSize)
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk(diskSize);

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create "New Dir1", "New Dir2", "New Dir3" in root directory
        await pfs3Volume.CreateDirectory("New Dir1");
        await pfs3Volume.CreateDirectory("New Dir2");
        await pfs3Volume.CreateDirectory("New Dir3");

        // act - list entries in root directory
        var entries = (await pfs3Volume.ListEntries()).ToList();

        // assert - root directory contains directories created
        Assert.Equal(3, entries.Count);
        Assert.Equal(1, entries.Count(x => x.Name == "New Dir1" && x.Type == EntryType.Dir));
        Assert.Equal(1, entries.Count(x => x.Name == "New Dir2" && x.Type == EntryType.Dir));
        Assert.Equal(1, entries.Count(x => x.Name == "New Dir3" && x.Type == EntryType.Dir));
    }

    [Theory]
    [InlineData(DiskSize100Mb)]
    [InlineData(DiskSize4Gb)]
    [InlineData(DiskSize16Gb)]
    public async Task WhenCreateDirectoryInSubDirectoryThenDirectoryExist(long diskSize)
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk(diskSize);

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create "New Dir1" in root directory
        await pfs3Volume.CreateDirectory("New Dir");

        // act - change directory to "New Dir", create "Sub Dir" directory
        await pfs3Volume.ChangeDirectory("New Dir");
        await pfs3Volume.CreateDirectory("Sub Dir");

        // act - change to root directory and list entries
        await pfs3Volume.ChangeDirectory("/");
        var entries = (await pfs3Volume.ListEntries()).ToList();

        // assert - root directory contains directory created
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "New Dir" && x.Type == EntryType.Dir));

        await pfs3Volume.ChangeDirectory("New Dir");
        entries = (await pfs3Volume.ListEntries()).ToList();

        // assert - "New Dir" directory contains directory created
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "Sub Dir" && x.Type == EntryType.Dir));

        await pfs3Volume.ChangeDirectory("/");
        await pfs3Volume.ChangeDirectory("New Dir");
        await pfs3Volume.ChangeDirectory("Sub Dir");
        entries = (await pfs3Volume.ListEntries()).ToList();
        Assert.Empty(entries);
    }

    [Theory]
    [InlineData(DiskSize100Mb)]
    [InlineData(DiskSize4Gb)]
    [InlineData(DiskSize16Gb)]
    public async Task WhenCreateNewFileInRootThenFileExist(long diskSize)
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk(diskSize);

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create file in root directory
        await pfs3Volume.CreateFile("New File");

        // act - list entries in root directory
        var entries = (await pfs3Volume.ListEntries()).ToList();

        // assert - root directory contains file created
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "New File" && x.Type == EntryType.File));
    }

    [Fact]
    public async Task WhenOpenFileInWriteModeAndFileExistsThenExceptionIsThrown()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - open file in root directory in write mode, creates new file
        await pfs3Volume.OpenFile("New File", FileMode.Write);

        // act - open file in root directory in write mode, overwrites file
        await pfs3Volume.OpenFile("New File", FileMode.Write);

        // act - list entries in root directory
        var entries = (await pfs3Volume.ListEntries()).ToList();

        // assert - root directory contains file created
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "New File" && x.Type == EntryType.File));
    }

    [Fact]
    public async Task WhenOpenFileInAppendModeAndFileExistsThenFileIsOpened()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - open file in root directory in write mode, creates new file
        await pfs3Volume.OpenFile("New File", FileMode.Write);

        // act - open file in root directory in append mode
        await pfs3Volume.OpenFile("New File", FileMode.Append);

        // act - list entries in root directory
        var entries = (await pfs3Volume.ListEntries()).ToList();

        // assert - root directory contains file created
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "New File" && x.Type == EntryType.File));
    }

    [Fact]
    public async Task WhenOpenFileInReadModeAndFileDoesntExistsThenExceptionIsThrown()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - open file in root directory in read mode, creates new file
        await Assert.ThrowsAsync<PathNotFoundException>(
            async () => await pfs3Volume.OpenFile("New File", FileMode.Read));
    }

    [Fact]
    public async Task WhenCreateAndOverwriteFileWithSmallerOneThenLessBytesAreFree()
    {
        // arrange - data to write
        var data = new byte[10000];
        Array.Fill<byte>(data, 1);

        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - get free bytes for comparison
        var freeBytesAtStart = pfs3Volume.Free;

        // act - create file
        await pfs3Volume.CreateFile("New File");

        // act - write data
        await using (var entryStream = await pfs3Volume.OpenFile("New File", FileMode.Append))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        // arrange - flush changes
        await pfs3Volume.Flush();

        // assert - free bytes after writing 1st time is smaller than at start
        var freeBytesAfter1StFile = pfs3Volume.Free;
        Assert.True(freeBytesAfter1StFile < freeBytesAtStart);

        // arrange - data to write
        data = new byte[1000];
        Array.Fill<byte>(data, 1);

        // act - create file and overwrite existing
        await pfs3Volume.CreateFile("New File", true);

        // act - write data
        await using (var entryStream = await pfs3Volume.OpenFile("New File", FileMode.Append))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        // arrange - flush changes
        await pfs3Volume.Flush();

        // assert - free bytes after writing 2nd time is larger 1st time (overwritten file is smaller)
        var freeBytesAfter2NdFile = pfs3Volume.Free;
        Assert.True(freeBytesAfter2NdFile > freeBytesAfter1StFile);

        // act - read data
        int bytesRead;
        byte[] dataRead;
        await using (var entryStream = await pfs3Volume.OpenFile("New File", FileMode.Read))
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
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - create file
        await pfs3Volume.CreateFile("New File");

        // arrange - file only has delete protection bit set
        await pfs3Volume.SetProtectionBits("New File", ProtectionBits.Delete);

        // act - open file in read mode then exception is thrown
        await Assert.ThrowsAsync<FileSystemException>(async () => await pfs3Volume.OpenFile("New File", FileMode.Read));
    }

    [Fact]
    public async Task WhenOpenFileWithoutReadBitAndIgnoreProtectionBitsThenFileOpened()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // arrange - create file
        await pfs3Volume.CreateFile("New File");

        // arrange - file only has delete protection bit set
        await pfs3Volume.SetProtectionBits("New File", ProtectionBits.Delete);

        // act - open file in read mode then exception is thrown
        await using var entryStream = await pfs3Volume.OpenFile("New File", FileMode.Read, ignoreProtectionBits: true);
    }

    [Theory]
    [InlineData(DiskSize100Mb)]
    [InlineData(DiskSize4Gb)]
    [InlineData(DiskSize16Gb)]
    public async Task WhenCreateWriteDataToNewFileThenWhenReadDataFromFileDataMatches(long diskSize)
    {
        // arrange - data to write
        var data = AmigaTextHelper.GetBytes("New file with some text.");

        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk(diskSize);

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create file in root directory
        await pfs3Volume.CreateFile("New File");

        // act - write data
        await using (var entryStream = await pfs3Volume.OpenFile("New File", FileMode.Write))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        // act - read data
        int bytesRead;
        byte[] dataRead;
        await using (var entryStream = await pfs3Volume.OpenFile("New File", FileMode.Read))
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
    [Trait("Category", "PFS3")]
    public async Task WhenCreate100FilesWriteDataAndSetCommentThenFilesExistAndDataMatches()
    {
        // creating 2 or more file and setting comment for each file triggers calls to rename within dir
        
        // arrange - data to write
        var data = new byte[4000];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256);
        }

        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // act - read data
        int bytesRead;
        byte[] dataRead;

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);
        
        for (var i = 0; i < 100; i++)
        {
            // act - create file in root directory
            await pfs3Volume.CreateFile($"New File{i}");

            // act - write data
            await using (var entryStream = await pfs3Volume.OpenFile($"New File{i}", FileMode.Write))
            {
                await entryStream.WriteAsync(data, 0, data.Length);
            }

            // act - create file in root directory
            await pfs3Volume.SetComment($"New File{i}", $"Comment{i}");
        }

        // act - list entries
        var entries = (await pfs3Volume.ListEntries()).ToList();

        for (var i = 0; i < 100; i++)
        {
            var entry = entries.FirstOrDefault(x => x.Name == $"New File{i}");
            Assert.Equal($"New File{i}", entry?.Name ?? string.Empty);
            Assert.NotNull(entry);
            Assert.Equal($"Comment{i}", entry.Comment);
                
            await using (var entryStream = await pfs3Volume.OpenFile($"New File{i}", FileMode.Read))
            {
                dataRead = new byte[entryStream.Length];
                bytesRead = await entryStream.ReadAsync(dataRead, 0, dataRead.Length);
            }

            // assert - data read matches data written
            Assert.Equal(data.Length, bytesRead);
            Assert.Equal(data.Length, dataRead.Length);
            Assert.Equal(data, dataRead);
        }
    }

    [Fact]
    [Trait("Category", "PFS3")]
    public async Task WhenCreate200FilesWriteDataAndSetCommentThenFilesExistDataMatchesAndCacheIsEmptyAfterFlushing()
    {
        // arrange - data to write
        var data = new byte[4000];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256);
        }

        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();
        
        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        await pfs3Volume.CreateDirectory("New Dir");
        await pfs3Volume.ChangeDirectory("New Dir");
        
        // act - read data
        int bytesRead;
        byte[] dataRead;

        for (var i = 0; i < 200; i++)
        {
            // act - create file in root directory
            await pfs3Volume.CreateFile($"New File{i}");

            // act - write data
            await using (var entryStream = await pfs3Volume.OpenFile($"New File{i}", FileMode.Write))
            {
                await entryStream.WriteAsync(data, 0, data.Length);
            }

            // act - create file in root directory
            await pfs3Volume.SetComment($"New File{i}", $"Comment{i}");

            // act - flush pfs3 volume for every 10th file
            if (i % 10 == 0)
            {
                await pfs3Volume.Flush();
            }
        }

        // act - flush changes
        await pfs3Volume.Flush();
        
        // act - list entries
        var entries = (await pfs3Volume.ListEntries()).ToList();

        for (var i = 0; i < 200; i++)
        {
            var entry = entries.FirstOrDefault(x => x.Name == $"New File{i}");
            Assert.Equal($"New File{i}", entry?.Name ?? string.Empty);
            Assert.NotNull(entry);
            Assert.Equal($"Comment{i}", entry.Comment);
                
            await using (var entryStream = await pfs3Volume.OpenFile($"New File{i}", FileMode.Read))
            {
                dataRead = new byte[entryStream.Length];
                bytesRead = await entryStream.ReadAsync(dataRead, 0, dataRead.Length);
            }

            // assert - data read matches data written
            Assert.Equal(data.Length, bytesRead);
            Assert.Equal(data.Length, dataRead.Length);
            Assert.Equal(data, dataRead);
        }

        // act - flush changes
        await pfs3Volume.Flush();

        // assert - cache is empty
        Assert.Empty(pfs3Volume.g.glob_lrudata.LRUarray);
        Assert.Empty(pfs3Volume.g.glob_lrudata.LRUpool);
        Assert.Empty(pfs3Volume.g.glob_lrudata.LRUqueue);
        
        // assert - volume is empty
        Assert.Empty(pfs3Volume.g.currentvolume.anodechainlist);
        Assert.Empty(pfs3Volume.g.currentvolume.anblks);
        Assert.Empty(pfs3Volume.g.currentvolume.bmblks);
        Assert.Empty(pfs3Volume.g.currentvolume.dirblks);
        Assert.Empty(pfs3Volume.g.currentvolume.deldirblks);
        Assert.Empty(pfs3Volume.g.currentvolume.bmindexblks);
        Assert.Empty(pfs3Volume.g.currentvolume.indexblks);
        Assert.Empty(pfs3Volume.g.currentvolume.superblks);
    }

    [Fact]
    [Trait("Category", "PFS3")]
    public async Task WhenCreate100FilesAndRenameAndSetCommentThenFilesExistAndDataMatches()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        const int files = 100;
        for (var i = 0; i < files; i++)
        {
            // act - create file in root directory
            await pfs3Volume.CreateFile($"New File{i}");
        }

        // act - flush changes
        await pfs3Volume.Flush();

        IList<Entry> entries;
        for (var i = 0; i < files; i++)
        {
            // act - rename file with longer name
            await pfs3Volume.Rename($"New File{i}", $"Renamed to longer filename{i}");

            // assert - renamed file exist once
            entries = (await pfs3Volume.ListEntries()).ToList();
            var renamed = entries.Count(x => x.Name == $"Renamed to longer filename{i}");
            Assert.Equal(1, renamed);
            
            // act - set comment for file
            await pfs3Volume.SetComment($"Renamed to longer filename{i}", $"CommentCommentCommentComment{i}");
            
            // assert - file with comment exist once
            entries = (await pfs3Volume.ListEntries()).ToList();
            var renamedWithComment = entries.Count(x => x.Name == $"Renamed to longer filename{i}");
            Assert.Equal(1, renamedWithComment);
        }

        // act - flush changes
        await pfs3Volume.Flush();
        
        // act - list entries
        entries = (await pfs3Volume.ListEntries()).OrderBy(x => x.Name).ToList();
            
        // assert - 100 entries exist and matches
        Assert.Equal(files, entries.Count);
        for (var i = 0; i < files; i++)
        {
            var entry = entries.FirstOrDefault(x => x.Name == $"Renamed to longer filename{i}");
            Assert.Equal($"Renamed to longer filename{i}", entry?.Name ?? string.Empty);
            Assert.NotNull(entry);
            Assert.Equal($"CommentCommentCommentComment{i}", entry.Comment);
        }
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

        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create file in root directory
        await pfs3Volume.CreateFile("New File");

        // act - write data
        await using (var entryStream = await pfs3Volume.OpenFile("New File", FileMode.Write))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        // act - read data
        int bytesRead;
        byte[] dataRead;
        await using (var entryStream = await pfs3Volume.OpenFile("New File", FileMode.Read))
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
    [InlineData(DiskSize100Mb)]
    [InlineData(DiskSize4Gb)]
    [InlineData(DiskSize16Gb)]
    public async Task WhenCreateWriteDataToNewFileThenWhenSeekAndReadDataMatches(long diskSize)
    {
        // arrange - data to write
        var data = AmigaTextHelper.GetBytes("New file with some text.");

        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk(diskSize);

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create file in root directory
        await pfs3Volume.CreateFile("New File");

        // act - write data
        await using (var entryStream = await pfs3Volume.OpenFile("New File", FileMode.Write))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        // act - read data
        int bytesRead;
        byte[] dataRead;
        long seekPosition;
        long readPosition;
        await using (var entryStream = await pfs3Volume.OpenFile("New File", FileMode.Read))
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
    [InlineData(DiskSize100Mb)]
    [InlineData(DiskSize4Gb)]
    [InlineData(DiskSize16Gb)]
    public async Task WhenCreateAndDeleteFileInRootThenFileDoesntExist(long diskSize)
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk(diskSize);

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create file in root directory
        await pfs3Volume.CreateFile("New File");

        // act - delete file from root directory
        await pfs3Volume.Delete("New File");

        // act - list entries in root directory
        var entries = (await pfs3Volume.ListEntries()).ToList();

        // assert - root directory is empty
        Assert.Empty(entries);
    }

    [Theory]
    [InlineData(DiskSize100Mb)]
    [InlineData(DiskSize4Gb)]
    [InlineData(DiskSize16Gb)]
    public async Task WhenCreateTwoFilesAndDeleteOneFileInRootThenOneFileExists(long diskSize)
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk(diskSize);

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create file in root directory
        await pfs3Volume.CreateFile("New File 1");
        await pfs3Volume.CreateFile("New File 2");

        // act - delete file from root directory
        await pfs3Volume.Delete("New File 1");

        // act - list entries in root directory
        var entries = (await pfs3Volume.ListEntries()).ToList();

        // assert - root directory contains new file 1
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "New File 2" && x.Type == EntryType.File));
    }

    [Theory]
    [InlineData(DiskSize100Mb)]
    [InlineData(DiskSize4Gb)]
    [InlineData(DiskSize16Gb)]
    public async Task WhenCreateAndDeleteDirectoryInRootDirectoryThenDirectoryDoesntExist(long diskSize)
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk(diskSize);

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create "New Dir" in root directory
        await pfs3Volume.CreateDirectory("New Dir");

        // act - delete directory from root directory
        await pfs3Volume.Delete("New Dir");

        // act - list entries in root directory
        var entries = (await pfs3Volume.ListEntries()).ToList();

        // assert - root directory is empty
        Assert.Empty(entries);
    }

    [Theory]
    [InlineData(DiskSize100Mb)]
    [InlineData(DiskSize4Gb)]
    [InlineData(DiskSize16Gb)]
    public async Task WhenCreateAndRenameFileInRootThenFileIsRenamed(long diskSize)
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk(diskSize);

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create file in root directory
        await pfs3Volume.CreateFile("New File");

        // act - rename file in root directory
        await pfs3Volume.Rename("New File", "Renamed File");

        // act - list entries in root directory
        var entries = (await pfs3Volume.ListEntries()).ToList();

        // assert - root directory contains file created
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "Renamed File" && x.Type == EntryType.File));
    }

    [Theory]
    [InlineData(DiskSize100Mb)]
    [InlineData(DiskSize4Gb)]
    [InlineData(DiskSize16Gb)]
    public async Task WhenMoveFileFromRootDirectoryToSubdirectoryThenFileIsLocatedInSubdirectory(long diskSize)
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk(diskSize);

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create "New Dir" in root directory
        await pfs3Volume.CreateDirectory("New Dir");

        // act - create file in root directory
        await pfs3Volume.CreateFile("New File");

        // act - move file from root directory to subdirectory
        await pfs3Volume.Rename("New File", "New Dir/Moved File");

        // act - mount pfs3 volume, list entries in root directory and unmount pfs3 volume
        var rootEntries = (await pfs3Volume.ListEntries()).ToList();
        await pfs3Volume.ChangeDirectory("New Dir");
        var subDirEntries = (await pfs3Volume.ListEntries()).ToList();

        // assert - root directory contains directory
        Assert.Single(rootEntries);
        Assert.Equal(1, rootEntries.Count(x => x.Name == "New Dir" && x.Type == EntryType.Dir));

        // assert - sub directory contains moved file
        Assert.Single(subDirEntries);
        Assert.Equal(1, subDirEntries.Count(x => x.Name == "Moved File" && x.Type == EntryType.File));
    }

    [Theory]
    [InlineData(DiskSize100Mb)]
    [InlineData(DiskSize4Gb)]
    [InlineData(DiskSize16Gb)]
    public async Task WhenSetCommentForFileInRootThenCommentIsChanged(long diskSize)
    {
        // arrange - comment to set
        var comment = "Comment for file";

        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk(diskSize);

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create file in root directory
        await pfs3Volume.CreateFile("New File");

        // act - set comment for file in root directory
        await pfs3Volume.SetComment("New File", comment);

        // act - list entries in root directory
        var entries = (await pfs3Volume.ListEntries()).ToList();

        // assert - root directory contains file created
        Assert.Single(entries);
        var dirEntry = entries.FirstOrDefault(x => x.Name == "New File" && x.Type == EntryType.File);
        Assert.NotNull(dirEntry);
        Assert.Equal(comment, dirEntry.Comment);
    }

    [Theory]
    [InlineData(DiskSize100Mb)]
    [InlineData(DiskSize4Gb)]
    [InlineData(DiskSize16Gb)]
    public async Task WhenSetProtectionBitsForFileInRootThenProtectionBitsAreChanged(long diskSize)
    {
        // arrange - protection bits to set
        var protectionBits = ProtectionBits.Delete | ProtectionBits.Executable | ProtectionBits.Write |
                             ProtectionBits.Read | ProtectionBits.HeldResident | ProtectionBits.Archive |
                             ProtectionBits.Pure | ProtectionBits.Script;

        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk(diskSize);

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create file in root directory
        await pfs3Volume.CreateFile("New File");

        // act - set protection bits for file in root directory
        await pfs3Volume.SetProtectionBits("New File", protectionBits);

        // act - list entries in root directory
        var entries = (await pfs3Volume.ListEntries()).ToList();

        // assert - root directory contains file created
        Assert.Single(entries);
        var dirEntry = entries.FirstOrDefault(x => x.Name == "New File" && x.Type == EntryType.File);
        Assert.NotNull(dirEntry);
        Assert.Equal(protectionBits, dirEntry.ProtectionBits);
    }

    [Theory]
    [InlineData(DiskSize100Mb)]
    [InlineData(DiskSize4Gb)]
    [InlineData(DiskSize16Gb)]
    public async Task WhenSetDateForFileInRootThenCreationDateIsChanged(long diskSize)
    {
        // arrange - date to set
        var date = DateTime.Now.AddDays(-10).Trim(TimeSpan.TicksPerSecond);

        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk(diskSize);

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create file in root directory
        await pfs3Volume.CreateFile("New File");

        // act - set date for file in root directory
        await pfs3Volume.SetDate("New File", date);

        // act - list entries in root directory
        var entries = (await pfs3Volume.ListEntries()).ToList();

        // assert - root directory contains file created
        Assert.Single(entries);
        var dirEntry = entries.FirstOrDefault(x => x.Name == "New File" && x.Type == EntryType.File);
        Assert.NotNull(dirEntry);
        Assert.Equal(date, dirEntry.Date);
    }

    [Fact]
    [Trait("Category", "PFS3")]
    public async Task WhenCreate2FilesIn2DirsWithProtectionBitsAndDateSetThenEntriesExistAndDataMatches()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create file 1 in root directory
        await pfs3Volume.CreateFile("File1", true, true);

        // act - write file 1 data
        var file1Data = BlockTestHelper.CreateBlockBytes(900);
        await using (var entryStream = await pfs3Volume.OpenFile("File1", FileMode.Write))
        {
            await entryStream.WriteAsync(file1Data, 0, file1Data.Length);
        }

        // act - set new file 1 protection bits
        var file1ProtectionBits = ProtectionBits.Read | ProtectionBits.Write;
        await pfs3Volume.SetProtectionBits("File1", file1ProtectionBits);

        // act - set new file 1 date
        var file1Date = DateTime.Now.AddDays(-5).Trim(TimeSpan.TicksPerSecond);
        await pfs3Volume.SetDate("File1", file1Date);

        // act - change to root directory
        await pfs3Volume.ChangeDirectory("/");

        // act - create dir sub-directory
        await pfs3Volume.CreateDirectory("Dir");

        // act - change to dir sub-directory
        await pfs3Volume.ChangeDirectory("Dir");

        // act - create file 2 in dir sub-directory
        await pfs3Volume.CreateFile("File2", true, true);

        // act - write file 2 data in chunks of 512 bytes
        var file2Data = BlockTestHelper.CreateBlockBytes(100000); // File2 size 983040 did also trigger the issue, but takes longer time 
        var file2DataStream = new MemoryStream(file2Data);
        int bytesRead;
        await using (var entryStream = await pfs3Volume.OpenFile("File2", FileMode.Append))
        {
            var buffer = new byte[4096];
            do
            {
                bytesRead = await file2DataStream.ReadAsync(buffer, 0, buffer.Length);
                await entryStream.WriteAsync(buffer, 0, bytesRead);
            } while (bytesRead == buffer.Length);
        }

        // act - set new file 2 protection bits
        var file2ProtectionBits = ProtectionBits.Read | ProtectionBits.Delete;
        await pfs3Volume.SetProtectionBits("File2", file2ProtectionBits);

        // act - set new file 2 date
        var file2Date = DateTime.Now.AddDays(-3).Trim(TimeSpan.TicksPerSecond);
        await pfs3Volume.SetDate("File2", file2Date);

        // act - change to root directory
        await pfs3Volume.ChangeDirectory("/");

        // act - list entries in root directory
        var entries = (await pfs3Volume.ListEntries()).ToList();

        // assert - root directory contains file 1 and dir entries
        Assert.Equal(2, entries.Count);
        var file1Entry = entries.FirstOrDefault(x => x.Name == "File1" && x.Type == EntryType.File);
        Assert.NotNull(file1Entry);
        Assert.Equal(file1Date, file1Entry.Date);
        Assert.Equal(file1ProtectionBits, file1Entry.ProtectionBits);
        var dirEntry = entries.FirstOrDefault(x => x.Name == "Dir" && x.Type == EntryType.Dir);
        Assert.NotNull(dirEntry);

        // assert - data read from file 1 matches
        byte[] actualFile1Data;
        await using (var entryStream = await pfs3Volume.OpenFile("File1", FileMode.Read))
        {
            actualFile1Data = new byte[entryStream.Length];
            bytesRead = await entryStream.ReadAsync(actualFile1Data, 0, actualFile1Data.Length);
        }

        Assert.Equal(file1Data.Length, bytesRead);
        Assert.Equal(file1Data.Length, actualFile1Data.Length);
        Assert.Equal(file1Data, actualFile1Data);

        // act - change to dir sub-directory
        await pfs3Volume.ChangeDirectory("Dir");

        // act - list entries in root directory
        entries = (await pfs3Volume.ListEntries()).ToList();

        // assert - root directory contains file 2 entry
        Assert.Single(entries);
        var file2Entry = entries.FirstOrDefault(x => x.Name == "File2" && x.Type == EntryType.File);
        Assert.NotNull(file2Entry);
        Assert.Equal(file2Date, file2Entry.Date);
        Assert.Equal(file2ProtectionBits, file2Entry.ProtectionBits);

        // assert - data read from file 2 matches
        byte[] actualFile2Data;
        await using (var entryStream = await pfs3Volume.OpenFile("File2", FileMode.Read))
        {
            actualFile2Data = new byte[entryStream.Length];
            bytesRead = await entryStream.ReadAsync(actualFile2Data, 0, actualFile2Data.Length);
        }

        Assert.Equal(file2Data.Length, bytesRead);
        Assert.Equal(file2Data.Length, actualFile2Data.Length);
        Assert.Equal(file2Data, actualFile2Data);
    }
    
    [Fact]
    public async Task WhenOverwriteExistingFileInDirectoryWith10FilesThenEntryMatchesOverwrittenData()
    {
        // arrange - data to write
        var initialData = BlockTestHelper.CreateBlockBytes(1200);
        var overwrittenData = AmigaTextHelper.GetBytes("Only Amiga makes it possible");

        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // act - mount pfs3 volume
        using (var pfs3Volume = await MountVolume(stream))
        {
            for (var i = 0; i < 10; i++)
            {
                // act - create file in root directory
                await pfs3Volume.CreateFile($"New File{i}");

                // act - write data
                await using (var entryStream = await pfs3Volume.OpenFile($"New File{i}", FileMode.Write))
                {
                    await entryStream.WriteAsync(initialData, 0, initialData.Length);
                }
            }
        }
        
        // act - mount pfs3 volume
        using (var pfs3Volume = await MountVolume(stream))
        {
            // act - create file in root directory
            await pfs3Volume.CreateFile("New File8", true, true);

            // act - list entries in root directory
            var entries = (await pfs3Volume.ListEntries()).ToList();
            
            Assert.Equal(10, entries.Count);
            
            // act - write file 1 data
            await using (var entryStream = await pfs3Volume.OpenFile("New File8", FileMode.Write))
            {
                await entryStream.WriteAsync(overwrittenData, 0, overwrittenData.Length);
            }
        }

        using (var pfs3Volume = await MountVolume(stream))
        {
            // act - list entries in root directory
            var entries = (await pfs3Volume.ListEntries()).ToList();

            Assert.Equal(10, entries.Count);

            // assert - new file 0-9 exists and matches initial data in size
            Entry entry;
            for (var i = 0; i < 10; i++)
            {
                // skip new file 8, asserted separately below
                if (i == 8)
                {
                    continue;
                }
                
                // assert - file exists and matches initial data
                entry = entries.FirstOrDefault(x => x.Name == $"New File{i}" && x.Type == EntryType.File);
                Assert.Equal($"New File{i}", entry?.Name ?? string.Empty);
                Assert.NotNull(entry);
                Assert.Equal(initialData.Length, entry.Size);
            }
            
            // assert - file exists and matches initial data
            entry = entries.FirstOrDefault(x => x.Name == $"New File8" && x.Type == EntryType.File);
            Assert.Equal($"New File8", entry?.Name ?? string.Empty);
            Assert.NotNull(entry);
            Assert.Equal(overwrittenData.Length, entry.Size);
        }
    }
    
    [Fact]
    public async Task WhenFindDirectoryAndFileEntryThenEntryIsReturned()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();
        
        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create directory
        await pfs3Volume.CreateDirectory("New Dir");

        // act - change directory
        await pfs3Volume.ChangeDirectory("New Dir");
        
        // act - create file
        await pfs3Volume.CreateFile("New File");

        // act - change directory
        await pfs3Volume.ChangeDirectory("/");

        // act - find new dir entry
        var result = await pfs3Volume.FindEntry("New Dir");
        
        // assert - file entry is found and matches
        Assert.NotNull(result);
        Assert.Empty(result.PartsNotFound);
        Assert.NotNull(result.Entry);
        Assert.Equal("New Dir", result.Entry.Name);
        Assert.Equal(EntryType.Dir, result.Entry.Type);
        
        // act - change directory
        await pfs3Volume.ChangeDirectory("New Dir");
        
        // act - find new file entry
        result = await pfs3Volume.FindEntry("New File");
        
        // assert - file entry is found and matches
        Assert.NotNull(result);
        Assert.Empty(result.PartsNotFound);
        Assert.NotNull(result.Entry);
        Assert.Equal("New File", result.Entry.Name);
        Assert.Equal(EntryType.File, result.Entry.Type);
    }

    [Fact]
    [Trait("Category", "PFS3")]
    public async Task WhenOverwriting100FilesIn3DirectoriesThenEntriesExistAndDataMatches()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();
        
        // arrange - data to write
        var data = new byte[100];
        
        // iterate 2 times: first creates files, second overwrites files
        for (var iteration = 0; iteration < 2; iteration++)
        {
            // arrange - mount pfs3 volume
            await using var pfs3Volume = await MountVolume(stream);

            // iterate 3 directories
            for (var dir = 0; dir < 5; dir++)
            {
                // act - change to root directory
                await pfs3Volume.ChangeDirectory("/");

                // act - create directory, if first iteration
                var dirName = $"New Dir{dir}";
                if (iteration == 0)
                {
                    await pfs3Volume.CreateDirectory(dirName);
                }

                // act - change directory
                await pfs3Volume.ChangeDirectory(dirName);

                // iterate 100 files
                for (var file = 0; file < 100; file++)
                {
                    // act - create file
                    var fileName = $"New File{file}";
                    await pfs3Volume.CreateFile(fileName, true);

                    // act - write data
                    await using (var entryStream = await pfs3Volume.OpenFile(fileName, FileMode.Append))
                    {
                        Array.Fill(data, (byte)file);

                        await entryStream.WriteAsync(data, 0, data.Length);
                        await entryStream.FlushAsync();
                    }

                    await pfs3Volume.Flush();
                    
                    // act - data read from file
                    byte[] actualFileData;
                    int bytesRead;
                    await using (var entryStream = await pfs3Volume.OpenFile(fileName, FileMode.Read))
                    {
                        actualFileData = new byte[entryStream.Length];
                        bytesRead = await entryStream.ReadAsync(actualFileData, 0, actualFileData.Length);
                    }

                    // assert - data read matches
                    Array.Fill(data, (byte)file);
                    Assert.Equal(data.Length, bytesRead);
                    Assert.Equal(data.Length, actualFileData.Length);
                    Assert.Equal(data, actualFileData);
                    
                }
            }
        }

        // arrange - mount pfs3 volume
        await using var pfs3Volume2 = await MountVolume(stream);

        // assert - 3 directories exist
        for (var dir = 0; dir < 5; dir++)
        {
            // act - change to root directory
            await pfs3Volume2.ChangeDirectory("/");

            // act - find dir entry
            var dirName = $"New Dir{dir}";
            var dirResult = await pfs3Volume2.FindEntry(dirName);
            
            // assert - dir entry is found and matches
            Assert.NotNull(dirResult);
            Assert.Empty(dirResult.PartsNotFound);
            Assert.NotNull(dirResult.Entry);
            Assert.Equal(dirName, dirResult.Entry.Name);
            Assert.Equal(EntryType.Dir, dirResult.Entry.Type);
            
            // act - change directory
            await pfs3Volume2.ChangeDirectory(dirName);

            // assert - 100 files exist
            for (var file = 0; file < 100; file++)
            {
                // act - find file entry
                var fileName = $"New File{file}";
                var fileResult = await pfs3Volume2.FindEntry(fileName);
            
                // assert - file entry is found and matches
                Assert.NotNull(fileResult);
                Assert.Empty(fileResult.PartsNotFound);
                Assert.NotNull(fileResult.Entry);
                Assert.Equal(fileName, fileResult.Entry.Name);
                Assert.Equal(EntryType.File, fileResult.Entry.Type);
                
                // act - data read from file
                byte[] actualFileData;
                int bytesRead;
                await using (var entryStream = await pfs3Volume2.OpenFile(fileName, FileMode.Read))
                {
                    actualFileData = new byte[entryStream.Length];
                    bytesRead = await entryStream.ReadAsync(actualFileData, 0, actualFileData.Length);
                }

                // assert - data read matches
                Array.Fill(data, (byte)file);
                Assert.Equal(data.Length, bytesRead);
                Assert.Equal(data.Length, actualFileData.Length);
                Assert.Equal(data, actualFileData);
            }
        }
    }

    [Fact]
    [Trait("Category", "PFS3")]
    public async Task WhenOverwriting100FilesThenEntriesExistAndDataMatches()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - data to write
        var data = new byte[100];

        // iterate 100 files
        for (var file = 0; file < 100; file++)
        {
            // arrange - mount pfs3 volume
            await using var pfs3Volume = await MountVolume(stream);
            
            // act - create file
            var fileName = $"New File{file}";
            await pfs3Volume.CreateFile(fileName, true);

            // act - write data
            await using (var entryStream = await pfs3Volume.OpenFile(fileName, FileMode.Append))
            {
                Array.Fill(data, (byte)file);

                await entryStream.WriteAsync(data, 0, data.Length);
                await entryStream.FlushAsync();
            }
        }

        // arrange - mount pfs3 volume
        await using (var pfs3Volume = await MountVolume(stream))
        {
            // iterate 100 files
            for (var file = 0; file < 100; file++)
            {
                // act - create file
                var fileName = $"New File{file}";

                // act - data read from file
                byte[] actualFileData;
                int bytesRead;
                await using (var entryStream = await pfs3Volume.OpenFile(fileName, FileMode.Read))
                {
                    actualFileData = new byte[entryStream.Length];
                    bytesRead = await entryStream.ReadAsync(actualFileData, 0, actualFileData.Length);
                }

                // assert - data read matches
                Array.Fill(data, (byte)file);
                Assert.Equal(data.Length, bytesRead);
                Assert.Equal(data.Length, actualFileData.Length);
                Assert.Equal(data, actualFileData);
            }
        }
    }
    
    [Fact]
    public async Task WhenWriteFileDataLessThanBlockSizeThenDataMatches()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - data to write
        var data = new byte[100];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)i;
        }
        var fileName = "New File";
        
        // arrange - mount pfs3 volume
        await using (var pfs3Volume = await MountVolume(stream))
        {
            // act - create file
            await pfs3Volume.CreateFile(fileName, true);

            // act - write data
            await using (var entryStream = await pfs3Volume.OpenFile(fileName, FileMode.Append))
            {
                await entryStream.WriteAsync(data, 0, data.Length);
                await entryStream.FlushAsync();
            }
        }

        await using (var pfs3Volume = await MountVolume(stream))
        {
            // act - find file entry
            var fileResult = await pfs3Volume.FindEntry(fileName);

            // assert - file entry is found and matches
            Assert.NotNull(fileResult);
            Assert.Empty(fileResult.PartsNotFound);
            Assert.NotNull(fileResult.Entry);
            Assert.Equal(fileName, fileResult.Entry.Name);
            Assert.Equal(EntryType.File, fileResult.Entry.Type);

            // act - data read from file
            byte[] actualFileData;
            int bytesRead;
            await using (var entryStream = await pfs3Volume.OpenFile(fileName, FileMode.Read))
            {
                actualFileData = new byte[entryStream.Length];
                bytesRead = await entryStream.ReadAsync(actualFileData, 0, actualFileData.Length);
            }

            // assert - data read matches
            Assert.Equal(data.Length, bytesRead);
            Assert.Equal(data.Length, actualFileData.Length);
            Assert.Equal(data, actualFileData);
        }
    }

    [Fact]
    [Trait("Category", "PFS3")]
    public async Task WhenOverwriting100FilesIn5Directories3TimesThenEntriesExistAndDataMatches()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();
        
        // arrange - data to write
        var data = new byte[100];
        Array.Fill<byte>(data, 1);
            
        // arrange - mount pfs3 volume
        await using (var pfs3Volume = await MountVolume(stream))
        {
            // iterate 3 times        
            for (var iteration = 0; iteration < 3; iteration++)
            {
                // iterate 5 directories
                for (var dir = 0; dir < 5; dir++)
                {
                    // act - change to root directory
                    await pfs3Volume.ChangeDirectory("/");

                    // act - create directory, if first iteration
                    var dirName = $"New Dir{dir}";
                    if (iteration == 0)
                    {
                        await pfs3Volume.CreateDirectory(dirName);
                    }

                    // act - change directory
                    await pfs3Volume.ChangeDirectory(dirName);

                    // iterate 100 files
                    for (var file = 0; file < 100; file++)
                    {
                        // act - create file
                        var fileName = $"New File{file}";
                        await pfs3Volume.CreateFile(fileName, true);

                        // act - write data
                        Array.Fill(data, (byte)file);
                        await using (var entryStream = await pfs3Volume.OpenFile(fileName, FileMode.Append))
                        {
                            await entryStream.WriteAsync(data, 0, data.Length);
                            await entryStream.FlushAsync();
                        }

                        await pfs3Volume.Flush();
                        
                        // act - data read from file
                        byte[] actualFileData;
                        int bytesRead;
                        await using (var entryStream = await pfs3Volume.OpenFile(fileName, FileMode.Read))
                        {
                            actualFileData = new byte[entryStream.Length];
                            bytesRead = await entryStream.ReadAsync(actualFileData, 0, actualFileData.Length);
                        }

                        // assert - data read matches
                        Assert.Equal(data.Length, bytesRead);
                        Assert.Equal(data.Length, actualFileData.Length);
                        Assert.Equal(data, actualFileData);
                    }
                }
            }
        }

        await using (var pfs3Volume = await MountVolume(stream))
        {
            // assert - 5 directories exist
            for (var dir = 0; dir < 5; dir++)
            {
                // act - change to root directory
                await pfs3Volume.ChangeDirectory("/");

                // act - find dir entry
                var dirName = $"New Dir{dir}";
                var dirResult = await pfs3Volume.FindEntry(dirName);

                // assert - dir entry is found and matches
                Assert.NotNull(dirResult);
                Assert.Empty(dirResult.PartsNotFound);
                Assert.NotNull(dirResult.Entry);
                Assert.Equal(dirName, dirResult.Entry.Name);
                Assert.Equal(EntryType.Dir, dirResult.Entry.Type);

                // act - change directory
                await pfs3Volume.ChangeDirectory(dirName);

                // assert - 100 files exist
                for (var file = 0; file < 100; file++)
                {
                    // act - find file entry
                    var fileName = $"New File{file}";
                    var fileResult = await pfs3Volume.FindEntry(fileName);

                    // assert - file entry is found and matches
                    Assert.NotNull(fileResult);
                    Assert.Empty(fileResult.PartsNotFound);
                    Assert.NotNull(fileResult.Entry);
                    Assert.Equal(fileName, fileResult.Entry.Name);
                    Assert.Equal(EntryType.File, fileResult.Entry.Type);

                    // act - data read from file
                    byte[] actualFileData;
                    int bytesRead;
                    await using (var entryStream = await pfs3Volume.OpenFile(fileName, FileMode.Read))
                    {
                        actualFileData = new byte[entryStream.Length];
                        bytesRead = await entryStream.ReadAsync(actualFileData, 0, actualFileData.Length);
                    }

                    // assert - data read matches
                    Array.Fill(data, (byte)file);
                    Assert.Equal(data.Length, bytesRead);
                    Assert.Equal(data.Length, actualFileData.Length);
                    Assert.Equal(data, actualFileData);
                }
            }
        }
    }
    
    [Fact]
    public async Task WhenFindEntryWithDirectorySeparatorInNameThenExceptionIsThrown()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();
        
        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act & assert - find entry with directory separator throws exception
        await Assert.ThrowsAsync<ArgumentException>(async () => await pfs3Volume.FindEntry("New Dir/New File"));
    }
    
    [Fact]
    public async Task WhenCreatingDirectoryAndFileWithSameNameThenExceptionIsThrown()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create directory
        const string name = "Dir";
        await pfs3Volume.CreateDirectory(name);
            
        // act & assert - create file with same name as directory
        await Assert.ThrowsAsync<PathAlreadyExistsException>(async () => await pfs3Volume.CreateFile(name));
    }

    [Fact]
    public async Task WhenCreatingDirectoryAndFileWithSameNameOverwritingExistingThenExceptionIsThrown()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - create directory
        const string name = "Dir";
        await pfs3Volume.CreateDirectory(name);
            
        // act & assert - create and overwrite file with same name as directory
        await Assert.ThrowsAsync<NotAFileException>(async () => await pfs3Volume.CreateFile(name, true, true));
    }
}