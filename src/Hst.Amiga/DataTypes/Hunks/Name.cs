namespace Hst.Amiga.DataTypes.Hunks
{
    /// <summary>
    /// Name hunk with name of the following hunk.
    /// </summary>
    public class Name : IHunk
    {
        public uint Identifier => HunkIdentifiers.Name;

        public string Value { get; set; }
    }
}
