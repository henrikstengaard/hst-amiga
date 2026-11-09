namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using System;

    /// <summary>
    /// Object (struct fsObject) representing a file, directory or soft link stored in an object container.
    /// </summary>
    public class SfsObject
    {
        public ushort OwnerUid { get; set; }
        public ushort OwnerGid { get; set; }

        /// <summary>
        /// Object node number of object.
        /// </summary>
        public uint ObjectNode { get; set; }

        /// <summary>
        /// Protection bits. Read, write, execute and delete bits are set when allowed (inverted compared to AmigaDOS).
        /// </summary>
        public uint Protection { get; set; }

        /// <summary>
        /// File: first data block and key of first extent, 0 if file is empty.
        /// Soft link: block of soft link block.
        /// Directory: hash table block.
        /// </summary>
        public uint Data { get; set; }

        /// <summary>
        /// File: size of file in bytes.
        /// Directory: first object container block of directory, 0 if directory is empty.
        /// </summary>
        public uint Size { get; set; }

        public DateTime DateModified { get; set; }
        public byte Bits { get; set; }
        public string Name { get; set; }
        public string Comment { get; set; }

        public SfsObject()
        {
            Name = string.Empty;
            Comment = string.Empty;
        }

        /// <summary>
        /// Hash table block for directory.
        /// </summary>
        public uint HashTable
        {
            get => Data;
            set => Data = value;
        }

        /// <summary>
        /// First object container block for directory.
        /// </summary>
        public uint FirstDirBlock
        {
            get => Size;
            set => Size = value;
        }

        public bool IsDir => (Bits & Constants.OTypeDir) != 0;
        public bool IsSoftLink => !IsDir && (Bits & Constants.OTypeLink) != 0 && (Bits & Constants.OTypeHardLink) == 0;
        public bool IsHardLink => (Bits & Constants.OTypeLink) != 0 && (Bits & Constants.OTypeHardLink) != 0;
        public bool IsFile => !IsDir && (Bits & Constants.OTypeLink) == 0;
        public bool IsHidden => (Bits & Constants.OTypeHidden) != 0;
        public bool IsUndeletable => (Bits & Constants.OTypeUndeletable) != 0;
        public bool IsQuickDir => (Bits & Constants.OTypeQuickDir) != 0;

        /// <summary>
        /// Size of object in bytes when stored in object container including padding to even size.
        /// </summary>
        public int GetStoredSize()
        {
            var size = Constants.ObjectHeaderSize + AmigaTextHelper.GetBytes(Name).Length + 1 +
                       AmigaTextHelper.GetBytes(Comment).Length + 1;
            return (size & 1) == 1 ? size + 1 : size;
        }

        /// <summary>
        /// Bytes needed to create object in object container, see createobjecttags() in objects.c.
        /// </summary>
        public int GetBytesNeeded()
        {
            return Constants.ObjectStructureSize + AmigaTextHelper.GetBytes(Name).Length + 2 +
                   AmigaTextHelper.GetBytes(Comment).Length;
        }

        public SfsObject Clone()
        {
            return (SfsObject)MemberwiseClone();
        }
    }
}
