using System.Collections.Generic;
using Hst.Amiga.DataTypes.DiskObjects.PngIcons;

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