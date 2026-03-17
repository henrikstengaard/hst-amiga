using System.Collections.Generic;
using Hst.Amiga.DataTypes.DiskObjects.ColorIcons;
using Hst.Amiga.DataTypes.DiskObjects.PngIcons;

namespace Hst.Amiga.DataTypes.DiskObjects
{
    /// <summary>
    /// Amiga icon containing a disk object and an optional color icon. The disk object contains the basic information about the icon, such as its type, position, and stack size. The color icon contains the image data for the icon, if it has a color icon.
    /// </summary>
    public class AmigaIcon
    {
        public enum IconKind
        {
            Normal,
            PngIcon
        }
        
        public IconKind Kind { get; set; }
        
        /// <summary>
        /// Disk object.
        /// </summary>
        public DiskObject DiskObject { get; set; }
        
        /// <summary>
        /// Color icon.
        /// </summary>
        public ColorIcon ColorIcon { get; set; }
        
        /// <summary>
        /// Png icons.
        /// </summary>
        public IEnumerable<PngIcon> PngIcons { get; set; }
    }
}