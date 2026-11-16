namespace Hst.Amiga.Vm.Libraries.Dos;

using System;
using System.Text;

/// <summary>
/// Parses AmigaDOS shell command lines: comments, variables, backquotes, command name, redirection and arguments.
/// </summary>
public static class ShellLineParser
{
    /// <summary>
    /// Parsed command line.
    /// </summary>
    public class ParsedLine
    {
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Arguments after command name and redirection.
        /// </summary>
        public string Arguments { get; set; } = string.Empty;

        /// <summary>
        /// File redirected to input with &lt;file, if any.
        /// </summary>
        public string InputFile { get; set; }

        /// <summary>
        /// File redirected to output with &gt;file or &gt;&gt;file, if any.
        /// </summary>
        public string OutputFile { get; set; }

        /// <summary>
        /// Output is appended with &gt;&gt;file.
        /// </summary>
        public bool AppendOutput { get; set; }
    }

    /// <summary>
    /// Remove comment starting with semicolon outside quotes.
    /// </summary>
    public static string StripComment(string line)
    {
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (quoted && ch == '*' && i + 1 < line.Length)
            {
                i++;
                continue;
            }

            if (ch == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (ch == ';' && !quoted)
            {
                return line.Substring(0, i);
            }
        }

        return line;
    }

    /// <summary>
    /// Expand variables $name and ${name}. Variables, which doesn't exist, are left unchanged.
    /// *$ outside quotes is replaced by $ without expanding variable.
    /// </summary>
    /// <param name="line">Command line.</param>
    /// <param name="getVariable">Function getting value of variable or null, if variable doesn't exist.</param>
    public static string ExpandVariables(string line, Func<string, string> getVariable)
    {
        var builder = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                quoted = !quoted;
                builder.Append(ch);
                continue;
            }

            if (ch == '*' && i + 1 < line.Length)
            {
                var next = line[i + 1];
                if (next == '$')
                {
                    // escaped dollar, kept as escape in quotes for ReadArgs
                    builder.Append(quoted ? "*$" : "$");
                    i++;
                    continue;
                }

                if (quoted)
                {
                    builder.Append(ch).Append(next);
                    i++;
                    continue;
                }
            }

            if (ch != '$' || i + 1 >= line.Length)
            {
                builder.Append(ch);
                continue;
            }

            string name;
            int end;
            if (line[i + 1] == '{')
            {
                var close = line.IndexOf('}', i + 2);
                if (close < 0)
                {
                    builder.Append(ch);
                    continue;
                }

                name = line.Substring(i + 2, close - i - 2);
                end = close;
            }
            else
            {
                end = i + 1;
                while (end < line.Length && IsVariableChar(line[end]))
                {
                    end++;
                }

                name = line.Substring(i + 1, end - i - 1);
                end--;
            }

            var value = name.Length == 0 ? null : getVariable(name);
            if (value == null)
            {
                builder.Append(ch);
                continue;
            }

            builder.Append(value);
            i = end;
        }

        return builder.ToString();
    }

    private static bool IsVariableChar(char ch) => char.IsLetterOrDigit(ch) || ch == '_' || ch == '.';

    /// <summary>
    /// Replace commands in backquotes with their output.
    /// </summary>
    /// <param name="line">Command line.</param>
    /// <param name="runCommand">Function running command and returning output.</param>
    public static string SubstituteBackquotes(string line, Func<string, string> runCommand)
    {
        var start = line.IndexOf('`');
        if (start < 0)
        {
            return line;
        }

        var builder = new StringBuilder();
        var position = 0;
        while (start >= 0)
        {
            var end = line.IndexOf('`', start + 1);
            if (end < 0)
            {
                break;
            }

            builder.Append(line, position, start - position);
            var output = runCommand(line.Substring(start + 1, end - start - 1)) ?? string.Empty;
            builder.Append(output.Replace("\r", string.Empty).Replace('\n', ' ').TrimEnd());
            position = end + 1;
            start = line.IndexOf('`', position);
        }

        builder.Append(line, position, line.Length - position);
        return builder.ToString();
    }

    /// <summary>
    /// Parse command name, redirection directly after command name and arguments.
    /// </summary>
    public static ParsedLine Parse(string line)
    {
        var result = new ParsedLine();
        var position = 0;
        SkipWhitespace(line, ref position);
        result.Command = ReadToken(line, ref position);

        while (true)
        {
            var save = position;
            SkipWhitespace(line, ref position);
            if (position >= line.Length || (line[position] != '<' && line[position] != '>'))
            {
                position = save;
                break;
            }

            var isInput = line[position] == '<';
            var append = false;
            position++;
            if (!isInput && position < line.Length && line[position] == '>')
            {
                append = true;
                position++;
            }

            SkipWhitespace(line, ref position);
            var file = ReadToken(line, ref position);
            if (isInput)
            {
                result.InputFile = file;
            }
            else
            {
                result.OutputFile = file;
                result.AppendOutput = append;
            }
        }

        SkipWhitespace(line, ref position);
        result.Arguments = position < line.Length ? line.Substring(position).TrimEnd() : string.Empty;
        return result;
    }

    private static void SkipWhitespace(string line, ref int position)
    {
        while (position < line.Length && (line[position] == ' ' || line[position] == '\t'))
        {
            position++;
        }
    }

    private static string ReadToken(string line, ref int position)
    {
        if (position >= line.Length)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        if (line[position] == '"')
        {
            position++;
            while (position < line.Length && line[position] != '"')
            {
                if (line[position] == '*' && position + 1 < line.Length)
                {
                    position++;
                }

                builder.Append(line[position++]);
            }

            position = Math.Min(position + 1, line.Length);
            return builder.ToString();
        }

        while (position < line.Length && line[position] != ' ' && line[position] != '\t')
        {
            builder.Append(line[position++]);
        }

        return builder.ToString();
    }
}
