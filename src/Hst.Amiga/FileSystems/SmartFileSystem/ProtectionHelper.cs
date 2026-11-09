namespace Hst.Amiga.FileSystems.SmartFileSystem
{
    /// <summary>
    /// Protection helper for smart file system.
    /// Smart file system stores protection as AmigaDOS protection bits XOR 0x0F, which means read, write, execute
    /// and delete bits are set when allowed. This matches the meaning of <see cref="ProtectionBits"/>.
    /// Bits above the lower 8 bits (group and other) are kept unchanged.
    /// </summary>
    public static class ProtectionHelper
    {
        public const uint DefaultProtection =
            (uint)(ProtectionBits.Read | ProtectionBits.Write | ProtectionBits.Executable | ProtectionBits.Delete);

        public static ProtectionBits ToProtectionBits(uint protection) => (ProtectionBits)(protection & 0xff);

        public static uint ToProtection(ProtectionBits protectionBits, uint existingProtection = 0) =>
            (existingProtection & ~0xffU) | ((uint)protectionBits & 0xff);

        /// <summary>
        /// Convert AmigaDOS fib_Protection value (read, write, execute and delete bits are set when not allowed)
        /// to smart file system protection.
        /// </summary>
        public static uint FromAmigaDosProtection(uint fibProtection) => fibProtection ^ 0x0f;

        /// <summary>
        /// Convert smart file system protection to AmigaDOS fib_Protection value.
        /// </summary>
        public static uint ToAmigaDosProtection(uint protection) => protection ^ 0x0f;

        public static bool IsReadable(uint protection) => (protection & (uint)ProtectionBits.Read) != 0;
        public static bool IsWritable(uint protection) => (protection & (uint)ProtectionBits.Write) != 0;
        public static bool IsDeletable(uint protection) => (protection & (uint)ProtectionBits.Delete) != 0;
    }
}
