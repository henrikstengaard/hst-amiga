using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Imaging;
using Hst.Imaging.Pngcs;

namespace Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons
{
    public static class TrueColorIconHelper
    {
        public static async Task<TrueColorIcon> CreateTrueColorIcon(Image image)
        {
            byte[] pngData;
            using (var pngWriteStream = new MemoryStream())
            {
                PngWriter.Write(pngWriteStream, image);
                pngData = pngWriteStream.ToArray();
            }

            using var pngReadStream = new MemoryStream(pngData);
            return (await TrueColorIconReader.ReadTrueColorIcons(pngReadStream)).FirstOrDefault();
        }
    }
}