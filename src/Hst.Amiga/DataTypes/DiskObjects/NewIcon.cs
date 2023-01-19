namespace Hst.Amiga.DataTypes.DiskObjects
{
    using Imaging;

    public class NewIcon
    {
        public bool Transparent { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Depth { get; set; }
        public Image Image { get; set; }
        // public byte[][] Palette { get; set; }
        // public byte[] ImagePixels { get; set; }
    }
}