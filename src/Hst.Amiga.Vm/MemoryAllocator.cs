namespace Hst.Amiga.Vm;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// First fit memory allocator of a memory region with 8 byte alignment. Allocated memory is cleared with zeroes.
/// </summary>
public class MemoryAllocator
{
    public const uint Alignment = 8;

    private readonly Ram ram;
    private readonly List<Block> freeBlocks;
    private readonly Dictionary<uint, uint> allocations;

    private struct Block
    {
        public uint Address;
        public uint Size;
    }

    public MemoryAllocator(Ram ram, uint start, uint end)
    {
        this.ram = ram;
        start = Align(start);
        end &= ~(Alignment - 1);
        if (end <= start)
        {
            throw new ArgumentException("Memory region is empty");
        }

        Start = start;
        End = end;
        freeBlocks = new List<Block> { new() { Address = start, Size = end - start } };
        allocations = new Dictionary<uint, uint>();
    }

    public uint Start { get; }
    public uint End { get; }

    /// <summary>
    /// Total number of free bytes.
    /// </summary>
    public uint Available => (uint)freeBlocks.Sum(x => (long)x.Size);

    /// <summary>
    /// Largest free block in bytes.
    /// </summary>
    public uint Largest => freeBlocks.Count == 0 ? 0 : freeBlocks.Max(x => x.Size);

    /// <summary>
    /// Total size of memory region.
    /// </summary>
    public uint Total => End - Start;

    public int AllocationCount => allocations.Count;

    private static uint Align(uint value) => (value + Alignment - 1) & ~(Alignment - 1);

    /// <summary>
    /// Allocate memory cleared with zeroes.
    /// </summary>
    /// <returns>Address of allocated memory or 0, if not enough memory.</returns>
    public uint Allocate(uint size)
    {
        if (size == 0 || size > End - Start)
        {
            return 0;
        }

        size = Align(size);
        for (var i = 0; i < freeBlocks.Count; i++)
        {
            var block = freeBlocks[i];
            if (block.Size < size)
            {
                continue;
            }

            if (block.Size == size)
            {
                freeBlocks.RemoveAt(i);
            }
            else
            {
                freeBlocks[i] = new Block { Address = block.Address + size, Size = block.Size - size };
            }

            allocations[block.Address] = size;
            ram.Fill(block.Address, size);
            return block.Address;
        }

        return 0;
    }

    /// <summary>
    /// Allocate memory at absolute address.
    /// </summary>
    /// <returns>Address of allocated memory aligned down or 0, if memory is not free.</returns>
    public uint AllocateAbsolute(uint address, uint size)
    {
        var start = address & ~(Alignment - 1);
        var end = Align(address + size);
        for (var i = 0; i < freeBlocks.Count; i++)
        {
            var block = freeBlocks[i];
            if (start < block.Address || end > block.Address + block.Size)
            {
                continue;
            }

            freeBlocks.RemoveAt(i);
            if (end < block.Address + block.Size)
            {
                freeBlocks.Insert(i, new Block { Address = end, Size = block.Address + block.Size - end });
            }

            if (start > block.Address)
            {
                freeBlocks.Insert(i, new Block { Address = block.Address, Size = start - block.Address });
            }

            allocations[start] = end - start;
            ram.Fill(start, end - start);
            return start;
        }

        return 0;
    }

    /// <summary>
    /// Size of allocation at address.
    /// </summary>
    /// <returns>Size in bytes or 0, if address is not allocated.</returns>
    public uint SizeOf(uint address) => allocations.TryGetValue(address, out var size) ? size : 0;

    public bool IsAllocated(uint address) => allocations.ContainsKey(address);

    /// <summary>
    /// Free allocated memory.
    /// </summary>
    /// <returns>True, if memory was freed. False, if address is not allocated.</returns>
    public bool Free(uint address)
    {
        if (!allocations.TryGetValue(address, out var size))
        {
            return false;
        }

        allocations.Remove(address);

        // insert sorted by address and merge with adjacent free blocks
        var index = 0;
        while (index < freeBlocks.Count && freeBlocks[index].Address < address)
        {
            index++;
        }

        freeBlocks.Insert(index, new Block { Address = address, Size = size });

        if (index + 1 < freeBlocks.Count && address + size == freeBlocks[index + 1].Address)
        {
            freeBlocks[index] = new Block { Address = address, Size = size + freeBlocks[index + 1].Size };
            freeBlocks.RemoveAt(index + 1);
        }

        if (index > 0 && freeBlocks[index - 1].Address + freeBlocks[index - 1].Size == address)
        {
            freeBlocks[index - 1] = new Block
            {
                Address = freeBlocks[index - 1].Address,
                Size = freeBlocks[index - 1].Size + freeBlocks[index].Size
            };
            freeBlocks.RemoveAt(index);
        }

        return true;
    }
}
