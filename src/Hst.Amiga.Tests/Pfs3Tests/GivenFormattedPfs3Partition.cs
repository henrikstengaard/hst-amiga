namespace Hst.Amiga.Tests.Pfs3Tests;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileSystems;
using FileSystems.Pfs3;
using Xunit;

public class GivenFormattedPfs3Disk : Pfs3TestBase
{
    [Fact]
    public async Task WhenCreateDirectoryInRootDirectoryThenDirectoryExist()
    {
        // arrange - create pfs3 formatted disk
        await CreatePfs3FormattedDisk();

        // arrange - get first partition
        var partitionBlock = RigidDiskBlock.PartitionBlocks.First();

        // act - mount pfs3 volume, create "New Dir" in root directory and unmount pfs3 volume
        var pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        await pfs3Volume.CreateDirectory("New Dir");
        await Pfs3Helper.Unmount(pfs3Volume.g);
        
        // act - mount pfs3 volume, list entries in root directory and unmount pfs3 volume
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        var entries = (await pfs3Volume.ListEntries()).ToList();
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // assert - root directory contains directory created
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "New Dir" && x.Type == EntryType.Dir));
    }

    [Fact]
    public async Task WhenCreateMultipleDirectoriesInRootDirectoryThenDirectoriesExist()
    {
        // arrange - create pfs3 formatted disk
        await CreatePfs3FormattedDisk();

        // arrange - get first partition
        var partitionBlock = RigidDiskBlock.PartitionBlocks.First();

        // act - mount pfs3 volume, create "New Dir1" in root directory and unmount pfs3 volume
        var pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        await pfs3Volume.CreateDirectory("New Dir1");
        await Pfs3Helper.Unmount(pfs3Volume.g);
        
        // act - mount pfs3 volume, create "New Dir2" in root directory and unmount pfs3 volume
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        await pfs3Volume.CreateDirectory("New Dir2");
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // act - mount pfs3 volume, create "New Dir3" in root directory and unmount pfs3 volume
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        await pfs3Volume.CreateDirectory("New Dir3");
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // act - mount pfs3 volume, list entries in root directory and unmount pfs3 volume
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        var entries = (await pfs3Volume.ListEntries()).ToList();
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // assert - root directory contains directories created
        Assert.Equal(3, entries.Count);
        Assert.Equal(1, entries.Count(x => x.Name == "New Dir1" && x.Type == EntryType.Dir));
        Assert.Equal(1, entries.Count(x => x.Name == "New Dir2" && x.Type == EntryType.Dir));
        Assert.Equal(1, entries.Count(x => x.Name == "New Dir3" && x.Type == EntryType.Dir));
    }

    [Fact]
    public async Task WhenCreateDirectoryInSubDirectoryThenDirectoryExist()
    {
        // arrange - create pfs3 formatted disk
        await CreatePfs3FormattedDisk();

        // arrange - get first partition
        var partitionBlock = RigidDiskBlock.PartitionBlocks.First();

        // act - mount pfs3 volume, create "New Dir1" in root directory and unmount pfs3 volume
        var pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        await pfs3Volume.CreateDirectory("New Dir");
        await Pfs3Helper.Unmount(pfs3Volume.g);
        
        // act - mount pfs3 volume, change directory to "New Dir", create "Sub Dir" directory and unmount pfs3 volume
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        await pfs3Volume.ChangeDirectory("New Dir");
        await pfs3Volume.CreateDirectory("Sub Dir");
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // act - mount pfs3 volume, list entries in root directory and unmount pfs3 volume
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
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

    [Fact]
    public async Task WhenCreateNewFileInRootThenFileExist()
    {
        // arrange - create pfs3 formatted disk
        await CreatePfs3FormattedDisk();

        // arrange - get first partition
        var partitionBlock = RigidDiskBlock.PartitionBlocks.First();

        // act - mount pfs3 volume, create file in root directory and unmount pfs3 volume
        var pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        await pfs3Volume.CreateFile("New File");
        await Pfs3Helper.Unmount(pfs3Volume.g);
        
        // act - mount pfs3 volume, list entries in root directory and unmount pfs3 volume
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        var entries = (await pfs3Volume.ListEntries()).ToList();
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // assert - root directory contains file created
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "New File" && x.Type == EntryType.File));
    }

    [Fact]
    public async Task WhenCreateWriteDataToNewFileThenWhenReadDataFromFileDataMatches()
    {
        // arrange - data to write
        var data = AmigaTextHelper.GetBytes("New file with some text.");
        
        // arrange - create pfs3 formatted disk
        await CreatePfs3FormattedDisk();

        // arrange - get first partition
        var partitionBlock = RigidDiskBlock.PartitionBlocks.First();

        // act - mount pfs3 volume, create file in root directory and unmount pfs3 volume
        var pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        await pfs3Volume.CreateFile("New File");
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // act - mount pfs3 volume, write data and unmount pfs3 volume
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        await using (var entryStream = await pfs3Volume.OpenFile("New File", true))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // act - mount pfs3 volume, read data and unmount pfs3 volume
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        int bytesRead;
        byte[] dataRead;
        await using (var entryStream = await pfs3Volume.OpenFile("New File", false))
        {
            dataRead = new byte[entryStream.Length];
            bytesRead = await entryStream.ReadAsync(dataRead, 0, dataRead.Length);
        }
        await Pfs3Helper.Unmount(pfs3Volume.g);
        
        // assert - data read matches data written
        Assert.Equal(data.Length, bytesRead);
        Assert.Equal(data.Length, dataRead.Length);
        Assert.Equal(data, dataRead);
    }
    
    [Fact]
    public async Task WhenCreateWriteDataToNewFileThenWhenSeekAndReadDataMatches()
    {
        // arrange - data to write
        var data = AmigaTextHelper.GetBytes("New file with some text.");
        
        // arrange - create pfs3 formatted disk
        await CreatePfs3FormattedDisk();

        // arrange - get first partition
        var partitionBlock = RigidDiskBlock.PartitionBlocks.First();

        // act - mount pfs3 volume, create file in root directory and unmount pfs3 volume
        var pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        await pfs3Volume.CreateFile("New File");
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // act - mount pfs3 volume, write data and unmount pfs3 volume
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        await using (var entryStream = await pfs3Volume.OpenFile("New File", true))
        {
            await entryStream.WriteAsync(data, 0, data.Length);
        }
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // act - mount pfs3 volume, read data and unmount pfs3 volume
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
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
        await Pfs3Helper.Unmount(pfs3Volume.g);

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
    
    [Fact]
    public async Task WhenCreateAndDeleteFileInRootThenFileDoesntExist()
    {
        // arrange - create pfs3 formatted disk
        await CreatePfs3FormattedDisk();

        // arrange - get first partition
        var partitionBlock = RigidDiskBlock.PartitionBlocks.First();

        // act - mount pfs3 volume, create file in root directory and unmount pfs3 volume
        var pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        await pfs3Volume.CreateFile("New File");
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // act - mount pfs3 volume, delete file from root directory and unmount pfs3 volume
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        await pfs3Volume.Delete("New File");
        await Pfs3Helper.Unmount(pfs3Volume.g);
        
        // act - mount pfs3 volume, list entries in root directory and unmount pfs3 volume
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        var entries = (await pfs3Volume.ListEntries()).ToList();
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // assert - root directory is empty
        Assert.Empty(entries);
    }
    
    [Fact]
    public async Task WhenCreateTwoFilesAndDeleteOneFileInRootThenOneFileExists()
    {
        // arrange - create pfs3 formatted disk
        await CreatePfs3FormattedDisk();

        // arrange - get first partition
        var partitionBlock = RigidDiskBlock.PartitionBlocks.First();

        // act - mount pfs3 volume, create file in root directory and unmount pfs3 volume
        var pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        await pfs3Volume.CreateFile("New File 1");
        await pfs3Volume.CreateFile("New File 2");
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // act - mount pfs3 volume, delete file from root directory and unmount pfs3 volume
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        await pfs3Volume.Delete("New File 1");
        await Pfs3Helper.Unmount(pfs3Volume.g);
        
        // act - mount pfs3 volume, list entries in root directory and unmount pfs3 volume
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        var entries = (await pfs3Volume.ListEntries()).ToList();
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // assert - root directory contains new file 1
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Name == "New File 2" && x.Type == EntryType.File));
    }
    
    [Fact]
    public async Task WhenCreateAndDeleteDirectoryInRootDirectoryThenDirectoryDoesntExist()
    {
        // arrange - create pfs3 formatted disk
        await CreatePfs3FormattedDisk();

        // arrange - get first partition
        var partitionBlock = RigidDiskBlock.PartitionBlocks.First();

        // act - mount pfs3 volume, create "New Dir" in root directory and unmount pfs3 volume
        var pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        await pfs3Volume.CreateDirectory("New Dir");
        await Pfs3Helper.Unmount(pfs3Volume.g);
        
        // act - mount pfs3 volume, delete directory from root directory and unmount pfs3 volume
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        await pfs3Volume.Delete("New Dir");
        await Pfs3Helper.Unmount(pfs3Volume.g);
        
        // act - mount pfs3 volume, list entries in root directory and unmount pfs3 volume
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        var entries = (await pfs3Volume.ListEntries()).ToList();
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // assert - root directory is empty
        Assert.Empty(entries);
    }
}