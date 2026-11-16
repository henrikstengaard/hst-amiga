namespace Hst.Amiga.Vm.Libraries.Dos;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Structs;

/// <summary>
/// Shell internal commands built into the AmigaOS 3.1 shell, which are not files in C:.
/// </summary>
public partial class DosLibrary
{
    private delegate int BuiltinCommand(string arguments);

    private Dictionary<string, BuiltinCommand> builtinCommands;

    private Dictionary<string, BuiltinCommand> BuiltinCommands => builtinCommands ??=
        new Dictionary<string, BuiltinCommand>(StringComparer.OrdinalIgnoreCase)
        {
            ["Alias"] = AliasCommand,
            ["Ask"] = AskCommand,
            ["CD"] = CdCommand,
            ["Echo"] = EchoCommand,
            ["Else"] = ElseCommand,
            ["EndCLI"] = EndShellCommand,
            ["EndIf"] = _ => 0,
            ["EndShell"] = EndShellCommand,
            ["EndSkip"] = _ => 0,
            ["FailAt"] = FailAtCommand,
            ["Fault"] = FaultCommand,
            ["Get"] = GetCommand,
            ["GetEnv"] = GetEnvCommand,
            ["If"] = IfCommand,
            ["Lab"] = _ => 0,
            ["NewCLI"] = NewShellCommand,
            ["NewShell"] = NewShellCommand,
            ["Path"] = PathCommand,
            ["Prompt"] = PromptCommand,
            ["Quit"] = QuitCommand,
            ["Resident"] = ResidentCommand,
            ["Run"] = RunBuiltinCommand,
            ["Set"] = SetCommand,
            ["SetEnv"] = SetEnvCommand,
            ["Skip"] = SkipCommand,
            ["Stack"] = StackCommand,
            ["UnAlias"] = UnAliasCommand,
            ["UnSet"] = UnSetCommand,
            ["UnSetEnv"] = UnSetEnvCommand,
            ["Why"] = WhyCommand
        };

    /// <summary>
    /// Names of shell internal commands.
    /// </summary>
    public IEnumerable<string> BuiltinCommandNames => BuiltinCommands.Keys;

    private bool TryRunBuiltinCommand(string command, string arguments, out int returnCode)
    {
        if (!BuiltinCommands.TryGetValue(command, out var builtin))
        {
            returnCode = 0;
            return false;
        }

        IoErrValue = 0;
        returnCode = builtin(arguments);
        return true;
    }

    private void Print(string text) => WriteString(OutputHandle, text);

    /// <summary>
    /// Parse arguments with template, printing error like ReadArgs() failing in a command.
    /// </summary>
    private object[] ParseArguments(string command, string template, string arguments)
    {
        var result = ReadArgsParser.Parse(template, (arguments ?? string.Empty) + "\n");
        if (result.Error == 0)
        {
            return result.Values;
        }

        SetIoErr(result.Error);
        Print($"{command}: {DosErrors.GetMessage(result.Error)}\n");
        return null;
    }

    private static string Text(object value) => value switch
    {
        null => null,
        string text => text,
        List<string> list => string.Join(" ", list),
        _ => value.ToString()
    };

    private static int? Number(object value) =>
        value switch
        {
            int number => number,
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            _ => null
        };

    private int EchoCommand(string arguments)
    {
        var args = ParseArguments("Echo", "/M,NOLINE/S,FIRST/K/N,LEN/K/N", arguments);
        if (args == null)
        {
            return ReturnError;
        }

        var strings = args[0] as List<string> ?? new List<string>();
        var first = Number(args[2]);
        var max = Number(args[3]);
        var parts = strings.Select(text => EchoSubstring(text, first, max));
        var output = string.Join(" ", parts);
        Print(args[1] is true ? output : output + "\n");
        return 0;
    }

    /// <summary>
    /// Get substring echoed like original Echo. First beyond end of string echoes last character and max
    /// without first echoes characters from end of string.
    /// </summary>
    private static string EchoSubstring(string text, int? first, int? max)
    {
        if (text.Length == 0)
        {
            return text;
        }

        var start = 0;
        if (first is > 0)
        {
            start = first.Value - 1 < text.Length ? first.Value - 1 : text.Length - 1;
        }
        else if (max.HasValue && text.Length > max.Value)
        {
            start = text.Length - Math.Max(0, max.Value);
        }

        var length = text.Length - start;
        if (max.HasValue)
        {
            length = Math.Min(length, Math.Max(0, max.Value));
        }

        return text.Substring(start, length);
    }

    private int IfCommand(string arguments)
    {
        var args = ParseArguments("If", "NOT/S,WARN/S,ERROR/S,FAIL/S,,EQ/K,GT/K,GE/K,VAL/S,EXISTS/K", arguments);
        if (args == null)
        {
            return ReturnError;
        }

        var returnCode = lastReturnCode;
        var condition = false;
        if (args[1] is true)
        {
            condition = returnCode >= ReturnWarn;
        }
        else if (args[2] is true)
        {
            condition = returnCode >= ReturnError;
        }
        else if (args[3] is true)
        {
            condition = returnCode >= ReturnFail;
        }
        else if (args[9] is string exists)
        {
            var path = ResolvePath(exists);
            condition = path != null && (File.Exists(path.HostPath) || Directory.Exists(path.HostPath));
        }
        else
        {
            var left = Text(args[4]) ?? string.Empty;
            var numeric = args[8] is true;
            if (args[5] is string eq)
            {
                condition = Compare(left, eq, numeric) == 0;
            }
            else if (args[6] is string gt)
            {
                condition = Compare(left, gt, numeric) > 0;
            }
            else if (args[7] is string ge)
            {
                condition = Compare(left, ge, numeric) >= 0;
            }
        }

        if (args[0] is true)
        {
            condition = !condition;
        }

        if (!condition)
        {
            SkipIfBlock(true);
        }

        return 0;
    }

    private static int Compare(string left, string right, bool numeric)
    {
        if (numeric && int.TryParse(left.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) &&
            int.TryParse(right.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var r))
        {
            return l.CompareTo(r);
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Else reached while executing true block skips to matching EndIf.
    /// </summary>
    private int ElseCommand(string arguments)
    {
        SkipIfBlock(false);
        return 0;
    }

    /// <summary>
    /// Skip lines of If block to matching Else or EndIf, nested If blocks are skipped.
    /// </summary>
    /// <param name="stopAtElse">Stop after matching Else to execute else block.</param>
    private void SkipIfBlock(bool stopAtElse)
    {
        var depth = 0;
        string line;
        while ((line = ReadScriptLine()) != null)
        {
            var command = FirstWord(line);
            if (command.Equals("If", StringComparison.OrdinalIgnoreCase))
            {
                depth++;
            }
            else if (command.Equals("EndIf", StringComparison.OrdinalIgnoreCase))
            {
                if (depth == 0)
                {
                    return;
                }

                depth--;
            }
            else if (stopAtElse && depth == 0 && command.Equals("Else", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
    }

    private static string FirstWord(string line)
    {
        var text = ShellLineParser.StripComment(line).Trim();
        var end = text.IndexOfAny(new[] { ' ', '\t' });
        return end < 0 ? text : text.Substring(0, end);
    }

    private int SkipCommand(string arguments)
    {
        var args = ParseArguments("Skip", "LABEL,BACK/S", arguments);
        if (args == null)
        {
            return ReturnError;
        }

        var label = Text(args[0]);
        var currentInput = Ram.ReadLong(CliAddress + CliStruct.CurrentInput);
        if (args[1] is true && files.TryGetValue(currentInput, out var file) && !file.IsConsole)
        {
            file.Position = 0;
        }

        string line;
        while ((line = ReadScriptLine()) != null)
        {
            var text = ShellLineParser.StripComment(line).Trim();
            var command = FirstWord(text);
            if (command.Equals("EndSkip", StringComparison.OrdinalIgnoreCase) && label == null)
            {
                return 0;
            }

            if (!command.Equals("Lab", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var labLabel = text.Substring(command.Length).Trim();
            if (label == null || labLabel.Equals(label, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
        }

        Print($"Label {label} not found by Skip\n");
        quitScript = true;
        return ReturnError;
    }

    private int QuitCommand(string arguments)
    {
        var args = ParseArguments("Quit", "RC/N", arguments);
        quitScript = true;
        return args == null ? ReturnError : Number(args[0]) ?? 0;
    }

    private int FailAtCommand(string arguments)
    {
        var args = ParseArguments("FailAt", "RCLIM/N", arguments);
        if (args == null)
        {
            return ReturnError;
        }

        var cli = CliAddress;
        var limit = Number(args[0]);
        if (!limit.HasValue)
        {
            Print($"Fail limit: {(int)Ram.ReadLong(cli + CliStruct.FailLevel)}\n");
            return 0;
        }

        Ram.WriteLong(cli + CliStruct.FailLevel, (uint)limit.Value);
        return 0;
    }

    private int SetCommand(string arguments) => SetVariableCommand("Set", arguments, false);

    private int SetEnvCommand(string arguments) => SetVariableCommand("SetEnv", arguments, true);

    private int SetVariableCommand(string command, string arguments, bool global)
    {
        var args = ParseArguments(command, global ? "NAME,SAVE/S,STRING/F" : "NAME,STRING/F", arguments);
        if (args == null)
        {
            return ReturnError;
        }

        var name = Text(args[0]);
        var value = Text(args[global ? 2 : 1]) ?? string.Empty;
        if (name == null)
        {
            foreach (var (variableName, variableValue) in ListVariables(global))
            {
                Print($"{variableName,-16}{variableValue}\n");
            }

            return 0;
        }

        var bytes = Ram.GetBytes(value);
        if (!global)
        {
            localVariables[name] = bytes;
            return 0;
        }

        return WriteEnvVariable(name, bytes, false) && (args[1] is not true || WriteEnvVariable(name, bytes, true))
            ? 0
            : ReturnError;
    }

    private IEnumerable<(string Name, string Value)> ListVariables(bool global)
    {
        if (!global)
        {
            return localVariables.Select(x => (x.Key, Ram.GetString(x.Value, 0, x.Value.Length)))
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList();
        }

        var env = ResolvePath("ENV:");
        if (env == null || !Directory.Exists(env.HostPath))
        {
            return Enumerable.Empty<(string, string)>();
        }

        return Directory.EnumerateFiles(env.HostPath)
            .Select(x => (Path.GetFileName(x), GetVariableText(Path.GetFileName(x)) ?? string.Empty))
            .OrderBy(x => x.Item1, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private int GetCommand(string arguments) => GetVariableCommand("Get", arguments, false);

    private int GetEnvCommand(string arguments) => GetVariableCommand("GetEnv", arguments, true);

    private int GetVariableCommand(string command, string arguments, bool global)
    {
        var args = ParseArguments(command, "NAME/A", arguments);
        if (args == null)
        {
            return ReturnError;
        }

        var name = Text(args[0]);
        string value = null;
        if (!global && localVariables.TryGetValue(name, out var bytes))
        {
            value = Ram.GetString(bytes, 0, bytes.Length);
        }
        else if (global)
        {
            value = ReadGlobalVariable(name);
        }

        if (value == null)
        {
            SetIoErr(DosErrors.ObjectNotFound);
            return ReturnWarn;
        }

        Print(value + "\n");
        return 0;
    }

    private string ReadGlobalVariable(string name)
    {
        var path = GetEnvPath(name, false);
        if (path == null || !File.Exists(path.HostPath))
        {
            return null;
        }

        var bytes = File.ReadAllBytes(path.HostPath);
        var text = Ram.GetString(bytes, 0, bytes.Length);
        var lineFeed = text.IndexOf('\n');
        return lineFeed >= 0 ? text.Substring(0, lineFeed) : text;
    }

    private int UnSetCommand(string arguments) => UnSetVariableCommand("UnSet", arguments, false);

    private int UnSetEnvCommand(string arguments) => UnSetVariableCommand("UnSetEnv", arguments, true);

    private int UnSetVariableCommand(string command, string arguments, bool global)
    {
        var args = ParseArguments(command, "NAME", arguments);
        if (args == null)
        {
            return ReturnError;
        }

        var name = Text(args[0]);
        if (name != null)
        {
            DeleteVariable(name, global ? GvfGlobalOnly : GvfLocalOnly);
        }

        return 0;
    }

    private int AliasCommand(string arguments)
    {
        var args = ParseArguments("Alias", "NAME,STRING/F", arguments);
        if (args == null)
        {
            return ReturnError;
        }

        var name = Text(args[0]);
        var value = Text(args[1]);
        if (name == null)
        {
            foreach (var alias in aliases.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                Print($"{alias.Key,-16}{alias.Value}\n");
            }

            return 0;
        }

        if (value == null)
        {
            if (aliases.TryGetValue(name, out var existing))
            {
                Print(existing + "\n");
            }

            return 0;
        }

        aliases[name] = value;
        return 0;
    }

    private int UnAliasCommand(string arguments)
    {
        var args = ParseArguments("UnAlias", "NAME", arguments);
        if (args == null)
        {
            return ReturnError;
        }

        if (Text(args[0]) is { } name)
        {
            aliases.Remove(name);
        }

        return 0;
    }

    private int AskCommand(string arguments)
    {
        var args = ParseArguments("Ask", "PROMPT/A", arguments);
        if (args == null)
        {
            return ReturnError;
        }

        while (true)
        {
            Print(Text(args[0]) + " ");
            FlushConsole();
            var answer = ReadLine(Ram.ReadLong(CliAddress + CliStruct.StandardInput));
            if (answer == null)
            {
                return 0;
            }

            answer = answer.Trim().ToLowerInvariant();
            if (answer is "y" or "yes")
            {
                return ReturnWarn;
            }

            if (answer is "" or "n" or "no")
            {
                return 0;
            }
        }
    }

    private int CdCommand(string arguments)
    {
        var args = ParseArguments("CD", "DIR", arguments);
        if (args == null)
        {
            return ReturnError;
        }

        var name = Text(args[0]);
        if (name == null)
        {
            Print(CurrentDirectory.FullPath + "\n");
            return 0;
        }

        var path = ResolvePath(name);
        if (path == null || !Directory.Exists(path.HostPath))
        {
            SetIoErr(path == null ? DosErrors.DeviceNotMounted : DosErrors.ObjectNotFound);
            Print($"Can't find {name}\n");
            return ReturnError;
        }

        return SetCurrentDirectory(path);
    }

    private int PathCommand(string arguments)
    {
        var args = ParseArguments("Path", "PATH/M,ADD/S,SHOW/S,RESET/S,REMOVE/S,QUIET/S,HEAD/S", arguments);
        if (args == null)
        {
            return ReturnError;
        }

        var paths = args[0] as List<string> ?? new List<string>();
        if (args[3] is true)
        {
            commandPath.Clear();
        }

        foreach (var path in paths)
        {
            commandPath.RemoveAll(x => x.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (args[4] is true)
            {
                continue;
            }

            var resolved = ResolvePath(path);
            if (resolved == null || !Directory.Exists(resolved.HostPath))
            {
                if (args[5] is not true)
                {
                    Print($"Path: {path} not found\n");
                }

                continue;
            }

            if (args[6] is true)
            {
                commandPath.Insert(0, path);
            }
            else
            {
                commandPath.Add(path);
            }
        }

        if (args[2] is true || (paths.Count == 0 && args[3] is not true))
        {
            Print("Current directory\n");
            foreach (var path in commandPath)
            {
                Print(path + "\n");
            }

            Print("C:\n");
        }

        return 0;
    }

    private int PromptCommand(string arguments)
    {
        var args = ParseArguments("Prompt", "PROMPT", arguments);
        if (args == null)
        {
            return ReturnError;
        }

        SetCliString(CliStruct.Prompt, Text(args[0]) ?? "%N> ");
        return 0;
    }

    private int StackCommand(string arguments)
    {
        var args = ParseArguments("Stack", "SIZE/N", arguments);
        if (args == null)
        {
            return ReturnError;
        }

        var cli = CliAddress;
        var size = Number(args[0]);
        if (!size.HasValue)
        {
            Print($"Current stack size is {Ram.ReadLong(cli + CliStruct.DefaultStack) * 4} bytes\n");
            return 0;
        }

        if (size.Value < 1600)
        {
            Print("Stack: Requested size too small\n");
            return ReturnError;
        }

        Ram.WriteLong(cli + CliStruct.DefaultStack, (uint)(size.Value / 4));
        return 0;
    }

    private int WhyCommand(string arguments)
    {
        if (lastReturnCode == 0 && lastResult2 == 0)
        {
            Print("The last command did not set a return code\n");
            return 0;
        }

        Print(lastResult2 == 0
            ? $"Last command failed with return code {lastReturnCode}\n"
            : $"Last command failed because {DosErrors.GetMessage(lastResult2)}\n");
        return 0;
    }

    private int FaultCommand(string arguments)
    {
        var args = ParseArguments("Fault", "/N/M", arguments);
        if (args == null)
        {
            return ReturnError;
        }

        foreach (var value in args[0] as List<string> ?? new List<string>())
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                SetIoErr(DosErrors.BadNumber);
                Print($"Fault: {DosErrors.GetMessage(DosErrors.BadNumber)}\n");
                return ReturnError;
            }

            Print($"Fault {code}: {DosErrors.GetMessage(code)}\n");
        }

        return 0;
    }

    private int ResidentCommand(string arguments)
    {
        var args = ParseArguments("Resident", "NAME,FILE,REMOVE/S,ADD/S,REPLACE/S,PURE=FORCE/S,SYSTEM/S", arguments);
        if (args == null)
        {
            return ReturnError;
        }

        var name = Text(args[0]);
        var file = Text(args[1]);
        if (name == null)
        {
            Print("NAME                 USECOUNT\n");
            foreach (var builtin in BuiltinCommands.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                Print($"{builtin,-21}INTERNAL\n");
            }

            foreach (var resident in residentSegments.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                Print($"{resident,-21}0\n");
            }

            return 0;
        }

        if (args[2] is true)
        {
            if (residentSegments.TryGetValue(name, out var removed))
            {
                loader.Unload(removed);
                residentSegments.Remove(name);
                return 0;
            }

            Print($"Resident: {name} not found\n");
            return ReturnError;
        }

        var path = FindCommand(file ?? name);
        if (path == null)
        {
            SetIoErr(DosErrors.ObjectNotFound);
            Print($"Resident: {file ?? name} not found\n");
            return ReturnError;
        }

        try
        {
            using var stream = File.OpenRead(path.HostPath);
            var segList = loader.Load(stream).GetAwaiter().GetResult().Bptr;
            var residentName = file == null ? Path.GetFileName(path.HostPath) : name;
            if (residentSegments.TryGetValue(residentName, out var existing))
            {
                loader.Unload(existing);
            }

            residentSegments[residentName] = segList;
            return 0;
        }
        catch (Exception e) when (e is IOException or VmException)
        {
            SetIoErr(DosErrors.FileNotObject);
            Print($"Resident: {name} is not an executable\n");
            return ReturnError;
        }
    }

    /// <summary>
    /// Run executes command synchronously, as there is no multitasking.
    /// </summary>
    private int RunBuiltinCommand(string arguments) =>
        string.IsNullOrWhiteSpace(arguments) ? 0 : ExecuteLine(arguments);

    private int EndShellCommand(string arguments)
    {
        endShell = true;
        quitScript = true;
        return 0;
    }

    private int NewShellCommand(string arguments)
    {
        Print("NewShell: New shells are not supported in virtual machine\n");
        SetIoErr(DosErrors.NotImplemented);
        return ReturnFail;
    }
}
