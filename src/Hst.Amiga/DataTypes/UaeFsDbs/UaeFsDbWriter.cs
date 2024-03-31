using System.Text;
using Hst.Core.Converters;

namespace Hst.Amiga.DataTypes.UaeFsDbs
{
    public static class UaeFsDbWriter
    {
        public static byte[] Build(UaeFsDbNode node)
        {
            var nodeBytes = new byte[node.Version == UaeFsDbNode.NodeVersion.Version1
                ? Constants.UaeFsDbNodeVersion1Size
                : Constants.UaeFsDbNodeVersion2Size];
            
            nodeBytes[0] = node.Valid;
            BigEndianConverter.ConvertUInt32ToBytes(node.Mode, nodeBytes, 0x1);
            TextHelper.WriteNullTerminatedString(Encoding.ASCII, node.AmigaName, nodeBytes, 0x5, 257);
            TextHelper.WriteNullTerminatedString(Encoding.ASCII, node.NormalName, nodeBytes, 0x106, 257);
            TextHelper.WriteNullTerminatedString(Encoding.ASCII, node.Comment, nodeBytes, 0x207, 81);

            if (node.Version != UaeFsDbNode.NodeVersion.Version2)
            {
                return nodeBytes;
            }
            
            BigEndianConverter.ConvertUInt32ToBytes(node.WinMode, nodeBytes, 0x258);
            TextHelper.WriteNullTerminatedString(Encoding.Unicode, node.AmigaNameUnicode, nodeBytes, 0x25c, 514);
            TextHelper.WriteNullTerminatedString(Encoding.Unicode, node.NormalNameUnicode, nodeBytes, 0x45e, 514);

            return nodeBytes;
        }
    }
}