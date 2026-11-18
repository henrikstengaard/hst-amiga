﻿namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Least recently used write-back cache of block bytes by logical sector. Used for metadata blocks (root,
    /// entry, file ext, bitmap, dir cache and comment blocks). Changed blocks are marked dirty and written to disk,
    /// when volume is flushed. Block bytes are copied on set and get, so callers can't change cached block bytes.
    /// </summary>
    internal class BlockCache
    {
        internal class CachedBlock
        {
            public uint Sector;
            public byte[] Bytes;
            public bool Dirty;
            public LinkedListNode<CachedBlock> Node;
        }

        private readonly int capacity;
        private readonly Dictionary<uint, CachedBlock> blocks;
        private readonly LinkedList<CachedBlock> lru;

        public BlockCache(int capacity)
        {
            this.capacity = capacity;
            blocks = new Dictionary<uint, CachedBlock>();
            lru = new LinkedList<CachedBlock>();
        }

        /// <summary>
        /// Number of cached blocks
        /// </summary>
        public int Count => blocks.Count;

        /// <summary>
        /// Number of dirty cached blocks, which are not written to disk yet
        /// </summary>
        public int DirtyCount { get; private set; }

        /// <summary>
        /// Try get copy of cached block bytes for logical sector
        /// </summary>
        public bool TryGet(uint sector, out byte[] blockBytes)
        {
            if (!blocks.TryGetValue(sector, out var block))
            {
                blockBytes = null;
                return false;
            }

            lru.Remove(block.Node);
            lru.AddFirst(block.Node);

            blockBytes = new byte[block.Bytes.Length];
            Array.Copy(block.Bytes, blockBytes, blockBytes.Length);
            return true;
        }

        /// <summary>
        /// Try get dirty cached block bytes for logical sector without copying and updating least recently used
        /// </summary>
        public bool TryGetDirty(uint sector, out byte[] blockBytes)
        {
            if (DirtyCount > 0 && blocks.TryGetValue(sector, out var block) && block.Dirty)
            {
                blockBytes = block.Bytes;
                return true;
            }

            blockBytes = null;
            return false;
        }

        /// <summary>
        /// Add or update cached block bytes for logical sector. Least recently used clean blocks are evicted,
        /// when capacity is exceeded. Dirty blocks are not evicted and must be written by flushing.
        /// </summary>
        /// <param name="sector">Logical sector</param>
        /// <param name="blockBytes">Block bytes</param>
        /// <param name="dirty">Block is changed and must be written to disk</param>
        public void Set(uint sector, byte[] blockBytes, bool dirty)
        {
            if (capacity <= 0 && !dirty)
            {
                return;
            }

            var copy = new byte[blockBytes.Length];
            Array.Copy(blockBytes, copy, copy.Length);

            if (blocks.TryGetValue(sector, out var block))
            {
                lru.Remove(block.Node);
                if (block.Dirty && !dirty)
                {
                    DirtyCount--;
                }
                else if (!block.Dirty && dirty)
                {
                    DirtyCount++;
                }
            }
            else
            {
                block = new CachedBlock { Sector = sector };
                block.Node = new LinkedListNode<CachedBlock>(block);
                blocks[sector] = block;
                if (dirty)
                {
                    DirtyCount++;
                }
            }

            block.Bytes = copy;
            block.Dirty = dirty;
            lru.AddFirst(block.Node);

            EvictCleanBlocks();
        }

        /// <summary>
        /// Remove cached blocks for logical sectors, also dirty blocks, e.g. when sectors are written as data blocks
        /// </summary>
        public void Invalidate(uint sector, int count = 1)
        {
            if (blocks.Count == 0)
            {
                return;
            }

            for (var i = 0; i < count; i++)
            {
                if (!blocks.TryGetValue(sector + (uint)i, out var block))
                {
                    continue;
                }

                lru.Remove(block.Node);
                blocks.Remove(block.Sector);
                if (block.Dirty)
                {
                    DirtyCount--;
                }
            }
        }

        /// <summary>
        /// Get dirty blocks ordered by sector
        /// </summary>
        public IList<CachedBlock> GetDirtyBlocks()
        {
            var dirtyBlocks = new List<CachedBlock>(DirtyCount);
            if (DirtyCount == 0)
            {
                return dirtyBlocks;
            }

            foreach (var block in blocks.Values)
            {
                if (block.Dirty)
                {
                    dirtyBlocks.Add(block);
                }
            }

            dirtyBlocks.Sort((x, y) => x.Sector.CompareTo(y.Sector));
            return dirtyBlocks;
        }

        /// <summary>
        /// Mark block clean after it's written to disk
        /// </summary>
        public void MarkClean(CachedBlock block)
        {
            if (!block.Dirty)
            {
                return;
            }

            block.Dirty = false;
            DirtyCount--;
        }

        /// <summary>
        /// Evict least recently used clean blocks, while capacity is exceeded
        /// </summary>
        public void EvictCleanBlocks()
        {
            var node = lru.Last;
            while (blocks.Count > capacity && node != null)
            {
                var previous = node.Previous;
                if (!node.Value.Dirty)
                {
                    lru.Remove(node);
                    blocks.Remove(node.Value.Sector);
                }

                node = previous;
            }
        }

        public void Clear()
        {
            blocks.Clear();
            lru.Clear();
            DirtyCount = 0;
        }
    }
}
