namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using System.Collections.Generic;

    /// <summary>
    /// Object container block (struct fsObjectContainer) containing one or more objects of a directory.
    /// </summary>
    public class ObjectContainer : Block
    {
        /// <summary>
        /// Object node of parent directory, 0 for root object container.
        /// </summary>
        public uint Parent { get; set; }

        /// <summary>
        /// Next object container block in directory, 0 if last.
        /// </summary>
        public uint Next { get; set; }

        /// <summary>
        /// Previous object container block in directory, 0 if first.
        /// </summary>
        public uint Previous { get; set; }

        public IList<SfsObject> Objects { get; set; }

        /// <summary>
        /// Root info, only present in root object container.
        /// </summary>
        public RootInfo RootInfo { get; set; }

        public ObjectContainer() : base(Constants.ObjectContainerId)
        {
            Objects = new List<SfsObject>();
        }

        /// <summary>
        /// Offset of first unused byte in object container.
        /// </summary>
        public int GetEmptySpaceOffset()
        {
            var offset = Constants.ObjectContainerHeaderSize;
            foreach (var sfsObject in Objects)
            {
                offset += sfsObject.GetStoredSize();
            }

            return offset;
        }

        /// <summary>
        /// Checks if objects fits in object container using same rules as smart file system, where each object
        /// must have enough space for fsObject structure, name and comment.
        /// </summary>
        public bool Fits(int blockSize)
        {
            var endOffset = blockSize - (RootInfo != null ? Constants.RootInfoSize : 0);
            var offset = Constants.ObjectContainerHeaderSize;
            foreach (var sfsObject in Objects)
            {
                if (blockSize - offset < sfsObject.GetBytesNeeded())
                {
                    return false;
                }

                offset += sfsObject.GetStoredSize();
            }

            return offset <= endOffset;
        }
    }
}
