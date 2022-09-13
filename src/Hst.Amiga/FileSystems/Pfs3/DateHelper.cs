namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Core.Extensions;

    public static class DateHelper
    {
        public static async Task<DateTime> ReadDate(Stream stream)
        {
            var amigaDate = new DateTime(1978, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            
            var days = await stream.ReadBigEndianUInt16(); // days since 1 jan 78
            var minutes = await stream.ReadBigEndianUInt16(); // minutes past midnight
            var ticks = await stream.ReadBigEndianUInt16(); // ticks (1/50 sec) past last minute
            return amigaDate.AddDays(days).AddMinutes(minutes).AddSeconds(60 / 50 * ticks);
        }

        public static async Task WriteDate(Stream stream, DateTime date)
        {
            if (date == DateTime.MinValue)
            {
                await stream.WriteBigEndianUInt16(0); // days since 1 jan 78
                await stream.WriteBigEndianUInt16(0); // minutes past midnight
                await stream.WriteBigEndianUInt16(0); // ticks (1/50 sec) past last minute
                return;
            }
            
            var amigaDate = new DateTime(1978, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var diffDate = date - amigaDate;
            var days = (ushort)diffDate.Days;
            var minutes = (ushort)(diffDate.Hours * 60 + diffDate.Minutes);
            var ticks = Convert.ToUInt16((double)50 / 60 * diffDate.Seconds);
            
            await stream.WriteBigEndianUInt16(days); // days since 1 jan 78
            await stream.WriteBigEndianUInt16(minutes); // minutes past midnight
            await stream.WriteBigEndianUInt16(ticks); // ticks (1/50 sec) past last minute
        }
    }
}