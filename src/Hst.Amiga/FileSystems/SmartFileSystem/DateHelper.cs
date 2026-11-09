namespace Hst.Amiga.FileSystems.SmartFileSystem
{
    using System;

    /// <summary>
    /// Date helper for smart file system dates stored as seconds since 1978-01-01 00:00:00.
    /// </summary>
    public static class DateHelper
    {
        public static DateTime ConvertToDate(uint seconds)
        {
            var date = AmigaDate.AmigaEpocDate.AddSeconds(seconds);
            return new DateTime(date.Ticks, DateTimeKind.Unspecified);
        }

        public static uint ConvertFromDate(DateTime date)
        {
            var epoch = new DateTime(AmigaDate.AmigaEpocDate.Ticks, DateTimeKind.Unspecified);
            var unspecifiedDate = new DateTime(date.Ticks, DateTimeKind.Unspecified);
            if (unspecifiedDate <= epoch)
            {
                return 0;
            }

            var seconds = Math.Floor((unspecifiedDate - epoch).TotalSeconds);
            return seconds >= uint.MaxValue ? uint.MaxValue : (uint)seconds;
        }
    }
}
