namespace Hst.Amiga.Vm.Loader;

using System.Collections.Generic;

/// <summary>
/// Segments of loaded hunk executable.
/// </summary>
public class SegmentList
{
    public SegmentList(IList<uint> hunkAddresses, uint bptr)
    {
        HunkAddresses = hunkAddresses;
        Bptr = bptr;
    }

    /// <summary>
    /// Addresses of hunk data for each hunk.
    /// </summary>
    public IList<uint> HunkAddresses { get; }

    /// <summary>
    /// BCPL pointer to first segment.
    /// </summary>
    public uint Bptr { get; }

    /// <summary>
    /// Address of first instruction in first hunk.
    /// </summary>
    public uint EntryPoint => HunkAddresses.Count > 0 ? HunkAddresses[0] : 0;
}
