using System.Collections.Generic;

namespace Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons
{
    public class IconData
    {
        public readonly IList<IconTag> IconTags;
        public readonly string DefaultTool;
        public readonly string ToolType;
        public readonly string ToolWindow;
    
        public IconData(IList<IconTag> iconTags, string defaultTool, string toolType,
            string toolWindow)
        {
            IconTags = iconTags;
            DefaultTool = defaultTool;
            ToolType = toolType;
            ToolWindow = toolWindow;
        }
    }
}