namespace Hst.Amiga.FileSystems.SmartFileSystem
{
    /// <summary>
    /// Hash helper for smart file system, see fs/support.c in smart file system source.
    /// </summary>
    public static class HashHelper
    {
        /// <summary>
        /// Convert character to upper case using international character set (ISO-8859-1).
        /// </summary>
        public static byte UpperChar(byte c)
        {
            if ((c >= 224 && c <= 254 && c != 247) || (c >= 'a' && c <= 'z'))
            {
                c -= 32;
            }

            return c;
        }

        /// <summary>
        /// Calculate 16-bit hash of name.
        /// </summary>
        public static ushort Hash(string name, bool caseSensitive)
        {
            var bytes = AmigaTextHelper.GetBytes(name);

            var length = 0;
            while (length < bytes.Length && bytes[length] != 0 && bytes[length] != '/')
            {
                length++;
            }

            var hash = (ushort)length;
            unchecked
            {
                for (var i = 0; i < length; i++)
                {
                    hash = (ushort)(hash * 13 + (caseSensitive ? bytes[i] : UpperChar(bytes[i])));
                }
            }

            return hash;
        }

        /// <summary>
        /// Number of hash entries (chains) in a hash table block.
        /// </summary>
        public static int HashEntries(int blockSize) => (blockSize - Constants.HashTableHeaderSize) >> 2;

        /// <summary>
        /// Hash chain for hash value, HASHCHAIN() in smart file system source.
        /// </summary>
        public static int HashChain(ushort hash, int blockSize) => (ushort)(hash % (ushort)HashEntries(blockSize));

        /// <summary>
        /// Compare names using case sensitivity of volume.
        /// </summary>
        public static bool NamesEqual(string name1, string name2, bool caseSensitive)
        {
            if (caseSensitive)
            {
                return string.Equals(name1, name2, System.StringComparison.Ordinal);
            }

            if (name1.Length != name2.Length)
            {
                return false;
            }

            var bytes1 = AmigaTextHelper.GetBytes(name1);
            var bytes2 = AmigaTextHelper.GetBytes(name2);
            if (bytes1.Length != bytes2.Length)
            {
                return false;
            }

            for (var i = 0; i < bytes1.Length; i++)
            {
                if (UpperChar(bytes1[i]) != UpperChar(bytes2[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
