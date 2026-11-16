namespace Hst.Amiga.Vm.Libraries.Dos;

using System;
using System.Text;

/// <summary>
/// Reads items like dos.library ReadItem() from a character source: unquoted items separated by whitespace,
/// quoted items with * escapes and equal signs.
/// </summary>
public static class ItemReader
{
    public const int ItemEqual = -2;
    public const int ItemError = -1;
    public const int ItemNothing = 0;
    public const int ItemUnquoted = 1;
    public const int ItemQuoted = 2;

    /// <summary>
    /// Read next item.
    /// </summary>
    /// <param name="read">Function reading next character, -1 at end.</param>
    /// <param name="unread">Action unreading last character read.</param>
    /// <param name="maxChars">Max characters in item buffer including zero termination.</param>
    /// <param name="item">Item read.</param>
    /// <returns>Item type: ItemEqual, ItemError, ItemNothing, ItemUnquoted or ItemQuoted.</returns>
    public static int Read(Func<int> read, Action unread, int maxChars, out string item)
    {
        item = string.Empty;
        var builder = new StringBuilder();

        int ch;
        do
        {
            ch = read();
        } while (ch == ' ' || ch == '\t');

        // end of line is left to be read again
        if (ch < 0 || ch == '\n')
        {
            if (ch >= 0)
            {
                unread();
            }

            return ItemNothing;
        }

        if (ch == '=')
        {
            return ItemEqual;
        }

        if (ch == '"')
        {
            while (true)
            {
                ch = read();
                if (ch < 0 || ch == '\n')
                {
                    return ItemError;
                }

                if (ch == '"')
                {
                    break;
                }

                if (ch == '*')
                {
                    ch = read();
                    if (ch < 0 || ch == '\n')
                    {
                        return ItemError;
                    }

                    ch = char.ToUpperInvariant((char)ch) switch
                    {
                        'N' => '\n',
                        'E' => 0x1B,
                        _ => ch
                    };
                }

                if (builder.Length >= maxChars - 1)
                {
                    return ItemError;
                }

                builder.Append((char)ch);
            }

            item = builder.ToString();
            return ItemQuoted;
        }

        while (true)
        {
            if (builder.Length >= maxChars - 1)
            {
                return ItemError;
            }

            builder.Append((char)ch);
            ch = read();
            if (ch < 0)
            {
                break;
            }

            if (ch == ' ' || ch == '\t')
            {
                break;
            }

            if (ch == '\n' || ch == '=')
            {
                unread();
                break;
            }
        }

        item = builder.ToString();
        return ItemUnquoted;
    }
}
