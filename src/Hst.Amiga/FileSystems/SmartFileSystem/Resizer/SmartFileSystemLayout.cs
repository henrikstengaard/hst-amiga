namespace Hst.Amiga.FileSystems.SmartFileSystem.Resizer
{
    using System.Collections.Generic;
    using Blocks;

    internal enum BlockReferenceKind
    {
        /// <summary>
        /// Block number stored as is.
        /// </summary>
        Block,

        /// <summary>
        /// Block number in node index container stored shifted left by log2(block size) - 5 with flags in low bits.
        /// </summary>
        NodeIndex
    }

    /// <summary>
    /// Block pointer stored in holder block at offset pointing to target block.
    /// </summary>
    internal readonly struct BlockReference
    {
        public BlockReference(uint holder, int offset, uint target, BlockReferenceKind kind)
        {
            Holder = holder;
            Offset = offset;
            Target = target;
            Kind = kind;
        }

        public uint Holder { get; }
        public int Offset { get; }
        public uint Target { get; }
        public BlockReferenceKind Kind { get; }
    }

    /// <summary>
    /// Admin space entry in admin space container.
    /// </summary>
    internal class AdminSpaceEntry
    {
        public uint Container { get; set; }
        public int Index { get; set; }
        public uint Space { get; set; }
    }

    /// <summary>
    /// File object with pointer to first extent.
    /// </summary>
    internal class FileObject
    {
        public uint Container { get; set; }
        public int Offset { get; set; }
        public uint FirstExtent { get; set; }
    }

    /// <summary>
    /// Structures of a Smart File System volume used to resize volume: administration blocks, block pointers
    /// between administration blocks, admin spaces, extents and files.
    /// </summary>
    internal class SmartFileSystemLayout
    {
        public int BlockSize { get; set; }
        public int ShiftsBlock32 { get; set; }
        public uint TotalBlocks { get; set; }
        public byte[] RootBlock { get; set; }
        public uint BitmapBase { get; set; }
        public uint AdminSpaceContainer { get; set; }
        public uint RootObjectContainer { get; set; }
        public uint ExtentBNodeRoot { get; set; }
        public uint ObjectNodeRoot { get; set; }
        public uint TransactionBlock => RootObjectContainer + 2;

        public List<uint> AdminContainers { get; } = new List<uint>();
        public List<AdminSpaceEntry> AdminSpaces { get; } = new List<AdminSpaceEntry>();
        public HashSet<uint> AdminBlocks { get; } = new HashSet<uint>();
        public List<BlockReference> References { get; } = new List<BlockReference>();
        public List<uint> BNodeContainers { get; } = new List<uint>();
        public SortedDictionary<uint, ExtentBNode> Extents { get; } = new SortedDictionary<uint, ExtentBNode>();
        public List<FileObject> Files { get; } = new List<FileObject>();
    }
}
