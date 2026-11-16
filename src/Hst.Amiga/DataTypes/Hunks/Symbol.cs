namespace Hst.Amiga.DataTypes.Hunks
{
    using System.Collections.Generic;

    /// <summary>
    /// Symbol hunk with symbol names and values.
    /// </summary>
    public class Symbol : IHunk
    {
        public uint Identifier => HunkIdentifiers.Symbol;

        public IEnumerable<SymbolEntry> Symbols { get; set; }
    }
}
