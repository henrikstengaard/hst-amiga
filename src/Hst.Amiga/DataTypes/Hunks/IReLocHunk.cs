namespace Hst.Amiga.DataTypes.Hunks
{
    using System.Collections.Generic;

    /// <summary>
    /// Hunk with relocations of long words by start address of hunks.
    /// </summary>
    public interface IReLocHunk : IHunk
    {
        /// <summary>
        /// Offsets grouped by the hunk number they point into.
        /// </summary>
        IEnumerable<ReLoc32Entry> Entries { get; }
    }
}
