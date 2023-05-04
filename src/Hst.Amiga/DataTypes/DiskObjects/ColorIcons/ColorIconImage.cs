namespace Hst.Amiga.DataTypes.DiskObjects.ColorIcons
{
    using Imaging;

    public class ColorIconImage
    {
        public int Depth { get; set; }
        public Image Image { get; set; }
        public int PaletteByteSize { get; set; }
    }
}