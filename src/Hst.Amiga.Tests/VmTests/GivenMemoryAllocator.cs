namespace Hst.Amiga.Tests.VmTests;

using Vm;
using Xunit;

public class GivenMemoryAllocator
{
    [Fact]
    public void When_AllocatingMemory_Then_MemoryIsAlignedAndCleared()
    {
        var ram = new Ram(0x10000);
        ram.Fill(0x1000, 0x100, 0xFF);
        var allocator = new MemoryAllocator(ram, 0x1000, 0x10000);

        var address = allocator.Allocate(10);

        Assert.Equal(0x1000u, address);
        Assert.Equal(16u, allocator.SizeOf(address));
        Assert.All(ram.ReadBytes(address, 16), b => Assert.Equal(0, b));
    }

    [Fact]
    public void When_FreeingAdjacentBlocks_Then_FreeBlocksAreMerged()
    {
        var ram = new Ram(0x10000);
        var allocator = new MemoryAllocator(ram, 0x1000, 0x10000);
        var total = allocator.Available;

        var first = allocator.Allocate(0x100);
        var second = allocator.Allocate(0x100);
        var third = allocator.Allocate(0x100);

        Assert.True(allocator.Free(second));
        Assert.True(allocator.Free(first));
        Assert.True(allocator.Free(third));

        Assert.Equal(total, allocator.Available);
        Assert.Equal(total, allocator.Largest);
        Assert.Equal(0, allocator.AllocationCount);
    }

    [Fact]
    public void When_FreeingUnallocatedAddress_Then_FalseIsReturned()
    {
        var ram = new Ram(0x10000);
        var allocator = new MemoryAllocator(ram, 0x1000, 0x10000);

        Assert.False(allocator.Free(0x2000));
    }

    [Fact]
    public void When_AllocatingMoreThanAvailable_Then_ZeroIsReturned()
    {
        var ram = new Ram(0x10000);
        var allocator = new MemoryAllocator(ram, 0x1000, 0x10000);

        Assert.Equal(0u, allocator.Allocate(0x20000));
    }

    [Fact]
    public void When_AllocatingAbsoluteAddress_Then_MemoryAtAddressIsAllocated()
    {
        var ram = new Ram(0x10000);
        var allocator = new MemoryAllocator(ram, 0x1000, 0x10000);

        Assert.Equal(0x4000u, allocator.AllocateAbsolute(0x4000, 0x100));
        Assert.Equal(0u, allocator.AllocateAbsolute(0x4000, 0x10));
        Assert.True(allocator.Free(0x4000));
        Assert.Equal(0x4000u, allocator.AllocateAbsolute(0x4000, 0x100));
    }
}
