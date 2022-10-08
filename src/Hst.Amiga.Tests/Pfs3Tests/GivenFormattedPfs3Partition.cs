namespace Hst.Amiga.Tests.Pfs3Tests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Core.Extensions;
using Extensions;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using RigidDiskBlocks;
using Xunit;
using Directory = FileSystems.Pfs3.Directory;

public class GivenFormattedPfs3Partition
{
    private readonly byte[] pfs3DosType = { 0x50, 0x44, 0x53, 0x3 };
    
    [Fact]
    public async Task WhenFormatting100MbPartitionAtStartOfHardDiskFileThenPfs3BlocksAreCreated()
    {
        var size = 100.MB().ToUniversalSize();
        var pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio"); 

        // arrange - create memory block stream
        //await using var stream = new BlockMemoryStream();
        await using var stream = File.Open("pfs3-newdir.hdf", FileMode.Create, FileAccess.ReadWrite);

        // arrange - create rigid disk block with 1 partition using pfs3 file system 
        var rigidDiskBlock = await RigidDiskBlock
            .Create(size)
            .AddFileSystem(pfs3DosType, await File.ReadAllBytesAsync(pfs3AioPath))
            .AddPartition("DH0", bootable: true)
            .WriteToStream(stream);

        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();
        
        // act - format first partition using pfs3 formatter
        await Pfs3Formatter.FormatPartition(stream, partitionBlock, "Workbench");

        var pfs3Volume = await Pfs3Volume.Mount(stream, partitionBlock);
        var root = await Directory.GetRoot(pfs3Volume.g);
        await Directory.NewDir(root, "created", pfs3Volume.g);
        await Pfs3Helper.Unmount(pfs3Volume.g);

        pfs3Volume = await Pfs3Volume.Mount(stream, partitionBlock);
        root = await Directory.GetRoot(pfs3Volume.g);
        await Directory.NewDir(root, "with", pfs3Volume.g);
        await Pfs3Helper.Unmount(pfs3Volume.g);

        pfs3Volume = await Pfs3Volume.Mount(stream, partitionBlock);
        root = await Directory.GetRoot(pfs3Volume.g);
        await Directory.NewDir(root, "hst.amiga library", pfs3Volume.g);
        await Pfs3Helper.Unmount(pfs3Volume.g);

        var dirnodenr = (uint)Macro.ANODE_ROOTDIR;
        var dirEntries = await Directory.GetDirEntries(dirnodenr, pfs3Volume.g);

        var t = 1;

        // get first entry
        // canode anode = new canode();
        // await anodes.GetAnode(anode, dirnodenr, pfs3Volume.g);
        // var dirblock = await Directory.LoadDirBlock(anode.blocknr, pfs3Volume.g);
        // var blk = dirblock.dirblock;
        // var firstEntry = DirEntryReader.Read(blk.entries, 0);
        //
        //
        //
        // var lockEntry = new lockentry
        // {
        //     le = entry.ListEntry,
        //     //nextanode = root.file.direntry.anode
        // };
        //
        // await Directory.ExamineAll(lockEntry, pfs3Volume.g);

        // await Pfs3Helper.Unmount(g);

        // await using var blockStream = File.OpenWrite("blocks.bin");
        //
        // foreach (var block in stream.Blocks)
        // {
        //     await blockStream.WriteAsync(block.Value, 0, block.Value.Length);
        // }


        //var db = stream.Blocks.Where(x => BigEndianConverter.ConvertBytesToInt16(x.Value, 0) == Constants.DBLKID).ToList();
    }

    [Fact]
    public async Task GivenDirBlockReader()
    {
        var blockBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "Pfs3", "dirblock_1.bin"));

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

            if (dirEntry.type > 0)
            {
                dirEntries.Add(dirEntry);
            }
            
            offset += dirEntry.next;
        } while (dirEntry.next > 0);

        Assert.Equal(2, dirEntries.Count);
    }
}