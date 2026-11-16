namespace Hst.Amiga.Vm.Libraries.Dos;

using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Formats BCPL style format strings used by dos.library VFWritef(). Each argument is a long word in argument
/// array. Width x is a single character with value x - '0'.
/// <list type="bullet">
/// <item>%S - C string.</item>
/// <item>%Tx - BCPL string left justified and padded with spaces to at least x characters.</item>
/// <item>%C - character.</item>
/// <item>%Ox, %Xx, %Ix, %Ux - octal, hexadecimal, decimal and unsigned decimal number, max x characters.</item>
/// <item>%N - decimal number of any length.</item>
/// <item>%$ - ignore argument.</item>
/// </list>
/// </summary>
public static class WritefFormatter
{
    /// <summary>
    /// Format string with arguments.
    /// </summary>
    /// <param name="ram">Ram with format string and arguments.</param>
    /// <param name="format">Address of zero terminated format string.</param>
    /// <param name="arguments">Address of argument array with long words.</param>
    /// <returns>Formatted bytes.</returns>
    public static byte[] Format(Ram ram, uint format, uint arguments)
    {
        var output = new List<byte>();
        var position = format;
        var argument = arguments;

        uint NextArgument()
        {
            var value = ram.ReadLong(argument);
            argument += 4;
            return value;
        }

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

            var code = char.ToUpperInvariant((char)ram.ReadByte(position++));
            switch (code)
            {
                case '\0':
                    return output.ToArray();
                case 'S':
                {
                    var address = NextArgument();
                    if (address != 0)
                    {
                        output.AddRange(Ram.GetBytes(ram.ReadCString(address)));
                    }

                    break;
                }
                case 'T':
                {
                    var width = ReadWidth(ram, ref position);
                    var bptr = NextArgument();
                    var bytes = bptr == 0 ? Array.Empty<byte>() : Ram.GetBytes(ram.ReadBString(Library.BAddr(bptr)));
                    output.AddRange(bytes);
                    for (var i = bytes.Length; i < width; i++)
                    {
                        output.Add((byte)' ');
                    }

                    break;
                }
                case 'C':
                    output.Add((byte)NextArgument());
                    break;
                case 'O':
                    AddLimited(output, Convert.ToString(NextArgument(), 8), ReadWidth(ram, ref position));
                    break;
                case 'X':
                    AddLimited(output, NextArgument().ToString("X", CultureInfo.InvariantCulture),
                        ReadWidth(ram, ref position));
                    break;
                case 'I':
                    AddLimited(output, ((int)NextArgument()).ToString(CultureInfo.InvariantCulture),
                        ReadWidth(ram, ref position));
                    break;
                case 'U':
                    AddLimited(output, NextArgument().ToString(CultureInfo.InvariantCulture),
                        ReadWidth(ram, ref position));
                    break;
                case 'N':
                    output.AddRange(Ram.GetBytes(((int)NextArgument()).ToString(CultureInfo.InvariantCulture)));
                    break;
                case '$':
                    NextArgument();
                    break;
                default:
                    // unknown code is written as is
                    output.Add((byte)code);
                    break;
            }
        }

        return output.ToArray();
    }

    private static int ReadWidth(Ram ram, ref uint position)
    {
        var ch = ram.ReadByte(position);
        if (ch == 0)
        {
            return 0;
        }

        position++;
        return ch - '0';
    }

    /// <summary>
    /// Add number with at most width characters keeping most significant digits like AmigaDOS.
    /// </summary>
    private static void AddLimited(List<byte> output, string number, int width)
    {
        for (var i = 0; i < number.Length && i < width; i++)
        {
            output.Add((byte)number[i]);
        }
    }
}
