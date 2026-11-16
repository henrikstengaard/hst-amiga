namespace Hst.Amiga.Vm.Libraries.Dos;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// AmigaDOS wildcard pattern compiled to regular expression. Supports ? (any character), # (zero or more of
/// following item), #? (anything), (a|b) (alternatives), ~ (negation), [a-z] (character class), % (empty)
/// and ' (escape following character).
/// </summary>
public class AmigaPattern
{
    private readonly Regex regex;

    public AmigaPattern(string pattern, bool ignoreCase)
    {
        Pattern = pattern ?? string.Empty;
        var position = 0;
        var expression = ParseSequence(Pattern, ref position, false);
        regex = new Regex($"^(?:{expression})$",
            RegexOptions.Singleline | RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : 0));
    }

    public string Pattern { get; }

    public bool IsMatch(string value) => regex.IsMatch(value ?? string.Empty);

    /// <summary>
    /// Pattern contains wildcard characters.
    /// </summary>
    public static bool IsWild(string pattern)
    {
        if (pattern == null)
        {
            return false;
        }

        for (var i = 0; i < pattern.Length; i++)
        {
            switch (pattern[i])
            {
                case '\'':
                    i++;
                    break;
                case '?':
                case '#':
                case '(':
                case '|':
                case '~':
                case '[':
                case '%':
                    return true;
            }
        }

        return false;
    }

    private static string ParseSequence(string pattern, ref int position, bool inGroup)
    {
        var builder = new StringBuilder();
        while (position < pattern.Length)
        {
            var ch = pattern[position];
            if (inGroup && (ch == '|' || ch == ')'))
            {
                break;
            }

            builder.Append(ParseItem(pattern, ref position));
        }

        return builder.ToString();
    }

    private static string ParseItem(string pattern, ref int position)
    {
        var ch = pattern[position++];
        switch (ch)
        {
            case '?':
                return ".";
            case '%':
                return string.Empty;
            case '#':
                if (position >= pattern.Length)
                {
                    return Regex.Escape("#");
                }

                return $"(?:{ParseItem(pattern, ref position)})*";
            case '~':
                if (position >= pattern.Length)
                {
                    return Regex.Escape("~");
                }

                // negation matches anything not matching the remaining pattern item
                var negated = ParseItem(pattern, ref position);
                return $"(?:(?!(?:{negated})$).*)";
            case '(':
            {
                var alternatives = new StringBuilder();
                while (true)
                {
                    alternatives.Append(ParseSequence(pattern, ref position, true));
                    if (position >= pattern.Length)
                    {
                        break;
                    }

                    var separator = pattern[position++];
                    if (separator == ')')
                    {
                        break;
                    }

                    alternatives.Append('|');
                }

                return $"(?:{alternatives})";
            }
            case '[':
            {
                var end = pattern.IndexOf(']', position);
                if (end < 0)
                {
                    return Regex.Escape("[");
                }

                var characterClass = pattern.Substring(position, end - position);
                position = end + 1;
                var negate = characterClass.StartsWith("~");
                if (negate)
                {
                    characterClass = characterClass.Substring(1);
                }

                var escaped = new StringBuilder();
                foreach (var c in characterClass)
                {
                    escaped.Append(c is '\\' or ']' or '[' or '^' ? $"\\{c}" : c.ToString());
                }

                return $"[{(negate ? "^" : string.Empty)}{escaped}]";
            }
            case '\'':
                if (position >= pattern.Length)
                {
                    return Regex.Escape("'");
                }

                return Regex.Escape(pattern[position++].ToString());
            default:
                return Regex.Escape(ch.ToString());
        }
    }
}
