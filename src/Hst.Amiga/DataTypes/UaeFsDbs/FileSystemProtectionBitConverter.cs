using System.IO;
using Hst.Amiga.FileSystems;

namespace Hst.Amiga.DataTypes.UaeFsDbs
{
    public static class FileSystemProtectionBitConverter
    {
        public static ProtectionBits UpdateProtectionBitsFromFile(FileInfo fileInfo, ProtectionBits protectionBits)
        {
            var newProtectionBits = ProtectionBitsConverter.ReadProtectionBitsFromFile(fileInfo);

            // mask away "----RWED" protection bits
            newProtectionBits = (ProtectionBits)((int)newProtectionBits ^ 0xf);
            newProtectionBits |= protectionBits & ProtectionBits.Script;

            return newProtectionBits;
        }
    }
}