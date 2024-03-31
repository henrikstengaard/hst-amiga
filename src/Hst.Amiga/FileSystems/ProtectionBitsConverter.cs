using System.IO;

namespace Hst.Amiga.FileSystems
{
    public static class ProtectionBitsConverter
    {
        public static ProtectionBits ToProtectionBits(int protectionValue)
        {
            var protectionFlags = (ProtectionBits)protectionValue;
            var protectionBits = ProtectionBits.None;

            // add held resident flag, if bit is present
            if (protectionFlags.HasFlag(ProtectionBits.HeldResident))
            {
                protectionBits |= ProtectionBits.HeldResident;
            }

            // add script flag, if bit is present
            if (protectionFlags.HasFlag(ProtectionBits.Script))
            {
                protectionBits |= ProtectionBits.Script;
            }

            // add pure flag, if bit is present
            if (protectionFlags.HasFlag(ProtectionBits.Pure))
            {
                protectionBits |= ProtectionBits.Pure;
            }

            // add archive flag, if bit is present
            if (protectionFlags.HasFlag(ProtectionBits.Archive))
            {
                protectionBits |= ProtectionBits.Archive;
            }
            
            // add read flag, if bit is not present
            if (!protectionFlags.HasFlag(ProtectionBits.Read))
            {
                protectionBits |= ProtectionBits.Read;
            }
            
            // add write flag, if bit is not present
            if (!protectionFlags.HasFlag(ProtectionBits.Write))
            {
                protectionBits |= ProtectionBits.Write;
            }
            
            // add executable flag, if bit is not present
            if (!protectionFlags.HasFlag(ProtectionBits.Executable))
            {
                protectionBits |= ProtectionBits.Executable;
            }

            // add delete flag, if bit is not present
            if (!protectionFlags.HasFlag(ProtectionBits.Delete))
            {
                protectionBits |= ProtectionBits.Delete;
            }
            
            return protectionBits;
        }

        public static byte ToProtectionValue(ProtectionBits protectionBits)
        {
            byte protectionValue = 0;

            if (protectionBits.HasFlag(ProtectionBits.HeldResident))
            {
                protectionValue |= (int)ProtectionBits.HeldResident;
            }
            
            if (protectionBits.HasFlag(ProtectionBits.Script))
            {
                protectionValue |= (int)ProtectionBits.Script;
            }
            
            if (protectionBits.HasFlag(ProtectionBits.Pure))
            {
                protectionValue |= (int)ProtectionBits.Pure;
            }
            
            if (protectionBits.HasFlag(ProtectionBits.Archive))
            {
                protectionValue |= (int)ProtectionBits.Archive;
            }
            
            if (!protectionBits.HasFlag(ProtectionBits.Read))
            {
                protectionValue |= (int)ProtectionBits.Read;
            }

            if (!protectionBits.HasFlag(ProtectionBits.Write))
            {
                protectionValue |= (int)ProtectionBits.Write;
            }
            
            if (!protectionBits.HasFlag(ProtectionBits.Executable))
            {
                protectionValue |= (int)ProtectionBits.Executable;
            }
            
            if (!protectionBits.HasFlag(ProtectionBits.Delete))
            {
                protectionValue |= (int)ProtectionBits.Delete;
            }

            return protectionValue;
        }
        
        public static ProtectionBits ReadProtectionBitsFromFile(FileInfo fileInfo)
        {
            var protectionBits = ProtectionBits.Executable | ProtectionBits.Read;

            if (!fileInfo.Attributes.HasFlag(FileAttributes.Archive))
            {
                protectionBits |= ProtectionBits.Archive;
            }
            if (!fileInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                protectionBits |= ProtectionBits.Write | ProtectionBits.Delete;
            }
            if (fileInfo.Attributes.HasFlag(FileAttributes.System))
            {
                protectionBits |= ProtectionBits.Pure;
            }
            if (fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
            {
                protectionBits |= ProtectionBits.HeldResident;
            }

            return protectionBits;
        }
    }
}