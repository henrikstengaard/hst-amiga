namespace HstWbInstaller.Core.IO.Info
{
    using System.IO;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;

    public static class TextDataWriter
    {
        public static async Task Write(TextData textData, Stream stream)
        {
            if (textData.Data[textData.Size - 1] != 0)
            {
                throw new IOException("Invalid zero byte");
            }
            
            await stream.WriteBigEndianUInt32(textData.Size);
            await stream.WriteBytes(textData.Data);
        }
    }
}