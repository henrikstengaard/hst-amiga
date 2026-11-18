namespace Hst.Amiga.Tests.FastFileSystemTests;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

public class GivenFastFileSystemHashChains : FastFileSystemTestBase
{
    [Fact]
    public async Task WhenDeleteAndRenameEntriesInHashChainsThenOtherEntriesInHashChainsAreKept()
    {
        // arrange - create more files than hash table size, so hash chains contain multiple entries
        var stream = await CreateFastFileSystemFormattedDisk();
        const int files = 300;
        var expected = new HashSet<string>();
        await using (var volume = await MountVolume(stream))
        {
            for (var i = 0; i < files; i++)
            {
                await volume.CreateFile($"File{i}");
                expected.Add($"File{i}");
            }

            // act - delete every third file and rename every other of the remaining files
            for (var i = 0; i < files; i++)
            {
                if (i % 3 == 0)
                {
                    await volume.Delete($"File{i}");
                    expected.Remove($"File{i}");
                }
                else if (i % 2 == 0)
                {
                    await volume.Rename($"File{i}", $"Renamed{i}");
                    expected.Remove($"File{i}");
                    expected.Add($"Renamed{i}");
                }
            }

            // assert - entries listed are equal to expected
            Assert.Equal(expected.OrderBy(x => x), (await volume.ListEntries()).Select(x => x.Name).OrderBy(x => x));
        }

        // assert - entries listed after remount are equal to expected and can be found
        await using (var volume = await MountVolume(stream))
        {
            Assert.Equal(expected.OrderBy(x => x), (await volume.ListEntries()).Select(x => x.Name).OrderBy(x => x));
            foreach (var name in expected)
            {
                Assert.NotNull((await volume.FindEntry(name)).Entry);
            }
        }
    }
}
