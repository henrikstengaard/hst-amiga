using System.Collections.Generic;
using Hst.Amiga.DataTypes.DiskObjects.ColorIcons;
using Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons;

namespace Hst.Amiga.DataTypes.DiskObjects
{
    /// <summary>
    /// Amiga icon containing a disk object and an optional color icon.
    /// The disk object contains the basic information about the icon, such as its type, position, and stack size.
    /// The color icon contains the image data for the icon, if it has a color icon.
    /// The true color icons contain the image data for the icon, if it has a true color icon.
    /// The true color icons are stored in PNG format and can be used to display the icon on modern systems that support PNG images.
    /// </summary>
    public class AmigaIcon
    {
        public enum IconKind
        {
            Normal,
            TrueColor
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
        /// TrueColor icons.
        /// </summary>
        public IEnumerable<TrueColorIcon> TrueColorIcons { get; set; }
        
        /// <summary>
        /// Tailing data contains data read after disk object and color icon.
        /// If color icons are not present, tailing data contains data read after disk object.
        /// </summary>
        public byte[] TailingData { get; set; }
    }
}