namespace Hst.Amiga.DataTypes.DiskObjects.NewIcons
{
    using Hst.Imaging;

    public class NewIcon
    {
        public bool Transparent { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Depth { get; set; }
        //public Image Image { get; set; }
        public Color[] Palette { get; set; }
        public byte[] Data { get; set; }
    }
}