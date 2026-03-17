namespace Hst.Amiga.DataTypes.DiskObjects.PngIcons
{
    public class PngHeader
    {
        public readonly uint Width;
        public readonly uint Height;
        public readonly byte BitDepth;
        public readonly byte ColorType;
        public readonly byte CompressionMethod;
        public readonly byte FilterMethod;
        public readonly byte InterlaceMethod;
        
        public PngHeader(uint width, uint height, byte bitDepth, byte colorType, byte compressionMethod, byte filterMethod, byte interlaceMethod)
        {
            Width = width;
            Height = height;
            BitDepth = bitDepth;
            ColorType = colorType;
            CompressionMethod = compressionMethod;
            FilterMethod = filterMethod;
            InterlaceMethod = interlaceMethod;
        }
    }
}