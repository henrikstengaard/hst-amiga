﻿using System;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Hst.Amiga.DataTypes.UaeMetafiles
{
    public class UaeMetafileReader
    {
        public static UaeMetafile Read(byte[] data)
        {
            if (data.Length <= 8)
            {
                throw new ArgumentException($"Data bytes {data.Length} doesn't contain protection bits", nameof(data));
            }

            var protectionBits = Encoding.ASCII.GetString(data, 0, 8);
            
            if (data.Length < 30)
            {
                throw new ArgumentException($"Data bytes {data.Length} doesn't contain date", nameof(data));
            }
            
            var date = Encoding.ASCII.GetString(data, 9, 22);
            if (!DateTime.TryParseExact(date, "yyyy-MM-dd HH:mm:ss.ff",
                    Thread.CurrentThread.CurrentCulture, DateTimeStyles.AssumeLocal, out var parsedDate))
            {
                throw new ArgumentException($"Date '{date}' is invalid format", nameof(data));
            }
            
            var comment = data.Length > 31 && data.Length > 33
                ? Encoding.UTF8.GetString(data, 32, data.Length - 32 - (data[data.Length - 1] == '\n' ? 1 : 0))
                : string.Empty;

            return new UaeMetafile
            {
                ProtectionBits = protectionBits,
                Date = parsedDate,
                Comment = comment
            };
        }
    }
}