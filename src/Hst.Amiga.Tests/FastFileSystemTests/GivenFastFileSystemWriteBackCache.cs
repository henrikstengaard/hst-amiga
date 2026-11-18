namespace Hst.Amiga.Tests.FastFileSystemTests;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileSystems.FastFileSystem;
using Xunit;
using FileMode = FileSystems.FileMode;

public class GivenFastFileSystemWriteBackCache : FastFileSystemTestBase
{
    private static byte[] CreateData(int size, int seed = 0) =>
        Enumerable.Range(0, size).Select(x => (byte)((x + seed) * 7 % 251)).ToArray();

    [Fact]
    public async Task WhenCreateEntriesThenBlocksAreWrittenWhenVolumeIsFlushed()
    {
        // arrange - create formatted disk
        var stream = new CountingStream(await CreateFastFileSystemFormattedDisk());
        var volume = await MountVolume(stream);
        stream.Reset();

        // act - create directory and empty files
        await volume.CreateDirectory("Dir");
        for (var i = 0; i < 100; i++)
        {
            await volume.CreateFile($"File{i}");
        }

        // assert - only root block bitmap flags invalid is written before flush
        Assert.Equal(1, stream.Writes);
        await using (var otherVolume = await MountVolume(stream, true))
        {
            Assert.Empty(await otherVolume.ListEntries());
        }

        // act - flush volume
        stream.Reset();
        await volume.Flush();

        // assert - dirty blocks are written with less writes than blocks changed, as consecutive blocks are merged
        Assert.True(stream.Writes < 101, $"{stream.Writes} writes");
        await using (var otherVolume = await MountVolume(stream, true))
        {
            Assert.Equal(101, (await otherVolume.ListEntries()).Count());
        }

        await volume.DisposeAsync();
    }

    [Fact]
    public async Task WhenChangesExceedMaxDirtyBlocksThenVolumeIsFlushedWhenOperationCompletes()
    {
        // arrange - create formatted disk
        var stream = await CreateFastFileSystemFormattedDisk();
        const int files = Volume.MaxDirtyBlocks + 100;

        // act - create more files than max dirty blocks without flushing volume
        var volume = await MountVolume(stream);
        for (var i = 0; i < files; i++)
        {
            await volume.CreateFile($"File{i}");
        }

        // assert - volume was flushed when max dirty blocks was exceeded
        await using (var otherVolume = await MountVolume(stream, true))
        {
            var entries = (await otherVolume.ListEntries()).Count();
            Assert.True(entries >= Volume.MaxDirtyBlocks / 2 && entries < files, $"{entries} entries");
        }

        // assert - all files exist after volume is disposed
        await volume.DisposeAsync();
        await using (var otherVolume = await MountVolume(stream))
        {
            Assert.Equal(files, (await otherVolume.ListEntries()).Count());
        }
    }

    [Fact]
    public async Task WhenDirtyBlockIsOverwrittenWithDataBlocksThenDataIsNotOverwrittenOnFlush()
    {
        // arrange - mount formatted disk as internal volume
        var stream = await CreateFastFileSystemFormattedDisk();
        var rigidDiskBlock = await RigidDiskBlocks.RigidDiskBlockReader.Read(stream);
        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();
        var volume = await FastFileSystemHelper.Mount(stream, partitionBlock.LowCyl, partitionBlock.HighCyl,
            partitionBlock.Surfaces, partitionBlock.BlocksPerTrack, partitionBlock.Reserved,
            partitionBlock.BlockSize, partitionBlock.FileSystemBlockSize);
        const uint sector = 5000;

        // arrange - write metadata block, which is dirty in block cache
        await volume.BlockStore.WriteBlock(sector, CreateData(512, 1));
        Assert.Equal(1, volume.BlockCache.DirtyCount);

        // act - write data blocks over dirty block and flush volume
        var data = CreateData(512 * 4, 2);
        await volume.BlockStore.WriteBlocks(sector - 1, 4, data, 0);
        await volume.BlockStore.FlushVolume();

        // assert - data blocks are not overwritten by dirty block
        var blocks = new byte[512 * 4];
        stream.Seek(volume.PartitionStartOffset + (sector - 1) * 512L, SeekOrigin.Begin);
        Assert.Equal(blocks.Length, await stream.ReadAsync(blocks, 0, blocks.Length));
        Assert.Equal(data, blocks);
    }

    [Fact]
    public async Task WhenReadBlocksWithDirtyBlockThenDirtyBlockIsRead()
    {
        // arrange - mount formatted disk as internal volume
        var stream = await CreateFastFileSystemFormattedDisk();
        var rigidDiskBlock = await RigidDiskBlocks.RigidDiskBlockReader.Read(stream);
        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();
        var volume = await FastFileSystemHelper.Mount(stream, partitionBlock.LowCyl, partitionBlock.HighCyl,
            partitionBlock.Surfaces, partitionBlock.BlocksPerTrack, partitionBlock.Reserved,
            partitionBlock.BlockSize, partitionBlock.FileSystemBlockSize);
        const uint sector = 5000;
        var dirtyBlock = CreateData(512, 3);
        await volume.BlockStore.WriteBlock(sector, dirtyBlock);

        // act - read blocks including dirty block, which is not written to disk yet
        var blocks = new byte[512 * 3];
        await volume.BlockStore.ReadBlocks(sector - 1, 3, blocks, 0);

        // assert - dirty block is read and blocks before and after are read from disk
        Assert.Equal(dirtyBlock, blocks.Skip(512).Take(512).ToArray());
        Assert.All(blocks.Take(512).Concat(blocks.Skip(1024)), x => Assert.Equal(0, x));
        Assert.Equal(dirtyBlock, await volume.BlockStore.ReadBlock(sector, false));
    }
}
