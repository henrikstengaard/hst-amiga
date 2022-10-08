namespace Hst.Amiga
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Core.Converters;
    using Core.Extensions;

    public static class DateHelper
    {
        private const int TicksPerSecond = 50; // pal amiga has 50 ticks per second

        /// <summary>
        /// Convert days, minutes and ticks to date
        /// </summary>
        /// <param name="days">days since 1 jan 78</param>
        /// <param name="minutes">minutes past midnight</param>
        /// <param name="ticks">ticks (1/50 sec) past last minute</param>
        /// <returns></returns>
        public static DateTime ConvertToDate(int days, int minutes, int ticks)
        {
            var seconds = ticks / TicksPerSecond;
            var milliseconds = (double)ticks % TicksPerSecond / 50 * 1000;
            return AmigaDate.AmigaEpocDate.AddDays(days).AddMinutes(minutes).AddSeconds(seconds)
                .AddMilliseconds(milliseconds);
        }

        /// <summary>
        /// Convert date to amiga date
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static AmigaDate ConvertToAmigaDate(DateTime date)
        {
            if (date == DateTime.MinValue || date < AmigaDate.AmigaEpocDate)
            {
                return new AmigaDate
                {
                    Days = 0,
                    Minutes = 0,
                    Ticks = 0
                };
            }
            
            var diffDate = date - AmigaDate.AmigaEpocDate;
            var days = diffDate.Days;
            var minutes = diffDate.Hours * 60 + diffDate.Minutes;
            var ticksSeconds = diffDate.Seconds * TicksPerSecond;
            var ticksMilliseconds = diffDate.Milliseconds == 0 ? 0 : Convert.ToInt32(((double)diffDate.Milliseconds / 1000) * TicksPerSecond);
            
            return new AmigaDate
            {
                Days = days,
                Minutes = minutes,
                Ticks = ticksSeconds + ticksMilliseconds
            };
        }

        public static DateTime ReadDate(byte[] bytes, int offset)
        {
            var days = BigEndianConverter.ConvertBytesToUInt32(bytes, offset); // days since 1 jan 78
            var minutes = BigEndianConverter.ConvertBytesToUInt32(bytes, offset + SizeOf.Long); // minutes past midnight
            var ticks = BigEndianConverter.ConvertBytesToUInt32(bytes,
                offset + (SizeOf.Long * 2)); // ticks (1/50 sec) past last minute

            return ConvertToDate((int)days, (int)minutes, (int)ticks);
        }

        public static async Task<DateTime> ReadDate(Stream stream)
        {
            var days = await stream.ReadBigEndianUInt32(); // days since 1 jan 78
            var minutes = await stream.ReadBigEndianUInt32(); // minutes past midnight
            var ticks = await stream.ReadBigEndianUInt32(); // ticks (1/50 sec) past last minute

            return ConvertToDate((int)days, (int)minutes, (int)ticks);
        }

        public static void WriteDate(byte[] bytes, int offset, DateTime date)
        {
            if (date == DateTime.MinValue)
            {
                BigEndianConverter.ConvertUInt32ToBytes(0, bytes, offset + 0); // days since 1 jan 78
                BigEndianConverter.ConvertUInt32ToBytes(0, bytes, offset + 4); // minutes past midnight
                BigEndianConverter.ConvertUInt32ToBytes(0, bytes, offset + 8); // ticks (1/50 sec) past last minute
                return;
            }

            var amigaDate = ConvertToAmigaDate(date);

            BigEndianConverter.ConvertUInt32ToBytes((uint)amigaDate.Days, bytes, offset + 0); // days since 1 jan 78
            BigEndianConverter.ConvertUInt32ToBytes((uint)amigaDate.Minutes, bytes,
                offset + 4); // minutes past midnight
            BigEndianConverter.ConvertUInt32ToBytes((uint)amigaDate.Ticks, bytes,
                offset + 8); // ticks (1/50 sec) past last minute
        }

        public static async Task WriteDate(Stream stream, DateTime date)
        {
            var bytes = new byte[12];
            WriteDate(bytes, 0, date);
            await stream.WriteBytes(bytes);
        }
    }
}