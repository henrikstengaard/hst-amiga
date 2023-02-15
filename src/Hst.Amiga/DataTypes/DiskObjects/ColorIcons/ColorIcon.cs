namespace Hst.Amiga.DataTypes.DiskObjects.ColorIcons
{
    public class ColorIcon
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Flags { get; set; }
        public int Aspect { get; set; }
        public int MaxPalBytes { get; set; }
        public ColorIconImage[] Images { get; set; }
    }
}