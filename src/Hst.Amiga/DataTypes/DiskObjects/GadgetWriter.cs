namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System.IO;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;

    public static class GadgetWriter
    {
        public static async Task Write(Gadget gadget, Stream stream)
        {
            await stream.WriteBigEndianUInt32(gadget.NextPointer);
            await stream.WriteBigEndianInt16(gadget.LeftEdge);
            await stream.WriteBigEndianInt16(gadget.TopEdge);
            await stream.WriteBigEndianInt16(gadget.Width);
            await stream.WriteBigEndianInt16(gadget.Height);
            await stream.WriteBigEndianUInt16(gadget.Flags);
            await stream.WriteBigEndianUInt16(gadget.Activation);
            await stream.WriteBigEndianUInt16(gadget.GadgetType);
            await stream.WriteBigEndianUInt32(gadget.GadgetRenderPointer);
            await stream.WriteBigEndianUInt32(gadget.SelectRenderPointer);
            await stream.WriteBigEndianUInt32(gadget.GadgetTextPointer);
            await stream.WriteBigEndianInt32(gadget.MutualExclude);
            await stream.WriteBigEndianUInt32(gadget.SpecialInfoPointer);
            await stream.WriteBigEndianUInt16(gadget.GadgetId);
            await stream.WriteBigEndianUInt32(gadget.UserDataPointer);
        }
    }
}