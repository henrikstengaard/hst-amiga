using System;
using System.Text;
using Hst.Core.Converters;

namespace Hst.Amiga.DataTypes.UaeFsDbs
{
    public static class UaeFsDbReader
    {
        public static UaeFsDbNode Read(byte[] data, int offset = 0, UaeFsDbNode.NodeVersion version = UaeFsDbNode.NodeVersion.Version1)
        {
            if (data.Length < Constants.UaeFsDbNodeVersion1Size)
            {
                throw new ArgumentException($"Data bytes {data.Length} is less than UAEFSDB node v1 size of {Constants.UaeFsDbNodeVersion1Size} bytes", nameof(data));
            }
            
            if (offset + Constants.UaeFsDbNodeVersion1Size > data.Length)
            {
                throw new ArgumentOutOfRangeException($"Data bytes {data.Length} doesn't fit a UAEFSDB node v1 size of {Constants.UaeFsDbNodeVersion1Size} bytes reading from offset {offset}", nameof(data));
            }

            var valid = data[offset];
            var mode = BigEndianConverter.ConvertBytesToUInt32(data, offset + 0x1);
            var amigaName = AmigaTextHelper.GetNullTerminatedString(data, offset + 0x5, 256);
            var normalName = TextHelper.ReadNullTerminatedString(Encoding.UTF8, data, offset + 0x106, 257);
            var comment = TextHelper.ReadNullTerminatedString(Encoding.UTF8, data, offset + 0x207, 81);

            if (version != UaeFsDbNode.NodeVersion.Version2)
            {
                return new UaeFsDbNode
                {
                    Version = version,
                    Valid = valid,
                    Mode = mode,
                    AmigaName = amigaName,
                    NormalName = normalName,
                    Comment = comment,
                    WinMode = 0,
                    AmigaNameUnicode = string.Empty,
                    NormalNameUnicode = string.Empty
                };
            }

            if (offset + Constants.UaeFsDbNodeVersion2Size > data.Length)
            {
                throw new ArgumentOutOfRangeException($"Data bytes {data.Length} doesn't fit a UAEFSDB node v2 size of {Constants.UaeFsDbNodeVersion2Size} bytes reading from offset {offset}", nameof(data)); 
            }
            
            var winMode = BigEndianConverter.ConvertBytesToUInt32(data, offset + 0x258);
            var amigaNameUnicode = TextHelper.ReadNullTerminatedString(Encoding.Unicode, data, offset + 0x25c, 514);
            var normalNameUnicode = TextHelper.ReadNullTerminatedString(Encoding.Unicode, data, offset + 0x45e, 514);

            return new UaeFsDbNode
            {
                Version = version,
                Mode = mode,
                AmigaName = amigaName,
                NormalName = normalName,
                Comment = comment,
                WinMode = winMode,
                AmigaNameUnicode = amigaNameUnicode,
                NormalNameUnicode = normalNameUnicode 
            };
        }
    }
}