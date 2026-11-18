namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Least recently used cache of block bytes by logical sector. Used as write-through cache for metadata blocks
    /// (root, entry, file ext, bitmap, dir cache blocks), so blocks always are written to disk and cache only
    /// saves reads. Block bytes are copied on add and get, so callers can't change cached block bytes.
    /// </summary>
    internal class BlockCache
    {
        private readonly int capacity;
        private readonly Dictionary<uint, LinkedListNode<KeyValuePair<uint, byte[]>>> blocks;
        private readonly LinkedList<KeyValuePair<uint, byte[]>> lru;

        public BlockCache(int capacity)
        {
            this.capacity = capacity;
            blocks = new Dictionary<uint, LinkedListNode<KeyValuePair<uint, byte[]>>>();
            lru = new LinkedList<KeyValuePair<uint, byte[]>>();
        }

        public int Count => blocks.Count;

        /// <summary>
        /// Try get copy of cached block bytes for logical sector
        /// </summary>
        public bool TryGet(uint sector, out byte[] blockBytes)
        {
            if (!blocks.TryGetValue(sector, out var node))
            {
                blockBytes = null;
                return false;
            }

            lru.Remove(node);
            lru.AddFirst(node);

            blockBytes = new byte[node.Value.Value.Length];
            Array.Copy(node.Value.Value, blockBytes, blockBytes.Length);
            return true;
        }

        /// <summary>
        /// Add or update cached block bytes for logical sector
        /// </summary>
        public void Set(uint sector, byte[] blockBytes)
        {
            if (capacity <= 0)
            {
                return;
            }

            var copy = new byte[blockBytes.Length];
            Array.Copy(blockBytes, copy, copy.Length);

            if (blocks.TryGetValue(sector, out var node))
            {
                lru.Remove(node);
            }
            else if (blocks.Count >= capacity)
            {
                var last = lru.Last;
                lru.RemoveLast();
                blocks.Remove(last.Value.Key);
            }

            node = new LinkedListNode<KeyValuePair<uint, byte[]>>(new KeyValuePair<uint, byte[]>(sector, copy));
            lru.AddFirst(node);
            blocks[sector] = node;
        }

        /// <summary>
        /// Remove cached block bytes for logical sectors
        /// </summary>
        public void Invalidate(uint sector, int count = 1)
        {
            if (blocks.Count == 0)
            {
                return;
            }

            for (var i = 0; i < count; i++)
            {
                if (blocks.TryGetValue(sector + (uint)i, out var node))
                {
                    lru.Remove(node);
                    blocks.Remove(sector + (uint)i);
                }
            }
        }

        public void Clear()
        {
            blocks.Clear();
            lru.Clear();
        }
    }
}
