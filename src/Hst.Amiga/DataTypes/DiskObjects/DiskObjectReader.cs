namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System.IO;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;

    public static class DiskObjectReader
    {
        public static async Task<DiskObject> Read(Stream stream)
        {
            var magic = await stream.ReadBigEndianUInt16();

            if (magic != 0xe310)
            {
                throw new IOException("Invalid disk object magic");
            }

            var version = await stream.ReadBigEndianUInt16();

            var diskObject = new DiskObject
            {
                Magic = magic,
                Version = version,
                Gadget = await GadgetReader.Read(stream),
                Type = (byte)stream.ReadByte()
            };

            diskObject.Pad = (byte)stream.ReadByte();
            diskObject.DefaultToolPointer = await stream.ReadBigEndianUInt32();
            diskObject.ToolTypesPointer = await stream.ReadBigEndianUInt32();
            diskObject.CurrentX = await stream.ReadBigEndianInt32();
            diskObject.CurrentY = await stream.ReadBigEndianInt32();
            diskObject.DrawerDataPointer = await stream.ReadBigEndianUInt32();
            diskObject.ToolWindowPointer = await stream.ReadBigEndianUInt32();
            diskObject.StackSize = await stream.ReadBigEndianInt32();

            if (diskObject.DrawerDataPointer != 0)
            {
                diskObject.DrawerData = await DrawerDataReader.Read(stream);
            }

            if (diskObject.Gadget.GadgetRenderPointer != 0)
            {
                diskObject.FirstImageData = await ImageDataReader.Read(stream);
            }

            if (diskObject.Gadget.SelectRenderPointer != 0)
            {
                diskObject.SecondImageData = await ImageDataReader.Read(stream);
            }
            
            if (diskObject.DefaultToolPointer != 0)
            {
                diskObject.DefaultTool = await TextDataReader.Read(stream);
            }

            if (diskObject.ToolTypesPointer != 0)
            {
                diskObject.ToolTypes = await ToolTypesReader.Read(stream);
            }

            if (diskObject.ToolWindowPointer != 0)
            {
                throw new IOException(
                    "ToolWindowPointer is defined. This is an extension, which was never implemented");
            }

            if (diskObject.DrawerDataPointer != 0 && diskObject.Gadget.UserDataPointer == 1)
            {
                diskObject.DrawerData2 = await DrawerData2Reader.Read(stream);
            }

            return diskObject;
        }
    }
}

    