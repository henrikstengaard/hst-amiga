namespace Hst.Amiga.DataTypes.DiskObjects.PngIcons
{
#if !NETSTANDARD2_1_OR_GREATER
    public record PngIcon(byte[] PngData, byte[] Header, PngChunk[] Chunks)
    {
    }
#else
    public class PngIcon
    {
        public readonly byte[] PngData;
        public readonly byte[] Header;
        public readonly PngChunk[] Chunks;
        
        public PngIcon(byte[] pngData, byte[] header, PngChunk[] chunks)
        {
            PngData = pngData;
            Header = header;
            Chunks = chunks;
        }
    }
#endif
}