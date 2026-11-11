namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    /// <summary>
    /// Hash table block (struct fsHashTable) for a directory containing object nodes of first object in each hash chain.
    /// </summary>
    public class HashTableBlock : Block
    {
        /// <summary>
        /// Object node of directory owning hash table.
        /// </summary>
        public uint Parent { get; set; }

        /// <summary>
        /// Object node of first object in each hash chain, 0 if hash chain is empty.
        /// </summary>
        public uint[] HashEntries { get; set; }

        public HashTableBlock(int blockSize) : base(Constants.HashTableId)
        {
            HashEntries = new uint[HashHelper.HashEntries(blockSize)];
        }
    }
}
