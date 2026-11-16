namespace Hst.Amiga.Vm.Loader;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DataTypes.Hunks;

/// <summary>
/// Loads hunk executables into memory as segment list like dos.library LoadSeg(). Each segment is allocated
/// with segment size and BCPL pointer to next segment before hunk data.
/// </summary>
public class HunkLoader
{
    private const uint HunkSizeMask = 0x3FFFFFFF;

    private readonly Ram ram;
    private readonly MemoryAllocator allocator;

    public HunkLoader(Ram ram, MemoryAllocator allocator)
    {
        this.ram = ram;
        this.allocator = allocator;
    }

    /// <summary>
    /// Load hunk executable from stream.
    /// </summary>
    /// <returns>Loaded segments.</returns>
    /// <exception cref="IOException">Stream is not a valid hunk executable.</exception>
    /// <exception cref="VmException">Not enough memory to load segments.</exception>
    public async Task<SegmentList> Load(Stream stream)
    {
        var hunks = (await HunkReader.Read(stream)).ToList();
        if (hunks.Count == 0 || hunks[0] is not Header header)
        {
            throw new IOException("Invalid hunk file, header is missing");
        }

        var hunkSizes = header.HunkSizes.Select(x => (x & HunkSizeMask) * 4).ToList();
        var segments = new List<uint>();

        try
        {
            // allocate segments with size and next segment bcpl pointer before hunk data
            foreach (var hunkSize in hunkSizes)
            {
                var memory = allocator.Allocate(hunkSize + 8);
                if (memory == 0)
                {
                    throw new VmException($"Not enough memory to load hunk of {hunkSize} bytes");
                }

                ram.WriteLong(memory, hunkSize + 8);
                segments.Add(memory + 4);
            }

            for (var i = 0; i < segments.Count; i++)
            {
                ram.WriteLong(segments[i], i + 1 < segments.Count ? segments[i + 1] >> 2 : 0);
            }

            var hunkIndex = -1;
            foreach (var hunk in hunks.Skip(1))
            {
                switch (hunk)
                {
                    case Code code:
                        hunkIndex++;
                        CopyHunkData(segments, hunkSizes, hunkIndex, code.Data);
                        break;
                    case Data data:
                        hunkIndex++;
                        CopyHunkData(segments, hunkSizes, hunkIndex, data.Bytes);
                        break;
                    case Bss _:
                        // memory is already cleared by allocator
                        hunkIndex++;
                        CheckHunkIndex(segments, hunkIndex);
                        break;
                    case IReLocHunk reLocHunk:
                        Relocate(segments, hunkSizes, hunkIndex, reLocHunk);
                        break;
                }
            }
        }
        catch
        {
            foreach (var segment in segments)
            {
                allocator.Free(segment - 4);
            }

            throw;
        }

        return new SegmentList(segments.Select(x => x + 4).ToList(), segments[0] >> 2);
    }

    private static void CheckHunkIndex(List<uint> segments, int hunkIndex)
    {
        if (hunkIndex < 0 || hunkIndex >= segments.Count)
        {
            throw new IOException($"Invalid hunk file, hunk {hunkIndex} exceeds {segments.Count} hunks in header");
        }
    }

    private void CopyHunkData(List<uint> segments, List<uint> hunkSizes, int hunkIndex, byte[] data)
    {
        CheckHunkIndex(segments, hunkIndex);
        var length = (int)Math.Min((uint)data.Length, hunkSizes[hunkIndex]);
        ram.WriteBytes(segments[hunkIndex] + 4, data, 0, length);
    }

    private void Relocate(List<uint> segments, List<uint> hunkSizes, int hunkIndex, IReLocHunk reLocHunk)
    {
        CheckHunkIndex(segments, hunkIndex);
        var hunkAddress = segments[hunkIndex] + 4;
        foreach (var entry in reLocHunk.Entries)
        {
            if (entry.HunkNumber >= segments.Count)
            {
                throw new IOException($"Invalid relocation to hunk {entry.HunkNumber}");
            }

            var targetAddress = segments[(int)entry.HunkNumber] + 4;
            foreach (var offset in entry.Offsets)
            {
                if (offset + 4 > hunkSizes[hunkIndex])
                {
                    throw new IOException($"Invalid relocation offset {offset} in hunk {hunkIndex}");
                }

                var address = hunkAddress + offset;
                ram.WriteLong(address, ram.ReadLong(address) + targetAddress);
            }
        }
    }

    /// <summary>
    /// Free segments of segment list.
    /// </summary>
    public void Unload(uint segListBptr)
    {
        var segment = segListBptr << 2;
        var count = 0;
        while (segment != 0 && count++ < 10000)
        {
            var next = ram.ReadLong(segment) << 2;
            allocator.Free(segment - 4);
            segment = next;
        }
    }
}
