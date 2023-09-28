namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System.IO;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;

    public static class DrawerDataWriter
    {
        public static async Task Write(DrawerData drawerData, Stream stream)
        {
            await stream.WriteBigEndianInt16(drawerData.LeftEdge);
            await stream.WriteBigEndianInt16(drawerData.TopEdge);
            await stream.WriteBigEndianInt16(drawerData.Width);
            await stream.WriteBigEndianInt16(drawerData.Height);
            stream.WriteByte(drawerData.DetailPen);
            stream.WriteByte(drawerData.BlockPen);
            await stream.WriteBigEndianUInt32(drawerData.IdcmpFlags);
            await stream.WriteBigEndianUInt32(drawerData.Flags);
            await stream.WriteBigEndianUInt32(drawerData.FirstGadget);
            await stream.WriteBigEndianUInt32(drawerData.CheckMark);
            await stream.WriteBigEndianUInt32(drawerData.Title);
            await stream.WriteBigEndianUInt32(drawerData.Screen);
            await stream.WriteBigEndianUInt32(drawerData.BitMap);
            await stream.WriteBigEndianInt16(drawerData.MinWidth);
            await stream.WriteBigEndianInt16(drawerData.MinHeight);
            await stream.WriteBigEndianUInt16(drawerData.MaxWidth);
            await stream.WriteBigEndianUInt16(drawerData.MaxHeight);
            await stream.WriteBigEndianUInt16(drawerData.Type);
            await stream.WriteBigEndianInt32(drawerData.CurrentX);
            await stream.WriteBigEndianInt32(drawerData.CurrentY);
        }
    }
}