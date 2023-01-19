namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System.IO;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;

    public static class DiskObjectWriter
    {
        public static async Task Write(DiskObject diskObject, Stream stream)
        {
            await stream.WriteBigEndianUInt16(diskObject.Magic);
            await stream.WriteBigEndianUInt16(diskObject.Version);
            await GadgetWriter.Write(diskObject.Gadget, stream);
            stream.WriteByte(diskObject.Type);
            stream.WriteByte(diskObject.Pad);
            await stream.WriteBigEndianUInt32(diskObject.DefaultToolPointer);
            await stream.WriteBigEndianUInt32(diskObject.ToolTypesPointer);
            await stream.WriteBigEndianInt32(diskObject.CurrentX);
            await stream.WriteBigEndianInt32(diskObject.CurrentY);
            await stream.WriteBigEndianUInt32(diskObject.DrawerDataPointer);
            await stream.WriteBigEndianUInt32(diskObject.ToolWindowPointer);
            await stream.WriteBigEndianInt32(diskObject.StackSize);
            
            if (diskObject.DrawerDataPointer != 0)
            {
                await DrawerDataWriter.Write(diskObject.DrawerData, stream);
            }

            if (diskObject.Gadget.GadgetRenderPointer != 0)
            {
                await ImageDataWriter.Write(diskObject.FirstImageData, stream);
            }

            if (diskObject.Gadget.SelectRenderPointer != 0)
            {
                await ImageDataWriter.Write(diskObject.SecondImageData, stream);
            }
            
            if (diskObject.DefaultToolPointer != 0)
            {
                await TextDataWriter.Write(diskObject.DefaultTool, stream);
            }

            if (diskObject.ToolTypesPointer != 0)
            {
                await ToolTypesWriter.Write(diskObject.ToolTypes, stream);
            }

            if (diskObject.ToolWindowPointer != 0)
            {
                throw new IOException(
                    "ToolWindowPointer is defined. This is an extension, which was never implemented");
            }

            if (diskObject.DrawerDataPointer != 0 && diskObject.Gadget.UserDataPointer == 1)
            {
                await DrawerData2Writer.Write(diskObject.DrawerData2, stream);
            }
        }
    }
}