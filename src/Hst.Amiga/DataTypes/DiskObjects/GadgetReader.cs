namespace HstWbInstaller.Core.IO.Info
{
    using System.IO;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;

    public static class GadgetReader
    {
        public static async Task<Gadget> Read(Stream stream)
        {
            var nextPointer = await stream.ReadBigEndianUInt32();
            var leftEdge = await stream.ReadBigEndianInt16();
            var topEdge = await stream.ReadBigEndianInt16();
            var width = await stream.ReadBigEndianInt16();
            var height = await stream.ReadBigEndianInt16();
            var flags = await stream.ReadBigEndianUInt16();
            var activation = await stream.ReadBigEndianUInt16();
            var gadgetType = await stream.ReadBigEndianUInt16();
            var gadgetRenderPointer = await stream.ReadBigEndianUInt32();
            var selectRenderPointer = await stream.ReadBigEndianUInt32();
            var gadgetTextPointer = await stream.ReadBigEndianUInt32();
            var mutualExclude = await stream.ReadBigEndianInt32();
            var specialInfoPointer = await stream.ReadBigEndianUInt32();
            var gadgetId = await stream.ReadBigEndianUInt16();
            var userDataPointer = await stream.ReadBigEndianUInt32();

            return new Gadget
            {
                NextPointer = nextPointer,
                LeftEdge = leftEdge,
                TopEdge = topEdge,
                Width = width,
                Height = height,
                Flags = flags,
                Activation = activation,
                GadgetType = gadgetType,
                GadgetRenderPointer = gadgetRenderPointer,
                SelectRenderPointer = selectRenderPointer,
                GadgetTextPointer = gadgetTextPointer,
                MutualExclude = mutualExclude,
                SpecialInfoPointer = specialInfoPointer,
                GadgetId = gadgetId,
                UserDataPointer = userDataPointer
            };
        }
    }
}