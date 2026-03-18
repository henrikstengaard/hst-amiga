using System.Collections.Generic;

namespace Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons
{
    public class IconData
    {
        public readonly IList<IconAttributeTag> IconAttributeTags;
        public readonly string DefaultTool;
        public readonly string ToolType;
    
        public IconData(IList<IconAttributeTag> iconAttributeTags, string defaultTool, string toolType)
        {
            IconAttributeTags = iconAttributeTags;
            DefaultTool = defaultTool;
            ToolType = toolType;
        }
    }
}