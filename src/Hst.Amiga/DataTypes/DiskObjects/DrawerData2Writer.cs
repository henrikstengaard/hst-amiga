namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System.IO;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;

    public static class DrawerData2Writer
    {
        public static async Task Write(DrawerData2 drawerData2, Stream stream)
        {
            await stream.WriteBigEndianUInt32(drawerData2.Flags);
            await stream.WriteBigEndianUInt16(drawerData2.ViewModes);
        }
    }
}