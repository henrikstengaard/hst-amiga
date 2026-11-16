namespace Hst.Amiga.Vm.Libraries.Dos;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// Parser for AmigaDOS ReadArgs() templates and argument lines. Template items are separated by comma and
/// consist of names separated by equal sign followed by modifiers /A required, /K keyword, /S switch, /T toggle,
/// /N number, /M multiple and /F rest of line.
/// </summary>
public class ReadArgsParser
{
    public class TemplateItem
    {
        public string[] Names { get; set; }
        public bool Required { get; set; }
        public bool Keyword { get; set; }
        public bool Switch { get; set; }
        public bool Toggle { get; set; }
        public bool Number { get; set; }
        public bool Multiple { get; set; }
        public bool RestOfLine { get; set; }

        public bool IsPositional => !Keyword && !Switch && !Toggle;

        public bool Matches(string name) =>
            Names.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
    }

    public class Result
    {
        /// <summary>
        /// Dos error code or 0, if arguments were parsed.
        /// </summary>
        public int Error { get; set; }

        /// <summary>
        /// Values per template item, null if not given. Values are string, bool (switch and toggle),
        /// int (number) or list of strings (multiple).
        /// </summary>
        public object[] Values { get; set; }
    }

    public static IList<TemplateItem> ParseTemplate(string template)
    {
        var items = new List<TemplateItem>();
        foreach (var part in (template ?? string.Empty).Split(','))
        {
            var tokens = part.Split('/');
            var names = tokens[0].Split('=').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            var item = new TemplateItem { Names = names };
            foreach (var modifier in tokens.Skip(1).Select(x => x.Trim().ToUpperInvariant()))
            {
                switch (modifier)
                {
                    case "A":
                        item.Required = true;
                        break;
                    case "K":
                        item.Keyword = true;
                        break;
                    case "S":
                        item.Switch = true;
                        break;
                    case "T":
                        item.Toggle = true;
                        break;
                    case "N":
                        item.Number = true;
                        break;
                    case "M":
                        item.Multiple = true;
                        break;
                    case "F":
                        item.RestOfLine = true;
                        break;
                }
            }

            items.Add(item);
        }

        return items;
    }

    public static Result Parse(string template, string arguments) => Parse(ParseTemplate(template), arguments);

    public static Result Parse(IList<TemplateItem> items, string arguments)
    {
        var values = new object[items.Count];
        var reader = new ItemReader(arguments ?? string.Empty);
        var multipleIndex = items.ToList().FindIndex(x => x.Multiple);

        while (true)
        {
            var item = reader.ReadItem(out var quoted, out var hasEquals, out var error);
            if (error != 0)
            {
                return Fail(error);
            }

            if (item == null)
            {
                break;
            }

            var keywordIndex = quoted ? -1 : FindIndex(items, x => x.Matches(item));
            if (keywordIndex >= 0)
            {
                var templateItem = items[keywordIndex];
                if (templateItem.Switch)
                {
                    values[keywordIndex] = true;
                    continue;
                }

                if (templateItem.Toggle)
                {
                    values[keywordIndex] = true;
                    continue;
                }

                string value;
                if (templateItem.RestOfLine)
                {
                    value = reader.ReadRestOfLine();
                }
                else
                {
                    value = reader.ReadItem(out _, out _, out error);
                    if (error != 0)
                    {
                        return Fail(error);
                    }
                }

                if (value == null)
                {
                    return Fail(DosErrors.KeyNeedsArg);
                }

                error = SetValue(templateItem, values, keywordIndex, value);
                if (error != 0)
                {
                    return Fail(error);
                }

                continue;
            }

            if (hasEquals)
            {
                // equal sign after unknown keyword is part of value
                var rest = reader.ReadItem(out _, out _, out error);
                item = string.Concat(item, "=", rest ?? string.Empty);
            }

            // positional argument fills first unfilled positional item
            var index = FindIndex(items, x => x.IsPositional && !x.Multiple && values[items.IndexOf(x)] == null);
            if (multipleIndex >= 0 && (index < 0 || multipleIndex < index))
            {
                index = multipleIndex;
            }

            if (index < 0)
            {
                return Fail(DosErrors.TooManyArgs);
            }

            var positionalItem = items[index];
            if (positionalItem.RestOfLine)
            {
                var rest = reader.ReadRestOfLine();
                item = string.IsNullOrEmpty(rest) ? item : string.Concat(item, " ", rest);
            }

            error = SetValue(positionalItem, values, index, item);
            if (error != 0)
            {
                return Fail(error);
            }
        }

        // required positional items after multiple item steal values from end of multiple
        if (multipleIndex >= 0 && values[multipleIndex] is List<string> multiple)
        {
            for (var i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                if (!item.Required || !item.IsPositional || item.Multiple || values[i] != null)
                {
                    continue;
                }

                var keep = items[multipleIndex].Required ? 1 : 0;
                if (multiple.Count <= keep)
                {
                    break;
                }

                var value = multiple[multiple.Count - 1];
                multiple.RemoveAt(multiple.Count - 1);
                var error = SetValue(item, values, i, value);
                if (error != 0)
                {
                    return Fail(error);
                }
            }

            if (multiple.Count == 0)
            {
                values[multipleIndex] = null;
            }
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Required && values[i] == null)
            {
                return Fail(DosErrors.RequiredArgMissing);
            }
        }

        return new Result { Values = values };
    }

    private static int FindIndex(IList<TemplateItem> items, Func<TemplateItem, bool> predicate)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (predicate(items[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static int SetValue(TemplateItem item, object[] values, int index, string value)
    {
        if (item.Multiple)
        {
            if (values[index] is not List<string> list)
            {
                list = new List<string>();
                values[index] = list;
            }

            list.Add(value);
            return 0;
        }

        if (item.Number)
        {
            if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
            {
                return DosErrors.BadNumber;
            }

            values[index] = number;
            return 0;
        }

        values[index] = value;
        return 0;
    }

    private static Result Fail(int error) => new() { Error = error };

    /// <summary>
    /// Reads items separated by whitespace, quoted items with * escapes and keyword=value pairs.
    /// </summary>
    private class ItemReader
    {
        private readonly string input;
        private int position;

        public ItemReader(string input)
        {
            this.input = input;
        }

        private bool AtEnd => position >= input.Length || input[position] == '\n';

        private void SkipWhitespace()
        {
            while (position < input.Length && (input[position] == ' ' || input[position] == '\t'))
            {
                position++;
            }
        }

        public string ReadItem(out bool quoted, out bool hasEquals, out int error)
        {
            quoted = false;
            hasEquals = false;
            error = 0;
            SkipWhitespace();

            // skip equal sign between keyword and value
            if (!AtEnd && input[position] == '=')
            {
                position++;
                SkipWhitespace();
            }

            if (AtEnd)
            {
                return null;
            }

            var builder = new StringBuilder();
            if (input[position] == '"')
            {
                quoted = true;
                position++;
                while (true)
                {
                    if (AtEnd)
                    {
                        error = DosErrors.UnmatchedQuotes;
                        return null;
                    }

                    var ch = input[position++];
                    if (ch == '"')
                    {
                        break;
                    }

                    if (ch == '*' && !AtEnd)
                    {
                        var escaped = input[position++];
                        builder.Append(char.ToUpperInvariant(escaped) switch
                        {
                            'N' => '\n',
                            'E' => '\x1B',
                            _ => escaped
                        });
                        continue;
                    }

                    builder.Append(ch);
                }

                return builder.ToString();
            }

            while (!AtEnd && input[position] != ' ' && input[position] != '\t' && input[position] != '=')
            {
                builder.Append(input[position++]);
            }

            if (!AtEnd && input[position] == '=')
            {
                hasEquals = true;
            }

            return builder.ToString();
        }

        public string ReadRestOfLine()
        {
            SkipWhitespace();
            if (!AtEnd && input[position] == '=')
            {
                position++;
                SkipWhitespace();
            }

            var start = position;
            while (!AtEnd)
            {
                position++;
            }

            var rest = input.Substring(start, position - start).TrimEnd();
            if (rest.Length >= 2 && rest[0] == '"' && rest[rest.Length - 1] == '"')
            {
                rest = rest.Substring(1, rest.Length - 2);
            }

            return rest.Length == 0 ? null : rest;
        }
    }
}
