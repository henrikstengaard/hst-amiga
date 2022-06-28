namespace Hst.Amiga
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Core.Extensions;

    public static class DateHelper
    {
        public static DateTime ConvertToDate(int days, int minutes, int ticks)
        {
            return AmigaDate.AmigaEpocDate.AddDays(days).AddMinutes(minutes).AddMilliseconds(ticks);
        }

        public static AmigaDate ConvertToAmigaDate(DateTime date)
        {
            var diffDate = date - AmigaDate.AmigaEpocDate;
            var days = diffDate.Days;
            var minutes = diffDate.Hours * 60 + diffDate.Minutes;
            var ticks = Convert.ToInt32(diffDate.Milliseconds);

            return new AmigaDate
            {
                Days = days,
                Minutes = minutes,
                Ticks = ticks
            };
        }
        
        public static async Task<DateTime> ReadDate(Stream stream)
        {
            var days = await stream.ReadBigEndianUInt32(); // days since 1 jan 78
            var minutes = await stream.ReadBigEndianUInt32(); // minutes past midnight
            var ticks = await stream.ReadBigEndianUInt32(); // ticks (1/50 sec) past last minute

            return ConvertToDate((int)days, (int)minutes, (int)ticks);
        }
        
        public static async Task WriteDate(Stream stream, DateTime date)
        {
            if (date == DateTime.MinValue)
            {
                await stream.WriteBigEndianUInt32(0); // days since 1 jan 78
                await stream.WriteBigEndianUInt32(0); // minutes past midnight
                await stream.WriteBigEndianUInt32(0); // ticks (1/50 sec) past last minute
                return;
            }

            var amigaDate = ConvertToAmigaDate(date);
            
            await stream.WriteBigEndianUInt32((uint)amigaDate.Days); // days since 1 jan 78
            await stream.WriteBigEndianUInt32((uint)amigaDate.Minutes); // minutes past midnight
            await stream.WriteBigEndianUInt32((uint)amigaDate.Ticks); // ticks (1/50 sec) past last minute
        }
    }
}