using System.IO;
using Hst.Core.Converters;

namespace Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons
{
    public static class IconChunkWriter
    {
        public static byte[] WriteIconChunkData(IconData iconData)
        {
            using var memoryStream = new MemoryStream();
            foreach (var iconAttributeTag in iconData.IconTags)
            {
                if (iconAttributeTag.Tag == Constants.IconAttributeTags.ATTR_DEFAULTTOOL ||
                    iconAttributeTag.Tag == Constants.IconAttributeTags.ATTR_TOOLTYPE ||
                    iconAttributeTag.Tag == Constants.IconAttributeTags.ATTR_TOOLWINDOW)
                {
                    continue;
                }
                
                memoryStream.Write(BigEndianConverter.ConvertUInt32ToBytes((uint)iconAttributeTag.Tag));
                memoryStream.Write(BigEndianConverter.ConvertUInt32ToBytes(iconAttributeTag.Value));
            }

            if (!string.IsNullOrEmpty(iconData.DefaultTool))
            {
                memoryStream.Write(BigEndianConverter.ConvertUInt32ToBytes(
                    (uint)Constants.IconAttributeTags.ATTR_DEFAULTTOOL));
                WriteText(memoryStream, iconData.DefaultTool);
            }

            if (!string.IsNullOrEmpty(iconData.ToolType))
            {
                memoryStream.Write(BigEndianConverter.ConvertUInt32ToBytes(
                    (uint)Constants.IconAttributeTags.ATTR_TOOLTYPE));
                WriteText(memoryStream, iconData.ToolType);
            }

            if (!string.IsNullOrEmpty(iconData.ToolWindow))
            {
                memoryStream.Write(BigEndianConverter.ConvertUInt32ToBytes(
                    (uint)Constants.IconAttributeTags.ATTR_TOOLWINDOW));
                WriteText(memoryStream, iconData.ToolWindow);
            }

            return memoryStream.ToArray();
        }
        
        private static void WriteText(MemoryStream memoryStream, string text)
        {
            var textBytes = AmigaTextHelper.GetBytes(text);
            memoryStream.Write(textBytes, 0, textBytes.Length);
            memoryStream.WriteByte(0);
        }
    }
}