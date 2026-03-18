namespace Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons
{
#if !NETSTANDARD2_1_OR_GREATER
    public record TrueColorIcon(byte[] PngData, byte[] Header, PngChunk[] Chunks)
    {
    }
#else
    public class TrueColorIcon
    {
        public readonly byte[] PngData;
        public readonly byte[] Header;
        public readonly PngChunk[] Chunks;
        
        public TrueColorIcon(byte[] pngData, byte[] header, PngChunk[] chunks)
        {
            PngData = pngData;
            Header = header;
            Chunks = chunks;
        }
    }
#endif
}