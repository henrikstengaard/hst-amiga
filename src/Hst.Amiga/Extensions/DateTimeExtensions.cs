namespace Hst.Amiga.Extensions
{
    using System;

    public static class DateTimeExtensions
    {
        public static DateTime Trim(this DateTime date, long ticks)
        {
            return new DateTime(date.Ticks - date.Ticks % ticks, date.Kind);
        }
    }
}