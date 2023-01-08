namespace Hst.Amiga.Tests.Pfs3Tests;

using System;
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
        var entry = await pfs3Volume.FindEntry("New Dir");

        // assert - entry exists and is equal
        Assert.NotNull(entry);
        Assert.Equal("New Dir", entry.Name);
        Assert.Equal(EntryType.Dir, entry.Type);
        Assert.Equal(0, entry.Size);
    }

    [Fact]
    public async Task WhenFindNonExistingEntryThenExceptionIsThrown()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - find entry
        var entry = await pfs3Volume.FindEntry("New Dir");

        // assert - entry is null, not found
        Assert.Null(entry);
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
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // assert - root directory contains directory created
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "New Dir" && x.Type == EntryType.Dir));
    }

    [Fact]
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
            // act - search for directory created in root directory
            var result = await Directory.SearchInDir(Constants.ANODE_ROOTDIR, $"New Dir{i}", objectInfo, pfs3Volume.g);

            // assert - search returned true, directory exists
            Assert.True(result);
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

    [Theory()]
    [InlineData(DiskSize100Mb)]
    [InlineData(DiskSize4Gb)]
    [InlineData(DiskSize16Gb)]
    public async Task WhenCreateWriteDataToNewFileAndOpenReadDataFromFile100TimesThenDataMatches(long diskSize)
    {
        // arrange - data to write
        var data = new byte[4000];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256);
        }

        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk(diskSize);

        // act - mount pfs3 volume
        await using var pfs3Volume = await MountVolume(stream);

        // act - read data
        int bytesRead;
        byte[] dataRead;

        for (var i = 0; i < 10; i++)
        {
            // act - create file in root directory
            await pfs3Volume.CreateFile($"New File{i}");

            // act - write data
            await using (var entryStream = await pfs3Volume.OpenFile($"New File{i}", FileMode.Write))
            {
                await entryStream.WriteAsync(data, 0, data.Length);
            }

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
    public async Task WhenSetCreationDateForFileInRootThenCreationDateIsChanged(long diskSize)
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
        var file2Data = BlockTestHelper.CreateBlockBytes(1024);
        var file2DataStream = new MemoryStream(file2Data);
        int bytesRead;
        await using (var entryStream = await pfs3Volume.OpenFile("File2", FileMode.Write))
        {
            var buffer = new byte[512];
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
}