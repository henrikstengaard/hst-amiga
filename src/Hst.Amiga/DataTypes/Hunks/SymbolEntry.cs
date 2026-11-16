namespace Hst.Amiga.DataTypes.Hunks
{
    public class SymbolEntry
    {
        public string Name { get; set; }

        /// <summary>
        /// Value of symbol, offset in hunk.
        /// </summary>
        public uint Value { get; set; }
    }
}
