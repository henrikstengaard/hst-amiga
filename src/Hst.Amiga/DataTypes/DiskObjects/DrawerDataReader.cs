namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System.IO;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;

    public static class DrawerDataReader
    {
        public static async Task<DrawerData> Read(Stream stream)
        {
            var leftEdge = await stream.ReadBigEndianInt16();
            var topEdge = await stream.ReadBigEndianInt16();
            var width = await stream.ReadBigEndianInt16();
            var height = await stream.ReadBigEndianInt16();
            var detailPen = (byte)stream.ReadByte();
            var blockPen = (byte)stream.ReadByte();
            var idcmpFlags = await stream.ReadBigEndianUInt32();
            var flags = await stream.ReadBigEndianUInt32();
            var firstGadget = await stream.ReadBigEndianUInt32();
            var checkMark = await stream.ReadBigEndianUInt32();
            var title = await stream.ReadBigEndianUInt32();
            var screen = await stream.ReadBigEndianUInt32();
            var bitMap = await stream.ReadBigEndianUInt32();
            var minWidth = await stream.ReadBigEndianInt16();
            var minHeight = await stream.ReadBigEndianInt16();
            var maxWidth = await stream.ReadBigEndianUInt16();
            var maxHeight = await stream.ReadBigEndianUInt16();
            var type = await stream.ReadBigEndianUInt16();
            var currentX = await stream.ReadBigEndianInt32();
            var currentY = await stream.ReadBigEndianInt32();

            return new DrawerData
            {
                LeftEdge = leftEdge,
                TopEdge = topEdge,
                Width = width,
                Height = height,
                DetailPen = detailPen,
                BlockPen = blockPen,
                IdcmpFlags = idcmpFlags,
                Flags = flags,
                FirstGadget = firstGadget,
                CheckMark = checkMark,
                Title = title,
                Screen = screen,
                BitMap = bitMap,
                MinWidth = minWidth,
                MinHeight = minHeight,
                MaxWidth = maxWidth,
                MaxHeight = maxHeight,
                Type = type,
                CurrentX = currentX,
                CurrentY = currentY
            };
        }
    }
}