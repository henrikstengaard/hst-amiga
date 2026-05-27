namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;

    public static class ToolTypesReader
    {
        public static async Task<ToolTypes> Read(Stream stream, bool allowErrors = false)
        {
            if (allowErrors && stream.Position > stream.Length - 3)
            {
                return new ToolTypes();
            }
            
            var entries = await stream.ReadBigEndianUInt32();
            var count = (entries / 4) - 1;

            var textDatas = new List<TextData>();
            for (var i = 0; i < count; i++)
            {
                textDatas.Add(await TextDataReader.Read(stream, allowErrors));
            }

            return new ToolTypes
            {
                TextDatas = textDatas
            };
        }
    }
}