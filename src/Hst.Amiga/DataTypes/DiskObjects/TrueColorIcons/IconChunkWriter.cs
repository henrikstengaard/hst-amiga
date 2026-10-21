using System.IO;
using Hst.Core.Converters;

namespace Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons
{
    public static class IconChunkWriter
    {
        public static byte[] WriteIconChunkData(IconData iconData)
        {
            using var memoryStream = new MemoryStream();
            var data = new byte[4];
            foreach (var iconAttributeTag in iconData.IconTags)
            {
                if (iconAttributeTag.Tag == Constants.IconAttributeTags.ATTR_DEFAULTTOOL ||
                    iconAttributeTag.Tag == Constants.IconAttributeTags.ATTR_TOOLTYPE ||
                    iconAttributeTag.Tag == Constants.IconAttributeTags.ATTR_TOOLWINDOW)
                {
                    continue;
                }
                BigEndianConverter.ConvertUInt32ToBytes((uint)iconAttributeTag.Tag, data, 0);
                memoryStream.Write(data, 0, data.Length);
                BigEndianConverter.ConvertUInt32ToBytes((uint)iconAttributeTag.Value, data, 0);
                memoryStream.Write(data, 0, data.Length);
            }

            if (!string.IsNullOrEmpty(iconData.DefaultTool))
            {
                BigEndianConverter.ConvertUInt32ToBytes((uint)Constants.IconAttributeTags.ATTR_DEFAULTTOOL, data, 0);
                memoryStream.Write(data, 0, data.Length);
                WriteText(memoryStream, iconData.DefaultTool);
            }

            if (!string.IsNullOrEmpty(iconData.ToolType))
            {
                BigEndianConverter.ConvertUInt32ToBytes((uint)Constants.IconAttributeTags.ATTR_TOOLTYPE, data, 0);
                memoryStream.Write(data, 0, data.Length);
                WriteText(memoryStream, iconData.ToolType);
            }

            if (!string.IsNullOrEmpty(iconData.ToolWindow))
            {
                BigEndianConverter.ConvertUInt32ToBytes((uint)Constants.IconAttributeTags.ATTR_TOOLWINDOW, data, 0);
                memoryStream.Write(data, 0, data.Length);
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