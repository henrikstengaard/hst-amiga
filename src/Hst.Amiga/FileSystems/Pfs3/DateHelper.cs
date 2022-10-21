namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Core.Converters;
    using Core.Extensions;

    public static class DateHelper
    {
        public static async Task<DateTime> ReadDate(Stream stream)
        {
            var dateBytes = await stream.ReadBytes(6);
            if (dateBytes.Length != 6)
            {
                throw new IOException("Invalid date");
            }
            return ReadDate(dateBytes, 0);
        }

        public static DateTime ReadDate(byte[] bytes, int offset)
        {
            var days = BigEndianConverter.ConvertBytesToUInt16(bytes, offset); // days since 1 jan 78
            var minutes = BigEndianConverter.ConvertBytesToUInt16(bytes, offset + 2); // minutes past midnight
            var ticks = BigEndianConverter.ConvertBytesToUInt16(bytes, offset + 4); // ticks (1/50 sec) past last minute

            return Amiga.DateHelper.ConvertToDate(days, minutes, ticks);
        }

        public static async Task WriteDate(Stream stream, DateTime date)
        {
            var dateBytes = new byte[6];
            WriteDate(date, dateBytes, 0);
            await stream.WriteBytes(dateBytes);
        }
        
        public static void WriteDate(DateTime date, byte[] data, int offset)
        {
            var amigaDate = Amiga.DateHelper.ConvertToAmigaDate(date);
            
            BigEndianConverter.ConvertUInt16ToBytes((ushort)amigaDate.Days, data, offset + 0); // days since 1 jan 78
            BigEndianConverter.ConvertUInt16ToBytes((ushort)amigaDate.Minutes, data, offset + 2); // minutes past midnight
            BigEndianConverter.ConvertUInt16ToBytes((ushort)amigaDate.Ticks, data, offset + 4); // ticks (1/50 sec) past last minute
        }
    }
}