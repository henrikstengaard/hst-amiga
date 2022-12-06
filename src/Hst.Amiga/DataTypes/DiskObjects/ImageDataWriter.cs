namespace HstWbInstaller.Core.IO.Info
{
    using System.IO;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;

    public static class ImageDataWriter
    {
        public static async Task Write(ImageData imageData, Stream stream)
        {
            await stream.WriteBigEndianInt16(imageData.LeftEdge);
            await stream.WriteBigEndianInt16(imageData.TopEdge);
            await stream.WriteBigEndianInt16(imageData.Width);
            await stream.WriteBigEndianInt16(imageData.Height);
            await stream.WriteBigEndianInt16(imageData.Depth);

            await stream.WriteBigEndianUInt32(imageData.ImageDataPointer);
            stream.WriteByte(imageData.PlanePick);
            stream.WriteByte(imageData.PlaneOnOff);
            await stream.WriteBigEndianUInt32(imageData.NextPointer);
            await stream.WriteBytes(imageData.Data);
        }
    }
}