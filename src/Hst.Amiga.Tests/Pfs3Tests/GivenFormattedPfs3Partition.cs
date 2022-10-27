namespace Hst.Amiga.Tests.Pfs3Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Extensions;
using FileSystems;
using FileSystems.Pfs3;
using Xunit;

public class GivenFormattedPfs3Disk : Pfs3TestBase
{
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
        await using (var entryStream = await pfs3Volume.OpenFile("New File", true))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        // act - read data
        int bytesRead;
        byte[] dataRead;
        await using (var entryStream = await pfs3Volume.OpenFile("New File", false))
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
        await using (var entryStream = await pfs3Volume.OpenFile("New File", true))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }

        // act - read data
        int bytesRead;
        byte[] dataRead;
        long seekPosition;
        long readPosition;
        await using (var entryStream = await pfs3Volume.OpenFile("New File", false))
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
        var protectionBits = ProtectionBits.Delete | ProtectionBits.Executable | ProtectionBits.Write | ProtectionBits.Read | ProtectionBits.HeldResident | ProtectionBits.Archive | ProtectionBits.Pure | ProtectionBits.Script;
        
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
}