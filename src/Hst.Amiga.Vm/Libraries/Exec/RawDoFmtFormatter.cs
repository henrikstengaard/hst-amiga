namespace Hst.Amiga.Vm.Libraries.Exec;

using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Formatter for exec.library RawDoFmt() format strings with arguments read from a data stream in memory.
/// Format specifiers are %[flags][width][.limit][l]type, where flag '-' left justifies and '0' pads with zeroes,
/// 'l' reads a long argument instead of a word and type is one of b (bcpl string), c (character),
/// d (signed decimal), u (unsigned decimal), x/X (hexadecimal) and s (string).
/// </summary>
public static class RawDoFmtFormatter
{
    /// <summary>
    /// Format string with arguments.
    /// </summary>
    /// <param name="ram">Memory to read format string, data stream and strings from.</param>
    /// <param name="format">Address of zero terminated format string.</param>
    /// <param name="data">Address of data stream with arguments.</param>
    /// <param name="nextData">Address of data stream following last argument used.</param>
    /// <returns>Formatted bytes without zero termination.</returns>
    public static byte[] Format(Ram ram, uint format, uint data, out uint nextData)
    {
        var output = new List<byte>();
        var position = format;

        while (true)
        {
            var ch = ram.ReadByte(position++);
            if (ch == 0)
            {
                break;
            }

            if (ch != '%')
            {
                output.Add(ch);
                continue;
            }

            var leftJustify = false;
            var padChar = (byte)' ';
            var width = 0;
            var limit = -1;
            var isLong = false;

            ch = ram.ReadByte(position++);
            if (ch == '-')
            {
                leftJustify = true;
                ch = ram.ReadByte(position++);
            }

            if (ch == '0')
            {
                padChar = (byte)'0';
                ch = ram.ReadByte(position++);
            }

            while (ch is >= (byte)'0' and <= (byte)'9')
            {
                width = width * 10 + (ch - '0');
                ch = ram.ReadByte(position++);
            }

            if (ch == '.')
            {
                limit = 0;
                ch = ram.ReadByte(position++);
                while (ch is >= (byte)'0' and <= (byte)'9')
                {
                    limit = limit * 10 + (ch - '0');
                    ch = ram.ReadByte(position++);
                }
            }

            if (ch == 'l' || ch == 'L')
            {
                isLong = true;
                ch = ram.ReadByte(position++);
            }

            byte[] field;
            switch ((char)ch)
            {
                case 'd':
                case 'D':
                {
                    var value = isLong ? (int)ReadLong(ram, ref data) : (short)ReadWord(ram, ref data);
                    field = Ram.GetBytes(value.ToString(CultureInfo.InvariantCulture));
                    break;
                }
                case 'u':
                case 'U':
                {
                    var value = isLong ? ReadLong(ram, ref data) : ReadWord(ram, ref data);
                    field = Ram.GetBytes(value.ToString(CultureInfo.InvariantCulture));
                    break;
                }
                case 'x':
                {
                    var value = isLong ? ReadLong(ram, ref data) : ReadWord(ram, ref data);
                    field = Ram.GetBytes(value.ToString("x", CultureInfo.InvariantCulture));
                    break;
                }
                case 'X':
                {
                    var value = isLong ? ReadLong(ram, ref data) : ReadWord(ram, ref data);
                    field = Ram.GetBytes(value.ToString("X", CultureInfo.InvariantCulture));
                    break;
                }
                case 'c':
                case 'C':
                {
                    var value = isLong ? ReadLong(ram, ref data) : ReadWord(ram, ref data);
                    field = new[] { (byte)value };
                    break;
                }
                case 's':
                case 'S':
                {
                    var address = ReadLong(ram, ref data);
                    field = Ram.GetBytes(address == 0 ? string.Empty : ram.ReadCString(address));
                    break;
                }
                case 'b':
                case 'B':
                {
                    var bptr = ReadLong(ram, ref data);
                    field = Ram.GetBytes(bptr == 0 ? string.Empty : ram.ReadBString(Library.BAddr(bptr)));
                    break;
                }
                case '\0':
                    nextData = data;
                    return output.ToArray();
                default:
                    // unknown format type is output as is
                    field = new[] { ch };
                    padChar = (byte)' ';
                    break;
            }

            if (limit >= 0 && field.Length > limit && (ch == 's' || ch == 'S' || ch == 'b' || ch == 'B'))
            {
                Array.Resize(ref field, limit);
            }

            var padding = Math.Max(0, width - field.Length);
            if (leftJustify)
            {
                output.AddRange(field);
                for (var i = 0; i < padding; i++)
                {
                    output.Add((byte)' ');
                }
            }
            else if (padChar == '0' && field.Length > 0 && field[0] == '-')
            {
                // zero padding after sign
                output.Add((byte)'-');
                for (var i = 0; i < padding; i++)
                {
                    output.Add(padChar);
                }

                for (var i = 1; i < field.Length; i++)
                {
                    output.Add(field[i]);
                }
            }
            else
            {
                for (var i = 0; i < padding; i++)
                {
                    output.Add(padChar);
                }

                output.AddRange(field);
            }
        }

        nextData = data;
        return output.ToArray();
    }

    private static uint ReadWord(Ram ram, ref uint data)
    {
        var value = ram.ReadWord(data);
        data += 2;
        return value;
    }

    private static uint ReadLong(Ram ram, ref uint data)
    {
        var value = ram.ReadLong(data);
        data += 4;
        return value;
    }
}
