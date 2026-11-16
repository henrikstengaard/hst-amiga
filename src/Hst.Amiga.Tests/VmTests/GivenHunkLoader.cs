namespace Hst.Amiga.Tests.VmTests;

using System.IO;
using System.Threading.Tasks;
using HunkTests;
using Vm;
using Vm.Loader;
using Xunit;

public class GivenHunkLoader
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_LoadingHunksWithRelocations_Then_PointersAreRelocatedToHunkAddresses(
        bool shortRelocations)
    {
        // arrange: code hunk with pointers to data and bss hunks, data hunk with pointer to code hunk
        var code = new byte[] { 0x4E, 0x75, 0, 0, 0, 0, 0, 4, 0, 0, 0, 8 };
        var data = new byte[] { 0, 0, 0, 2, 0xAA, 0xBB, 0xCC, 0xDD };
        var builder = new HunkFileBuilder()
            .AddCode(code, 0, (4, 1), (8, 2));
        if (shortRelocations)
        {
            builder.WithShortRelocations();
        }

        var bytes = builder
            .AddData(data, 0, (0, 0))
            .AddBss(64)
            .Build();

        var ram = new Ram(0x100000);
        var allocator = new MemoryAllocator(ram, 0x1000, 0x100000);
        var loader = new HunkLoader(ram, allocator);

        // act
        var segmentList = await loader.Load(new MemoryStream(bytes));

        // assert
        Assert.Equal(3, segmentList.HunkAddresses.Count);
        var codeAddress = segmentList.HunkAddresses[0];
        var dataAddress = segmentList.HunkAddresses[1];
        var bssAddress = segmentList.HunkAddresses[2];
        Assert.Equal(codeAddress, segmentList.EntryPoint);
        Assert.Equal((codeAddress - 4) >> 2, segmentList.Bptr);
        Assert.Equal(0x4E75, ram.ReadWord(codeAddress));
        Assert.Equal(dataAddress + 4, ram.ReadLong(codeAddress + 4));
        Assert.Equal(bssAddress + 8, ram.ReadLong(codeAddress + 8));
        Assert.Equal(codeAddress + 2, ram.ReadLong(dataAddress));
        Assert.Equal(0xAABBCCDDu, ram.ReadLong(dataAddress + 4));

        // segments are linked with bcpl pointers
        Assert.Equal((dataAddress - 4) >> 2, ram.ReadLong(codeAddress - 4));
        Assert.Equal((bssAddress - 4) >> 2, ram.ReadLong(dataAddress - 4));
        Assert.Equal(0u, ram.ReadLong(bssAddress - 4));
        Assert.Equal(72u, ram.ReadLong(bssAddress - 8));
    }

    [Fact]
    public async Task When_UnloadingSegments_Then_MemoryIsFreed()
    {
        var bytes = new HunkFileBuilder()
            .AddCode(new byte[] { 0x4E, 0x75, 0, 0 })
            .AddBss(128)
            .Build();
        var ram = new Ram(0x100000);
        var allocator = new MemoryAllocator(ram, 0x1000, 0x100000);
        var available = allocator.Available;
        var loader = new HunkLoader(ram, allocator);

        var segmentList = await loader.Load(new MemoryStream(bytes));
        loader.Unload(segmentList.Bptr);

        Assert.Equal(available, allocator.Available);
        Assert.Equal(0, allocator.AllocationCount);
    }

    [Fact]
    public async Task When_LoadingInvalidFile_Then_IOExceptionIsThrown()
    {
        var ram = new Ram(0x100000);
        var allocator = new MemoryAllocator(ram, 0x1000, 0x100000);
        var loader = new HunkLoader(ram, allocator);

        await Assert.ThrowsAsync<IOException>(() => loader.Load(new MemoryStream(new byte[] { 1, 2, 3, 4 })));
    }
}
