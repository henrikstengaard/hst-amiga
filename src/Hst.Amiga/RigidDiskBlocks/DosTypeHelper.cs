namespace Hst.Amiga.RigidDiskBlocks
{
    using System;
    using System.Linq;
    using System.Text;

    public static class DosTypeHelper
    {
        public static byte[] FormatDosType(string dosType)
        {
            if (dosType.Length != 4)
            {
                throw new ArgumentException("Dos type must be 4 characters in length", nameof(dosType));
            }
            
            return FormatDosType(dosType.Substring(0, 3), Convert.ToByte(dosType[3] - 48));
        }

        public static byte[] FormatDosType(string identifier, byte version)
        {
            if (identifier.Length != 3)
            {
                throw new ArgumentException("Identifier must be 3 characters in length", nameof(identifier));
            }

            if (version > 9)
            {
                throw new ArgumentException("Version must be between 0 and 9", nameof(version));
            }

            return Encoding.ASCII.GetBytes(identifier).Concat(new[] { version }).ToArray();
        }
    }
}