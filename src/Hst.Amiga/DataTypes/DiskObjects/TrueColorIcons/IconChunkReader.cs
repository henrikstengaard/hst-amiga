using System.Collections.Generic;
using System.IO;
using Hst.Core.Converters;

namespace Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons
{
    public static class IconChunkReader
    {
        public static IconData ReadIconChunkData(byte[] iconChunk)
        {
            var tags = new List<IconTag>();
            string defaultTool = null;
            string toolType = null;
            string toolWindow = null;
            
            var position = 0;
            while (position < iconChunk.Length)
            {
                var tagUIntValue = BigEndianConverter.ConvertBytesToUInt32(iconChunk, position);
                position += 4;
                var tag = (Constants.IconAttributeTags)tagUIntValue;

                switch (tag)
                {
                    case Constants.IconAttributeTags.ATTR_ICONX:
                    case Constants.IconAttributeTags.ATTR_ICONY:
                    case Constants.IconAttributeTags.ATTR_DD_CURRENTX:
                    case Constants.IconAttributeTags.ATTR_DD_CURRENTY:
                    case Constants.IconAttributeTags.ATTR_DRAWERX:
                    case Constants.IconAttributeTags.ATTR_DRAWERY:
                    case Constants.IconAttributeTags.ATTR_DRAWERWIDTH:
                    case Constants.IconAttributeTags.ATTR_DRAWERHEIGHT:
                    case Constants.IconAttributeTags.ATTR_DRAWERFLAGS:
                    case Constants.IconAttributeTags.ATTR_DRAWERFLAGS2:
                    case Constants.IconAttributeTags.ATTR_DRAWERFLAGS3:
                    case Constants.IconAttributeTags.ATTR_FRAMELESS:
                    case Constants.IconAttributeTags.ATTR_STACKSIZE:
                    case Constants.IconAttributeTags.ATTR_TYPE:
                    case Constants.IconAttributeTags.ATTR_VIEWMODES:
                    case Constants.IconAttributeTags.ATTR_VIEWMODES2:
                        var value = BigEndianConverter.ConvertBytesToUInt32(iconChunk, position);
                        position += 4;
                        tags.Add(new IconTag(tag, value));
                        break;
                    case Constants.IconAttributeTags.ATTR_DEFAULTTOOL:
                        var defaultToolLength = ReadTextLength(iconChunk, position);
                        defaultTool = AmigaTextHelper.GetString(iconChunk, position, defaultToolLength);
                        position += defaultToolLength + 1;
                        break;
                    case Constants.IconAttributeTags.ATTR_TOOLTYPE:
                        var toolTypeLength = ReadTextLength(iconChunk, position);
                        toolType = AmigaTextHelper.GetString(iconChunk, position, toolTypeLength);
                        position += toolTypeLength + 1;
                        break;
                    case Constants.IconAttributeTags.ATTR_TOOLWINDOW:
                        var toolWindowLength = ReadTextLength(iconChunk, position);
                        toolWindow = AmigaTextHelper.GetString(iconChunk, position, toolWindowLength);
                        position += toolWindowLength + 1;
                        break;
                    default:
                        throw new InvalidDataException($"Unknown icon attribute tag: 0x{tagUIntValue:x}");
                }
            }

            return new IconData(tags, defaultTool, toolType, toolWindow);
        }
        
        private static int ReadTextLength(byte[] data, int offset)
        {
            var length = 0;
            while (offset + length < data.Length && data[offset + length] != 0)
            {
                length++;
            }

            return length;
        }
    }
}