namespace Hst.Amiga.DataTypes.DiskObjects.ColorIcons
{
    using System;

    public class ColorIcon
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Flags { get; set; }
        public int Aspect { get; set; }
        
        /// <summary>
        /// Maximum number of bytes used by any color icon image palette. This is calculated and updated when writing color icon.
        /// </summary>
        public int MaxPalBytes { get; set; }

        public ColorIconImage[] Images { get; set; }

        public ColorIcon()
        {
            Images = Array.Empty<ColorIconImage>();
        }
    }
}