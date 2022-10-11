namespace Hst.Amiga.Tests.Pfs3Tests;

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
}