namespace Hst.Amiga.DataTypes.DiskObjects.PngIcons
{
#if !NETSTANDARD2_1_OR_GREATER
    public record PngChunk(byte[] ChunkData, uint Length, byte[] Type, byte[] Data, uint Crc)
    {
    }
#else
    public class PngChunk
    {
        public readonly byte[] ChunkData;
        public readonly uint Length;
        public readonly byte[] Type;
        public readonly byte[] Data;
        public readonly uint Crc;

        public PngChunk(byte[] chunkData, uint length, byte[] type, byte[] data, uint crc)
        {
            ChunkData = chunkData;
            Length = length;
            Type = type;
            Data = data;
            Crc = crc;
        }
    }
#endif
}