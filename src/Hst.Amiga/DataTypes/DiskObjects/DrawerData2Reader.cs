namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System.IO;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;

    public static class DrawerData2Reader
    {
        public static async Task<DrawerData2> Read(Stream stream)
        {
            var ddFlags = await stream.ReadBigEndianUInt32();
            var ddViewModes = await stream.ReadBigEndianUInt16();

            return new DrawerData2
            {
                Flags = ddFlags,
                ViewModes = ddViewModes
            };
        }
    }
}