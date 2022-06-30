﻿namespace Hst.Amiga
{
    using System;

    public class AmigaDate
    {
        public static readonly DateTime AmigaEpocDate = new DateTime(1978, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public int Days { get; set; }
        public int Minutes { get; set; }
        public int Ticks { get; set; }
    }
}