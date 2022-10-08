namespace Hst.Amiga.Tests.Pfs3Tests;

using System.Linq;
using System.Threading.Tasks;
using FileSystems.Pfs3;
using Xunit;
using Directory = FileSystems.Pfs3.Directory;

public class GivenFormattedPfs3Disk : Pfs3TestBase
{
    [Fact]
    public async Task WhenCreateDirectoriesInRootThenDirectoriesExist()
    {
        // arrange - create pfs3 formatted disk
        await CreatePfs3FormattedDisk();

        // arrange - get first partition
        var partitionBlock = RigidDiskBlock.PartitionBlocks.First();

        // act - mount pfs3 volume from first partition
        var pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        
        // act - get root directory
        var root = await Directory.GetRoot(pfs3Volume.g);

        // act - create directory "created" in root directory
        await Directory.NewDir(root, "created", pfs3Volume.g);
        
        // act - unmount pfs3 volume
        await Pfs3Helper.Unmount(pfs3Volume.g);
        
        // act - mount pfs3 volume from first partition
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);

        // act - get root directory
        root = await Directory.GetRoot(pfs3Volume.g);
        // act - create directory "with" in root directory
        await Directory.NewDir(root, "with", pfs3Volume.g);

        // act - unmount pfs3 volume
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // act - mount pfs3 volume from first partition
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);

        // act - get root directory
        root = await Directory.GetRoot(pfs3Volume.g);

        // act - create directory "hst.amiga library" in root directory
        await Directory.NewDir(root, "hst.amiga library", pfs3Volume.g);
        
        // act - unmount pfs3 volume
        await Pfs3Helper.Unmount(pfs3Volume.g);

        // act - mount pfs3 volume from first partition
        pfs3Volume = await Pfs3Volume.Mount(Stream, partitionBlock);
        
        // act - get entries from root directory
        var entries = (await pfs3Volume.GetEntries()).ToList();

        // assert - root directory contains 3 created directories
        Assert.Equal(3, entries.Count);
        Assert.Equal(1, entries.Count(x => x.Name == "created"));
        Assert.Equal(1, entries.Count(x => x.Name == "with"));
        Assert.Equal(1, entries.Count(x => x.Name == "hst.amiga library"));
    }
}