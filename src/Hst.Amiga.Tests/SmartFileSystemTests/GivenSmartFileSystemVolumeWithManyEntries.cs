namespace Hst.Amiga.Tests.SmartFileSystemTests;

using System.Linq;
using System.Threading.Tasks;
using FileSystems.SmartFileSystem;
using Xunit;
using Disk = FileSystems.SmartFileSystem.Disk;

public class GivenSmartFileSystemVolumeWithManyEntries : SmartFileSystemTestBase
{
    [Fact]
    public async Task WhenCreatingAndDeletingManyFilesThenObjectNodeTreeGrowsAndShrinks()
    {
        // arrange
        const int files = 3000;
        var stream = await CreateSmartFileSystemFormattedDisk();

        await using (var volume = await MountVolume(stream))
        {
            var freeBefore = volume.Free;
            await volume.CreateDirectory("Many");
            await volume.ChangeDirectory("Many");

            // act - create many files, which requires more object containers and more than
            // one level in object node tree (49 nodes per leaf node container for 512 bytes block size)
            for (var i = 0; i < files; i++)
            {
                await volume.CreateFile($"file_with_a_long_name_{i:D5}.txt");
            }

            // assert - object node tree has more than one level
            var nodeRoot = await Disk.ReadNodeContainer(volume.Volume, volume.Volume.RootBlock.ObjectNodeRoot);
            Assert.False(nodeRoot.IsLeaf);
            Assert.Equal(files, (await volume.ListEntries()).Count());
            Assert.NotNull((await volume.FindEntry("file_with_a_long_name_01234.txt")).Entry);

            // act - delete all files
            for (var i = 0; i < files; i++)
            {
                await volume.Delete($"file_with_a_long_name_{i:D5}.txt");
            }

            await volume.ChangeDirectory("/");
            await volume.Delete("Many");

            // assert - admin spaces allocated stay allocated in smart file system
            Assert.Empty(await volume.ListEntries());
            Assert.True(volume.Free <= freeBefore);
        }

        await AssertVolumeIsValid(stream);
    }

    [Fact]
    public async Task WhenWritingManyFragmentedFilesThenExtentBTreeIsSplitAndMerged()
    {
        // arrange
        const int files = 200;
        var stream = await CreateSmartFileSystemFormattedDisk();

        await using (var volume = await MountVolume(stream))
        {
            // act - write files interleaved so each file gets many extents
            var streams = Enumerable.Range(0, files)
                .Select(i => volume.OpenFile($"file{i}", FileSystems.FileMode.Write).GetAwaiter().GetResult())
                .ToList();
            for (var round = 0; round < 10; round++)
            {
                for (var i = 0; i < files; i++)
                {
                    var data = CreateTestData(512, i + round);
                    await streams[i].WriteAsync(data, 0, data.Length);
                }
            }

            foreach (var entryStream in streams)
            {
                await entryStream.DisposeAsync();
            }

            // assert - extent b-tree root is internal node after splits
            var extentRoot = await Disk.ReadBNodeContainer(volume.Volume, volume.Volume.RootBlock.ExtentBNodeRoot);
            Assert.False(extentRoot.IsLeaf);
        }

        await AssertVolumeIsValid(stream);

        await using (var volume = await MountVolume(stream))
        {
            // assert - data is equal
            for (var i = 0; i < files; i += 37)
            {
                var expected = Enumerable.Range(0, 10).SelectMany(round => CreateTestData(512, i + round)).ToArray();
                Assert.Equal(expected, await ReadFile(volume, $"file{i}"));
            }

            // act - delete every second file
            for (var i = 0; i < files; i += 2)
            {
                await volume.Delete($"file{i}");
            }
        }

        await AssertVolumeIsValid(stream);

        await using (var volume = await MountVolume(stream))
        {
            // act - delete remaining files
            for (var i = 1; i < files; i += 2)
            {
                await volume.Delete($"file{i}");
            }

            // assert - extent b-tree is empty
            var extentRoot = await Disk.ReadBNodeContainer(volume.Volume, volume.Volume.RootBlock.ExtentBNodeRoot);
            Assert.True(extentRoot.IsLeaf);
            Assert.Empty(extentRoot.Nodes);
        }

        await AssertVolumeIsValid(stream);
    }

    [Fact]
    public async Task WhenCreatingManyDirectoriesThenAdminSpacesAreAdded()
    {
        // arrange
        var stream = await CreateSmartFileSystemFormattedDisk();

        await using (var volume = await MountVolume(stream))
        {
            // act - each directory uses an object container and a hash table, which requires many admin spaces
            for (var i = 0; i < 100; i++)
            {
                await volume.CreateDirectory($"dir{i}");
                await volume.ChangeDirectory($"dir{i}");
                await volume.CreateFile("file");
                await volume.ChangeDirectory("/");
            }

            // assert
            Assert.Equal(100, (await volume.ListEntries()).Count());
            var adminSpaceContainer = await Disk.ReadAdminSpaceContainer(volume.Volume,
                volume.Volume.RootBlock.AdminSpaceContainer);
            Assert.True(adminSpaceContainer.AdminSpaces.Count(x => x.Space != 0) > 5);
        }

        await AssertVolumeIsValid(stream);
    }
}
