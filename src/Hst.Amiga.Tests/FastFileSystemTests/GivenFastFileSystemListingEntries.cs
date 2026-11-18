namespace Hst.Amiga.Tests.FastFileSystemTests;

using System.Linq;
using System.Threading.Tasks;
using FileSystems;
using FileSystems.FastFileSystem;
using RigidDiskBlocks;
using Xunit;

public class GivenFastFileSystemListingEntries : FastFileSystemTestBase
{
    private const int Links = 200;

    private async Task<BlockMemoryStream> CreateDiskWithLinks()
    {
        var stream = await CreateFastFileSystemFormattedDisk();
        await using var volume = await MountVolume(stream);
        await volume.CreateDirectory("Dir");
        await volume.ChangeDirectory("Dir");
        await volume.CreateFile("File");
        await volume.CreateDirectory("SubDir");
        await volume.ChangeDirectory("/");

        // create more links than hash table size, so links are located in hash chains
        for (var i = 0; i < Links; i++)
        {
            await volume.CreateLink($"FileLink{i}", "Dir/File");
            await volume.CreateLink($"DirLink{i}", "Dir/SubDir");
        }

        return stream;
    }

    [Fact]
    public async Task WhenListEntriesThenAllLinksInHashChainsAreResolved()
    {
        // arrange - create disk with file and dir links
        var stream = await CreateDiskWithLinks();

        // act - list entries
        await using var volume = await MountVolume(stream);
        var entries = (await volume.ListEntries()).ToList();

        // assert - all file and dir links have link path
        var fileLinks = entries.Where(x => x.Type == EntryType.FileLink).ToList();
        var dirLinks = entries.Where(x => x.Type == EntryType.DirLink).ToList();
        Assert.Equal(Links, fileLinks.Count);
        Assert.Equal(Links, dirLinks.Count);
        Assert.All(fileLinks, x => Assert.Equal("Dir/File", x.LinkPath));
        Assert.All(dirLinks, x => Assert.Equal("Dir/SubDir", x.LinkPath));
    }

    [Fact]
    public async Task WhenListEntriesWithoutResolvingLinkPathsThenLinksAreNotResolved()
    {
        // arrange - create disk with file and dir links
        var stream = await CreateDiskWithLinks();

        // act - mount volume without resolving link paths and list entries
        var partitionBlock = (await RigidDiskBlockReader.Read(stream)).PartitionBlocks.First();
        await using var volume = await FastFileSystemVolume.MountPartition(stream, partitionBlock, false);
        var entries = (await volume.ListEntries()).ToList();

        // assert - no file or dir links have link path
        Assert.Equal(Links * 2 + 1, entries.Count);
        Assert.All(entries, x => Assert.Null(x.LinkPath));
    }

    [Fact]
    public async Task WhenListEntriesThenEntryBlocksCloseToEachOtherAreReadWithSingleReads()
    {
        // arrange - create disk with files
        var stream = new CountingStream(await CreateFastFileSystemFormattedDisk());
        const int files = 500;
        await using (var volume = await MountVolume(stream))
        {
            for (var i = 0; i < files; i++)
            {
                await volume.CreateFile($"File{i}");
            }
        }

        // act - mount volume and list entries
        await using (var volume = await MountVolume(stream))
        {
            stream.Reset();
            var entries = (await volume.ListEntries()).ToList();

            // assert - entries are listed with less reads than entries
            Assert.Equal(files, entries.Count);
            Assert.True(stream.Reads < files / 4, $"{stream.Reads} reads");
        }
    }
}
