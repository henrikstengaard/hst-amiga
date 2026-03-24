using System.Collections.Generic;
using System.Linq;
using Hst.Imaging;

namespace Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons
{
    public class TrueColorIcon
    {
        public byte[] PngData { get; private set; }
        public readonly byte[] Header;
        public PngChunk[] Chunks { get; private set; }
        public Image Image { get; private set; }

        public TrueColorIcon(byte[] pngData, byte[] header, PngChunk[] chunks, Image image)
        {
            PngData = pngData;
            Header = header;
            Chunks = chunks;
            Image = image;
        }

        public void UpdateChunks(IEnumerable<PngChunk> chunks)
        {
            var pngChunks = chunks as PngChunk[] ?? chunks.ToArray();
            PngData = Constants.PngSignature.Concat(pngChunks.SelectMany(chunk => chunk.ChunkData)).ToArray();
                Chunks = pngChunks.ToArray();
        }
    }
}