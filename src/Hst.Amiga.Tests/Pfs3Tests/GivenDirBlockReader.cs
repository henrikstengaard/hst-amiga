namespace Hst.Amiga.Tests.Pfs3Tests;

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using Xunit;

public class GivenDirBlockReader
{
    [Fact]
    public async Task WhenReadDirEntriesThenEntriesMatch()
    {
        var blockBytes = await System.IO.File.ReadAllBytesAsync(Path.Combine("TestData", "Pfs3", "dirblock_1.bin"));

        var g = new globaldata
        {
            RootBlock = new RootBlock
            {
                ReservedBlksize = 1024
            }
        };

        var dirBlock = await DirBlockReader.Parse(blockBytes, g);

        var dirEntries = new List<direntry>();

        var offset = 0;
        direntry dirEntry;
        do
        {
            dirEntry = DirEntryReader.Read(dirBlock.entries, offset);

            if (dirEntry.next > 0)
            {
                dirEntries.Add(dirEntry);
            }

            offset += dirEntry.next;
        } while (dirEntry.next > 0);

        Assert.Equal(2, dirEntries.Count);
    }
}