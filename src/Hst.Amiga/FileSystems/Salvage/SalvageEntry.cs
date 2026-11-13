namespace Hst.Amiga.FileSystems.Salvage
{
    using System;

    /// <summary>
    /// Directory or file found by salvage with metadata to preserve.
    /// </summary>
    public class SalvageEntry
    {
        /// <summary>
        /// Amiga name of directory or file.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Type of entry, either directory or file.
        /// </summary>
        public EntryType Type { get; set; }

        /// <summary>
        /// Size of file in bytes, 0 for directories.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Raw Amiga protection value (fib_Protection). Bits 0-7 are hsparwed, where rwed bits are inverted
        /// (set means not allowed). Bits 8-31 are multi user protection bits.
        /// </summary>
        public uint Protection { get; set; }

        /// <summary>
        /// Protection bits hsparwed converted from raw Amiga protection value.
        /// </summary>
        public ProtectionBits ProtectionBits => ProtectionBitsConverter.ToProtectionBits((int)(Protection & 0xff));

        /// <summary>
        /// Date of directory or file.
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Comment (file note) of directory or file.
        /// </summary>
        public string Comment { get; set; }
    }
}
