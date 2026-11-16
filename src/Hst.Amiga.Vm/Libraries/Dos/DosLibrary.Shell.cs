namespace Hst.Amiga.Vm.Libraries.Dos;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FileSystem;
using Processor;
using Structs;

/// <summary>
/// AmigaDOS shell running command lines and scripts in the process. Commands run as subroutines in the same
/// process with their own stack, as there is no multitasking.
/// </summary>
public partial class DosLibrary
{
    public const int ReturnWarn = 5;
    public const int ReturnError = 10;
    public const int ReturnFail = 20;

    private const uint ProtectionScript = 1 << 6;
    private const int MinStackSize = 4096;

    private readonly Dictionary<string, string> aliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> commandPath = new();
    private readonly Dictionary<string, uint> residentSegments = new(StringComparer.OrdinalIgnoreCase);

    private bool endShell;
    private bool quitScript;
    private int lastReturnCode;
    private int lastResult2;
    private string lastCommandName = string.Empty;

    /// <summary>
    /// Last command ended by calling dos.library Exit().
    /// </summary>
    public bool LastCommandCalledExit { get; private set; }

    /// <summary>
    /// Script is running with current input of cli set to a script file, e.g. by Execute.
    /// </summary>
    public bool IsScriptRunning
    {
        get
        {
            var cli = CliAddress;
            return cli != 0 && Ram.ReadLong(cli + CliStruct.CurrentInput) != Ram.ReadLong(cli + CliStruct.StandardInput);
        }
    }

    /// <summary>
    /// Command exists as built-in, resident or file in current directory, command path or C:.
    /// </summary>
    public bool CommandExists(string command) =>
        BuiltinCommands.ContainsKey(command) || residentSegments.ContainsKey(command) || FindCommand(command) != null;

    /// <summary>
    /// Report errors from commands run by the shell instead of stopping the virtual machine.
    /// </summary>
    private bool catchCommandErrors;

    /// <summary>
    /// Run program segment list as a command with arguments in the process.
    /// </summary>
    /// <param name="segList">BCPL pointer to segment list.</param>
    /// <param name="commandName">Name of command.</param>
    /// <param name="arguments">Argument string.</param>
    /// <param name="stackSize">Stack size in bytes, zero uses default stack size of cli.</param>
    /// <returns>Return code of program.</returns>
    public int RunSegment(uint segList, string commandName, string arguments, uint stackSize = 0)
    {
        var cli = CliAddress;
        if (stackSize == 0)
        {
            stackSize = cli == 0 ? MinStackSize : Ram.ReadLong(cli + CliStruct.DefaultStack) * 4;
        }

        stackSize = (Math.Max(stackSize, MinStackSize) + 3) & ~3u;
        var stack = Context.Allocator.Allocate(stackSize);
        if (stack == 0)
        {
            SetIoErr(DosErrors.NoFreeStore);
            return ReturnFail;
        }

        var argumentText = (arguments ?? string.Empty).TrimEnd('\n') + "\n";
        var argumentsAddress = AllocateString(argumentText);
        var stackUpper = stack + stackSize;

        // save registers and process state restored after command returns
        var savedD = (uint[])Cpu.D.Clone();
        var savedA = (uint[])Cpu.A.Clone();
        var savedPc = Cpu.PC;
        var savedCcr = Cpu.Ccr;
        var savedArguments = Ram.ReadLong(Process + ProcessStruct.Arguments);
        var savedSpLower = Ram.ReadLong(Process + TaskStruct.SpLower);
        var savedSpUpper = Ram.ReadLong(Process + TaskStruct.SpUpper);
        var savedStackSize = Ram.ReadLong(Process + ProcessStruct.StackSize);
        var savedStackBase = Ram.ReadLong(Process + ProcessStruct.StackBase);
        var savedReturnAddr = Ram.ReadLong(Process + ProcessStruct.ReturnAddr);
        var savedCommandName = cli == 0 ? null : Ram.ReadBString(BAddr(Ram.ReadLong(cli + CliStruct.CommandName)));
        var savedModule = cli == 0 ? 0 : Ram.ReadLong(cli + CliStruct.Module);

        Ram.WriteLong(Process + ProcessStruct.Arguments, argumentsAddress);
        Ram.WriteLong(Process + TaskStruct.SpLower, stack);
        Ram.WriteLong(Process + TaskStruct.SpUpper, stackUpper);
        Ram.WriteLong(Process + ProcessStruct.StackSize, stackSize);
        Ram.WriteLong(Process + ProcessStruct.StackBase, MkBAddr(stack));
        Ram.WriteLong(Process + ProcessStruct.ReturnAddr, stackUpper - 4);
        if (cli != 0)
        {
            SetCliString(CliStruct.CommandName, commandName ?? string.Empty);
            Ram.WriteLong(cli + CliStruct.Module, segList);
        }

        // arguments are read from input handle like in the input buffer on AmigaDOS
        var inputFile = files.TryGetValue(InputHandle, out var f) ? f : null;
        inputFile?.SetPendingInput(Ram.GetBytes(argumentText));

        Cpu.A[7] = stackUpper;
        Cpu.Push32(stackSize);
        Cpu.D[0] = (uint)Ram.GetBytes(argumentText).Length;
        Cpu.A[0] = argumentsAddress;
        Cpu.A[6] = exec.BaseAddress;
        IoErrValue = 0;

        int returnCode;
        LastCommandCalledExit = false;
        try
        {
            returnCode = (int)Context.CallSubroutine(BAddr(segList) + 4);
        }
        catch (ProgramExitException e)
        {
            returnCode = e.ReturnCode;
            LastCommandCalledExit = e.CalledExit;
        }
        finally
        {
            inputFile?.ClearPendingInput();
            Array.Copy(savedD, Cpu.D, savedD.Length);
            Array.Copy(savedA, Cpu.A, savedA.Length);
            Cpu.PC = savedPc;
            Cpu.Ccr = savedCcr;
            Ram.WriteLong(Process + ProcessStruct.Arguments, savedArguments);
            Ram.WriteLong(Process + TaskStruct.SpLower, savedSpLower);
            Ram.WriteLong(Process + TaskStruct.SpUpper, savedSpUpper);
            Ram.WriteLong(Process + ProcessStruct.StackSize, savedStackSize);
            Ram.WriteLong(Process + ProcessStruct.StackBase, savedStackBase);
            Ram.WriteLong(Process + ProcessStruct.ReturnAddr, savedReturnAddr);
            if (cli != 0)
            {
                SetCliString(CliStruct.CommandName, savedCommandName ?? string.Empty);
                Ram.WriteLong(cli + CliStruct.Module, savedModule);
            }

            Context.Allocator.Free(argumentsAddress);
            Context.Allocator.Free(stack);
        }

        SetReturnCode(returnCode, IoErrValue);
        return returnCode;
    }

    private void SetReturnCode(int returnCode, int result2)
    {
        lastReturnCode = returnCode;
        lastResult2 = result2;
        var cli = CliAddress;
        if (cli != 0)
        {
            Ram.WriteLong(cli + CliStruct.ReturnCode, (uint)returnCode);
            Ram.WriteLong(cli + CliStruct.Result2, (uint)result2);
        }

        localVariables["RC"] = Ram.GetBytes(returnCode.ToString());
        localVariables["Result2"] = Ram.GetBytes(result2.ToString());
    }

    /// <summary>
    /// Run shell reading command lines from current input of cli. Scripts started with Execute are read until
    /// end and interactive shell reads command lines from standard input until EndShell or end of input.
    /// </summary>
    /// <param name="interactiveShell">Read command lines from standard input with prompt.</param>
    /// <returns>Return code of last command.</returns>
    public int RunShell(bool interactiveShell)
    {
        var savedCatch = catchCommandErrors;
        catchCommandErrors = true;
        endShell = false;
        try
        {
            while (!endShell)
            {
                var cli = CliAddress;
                var currentInput = Ram.ReadLong(cli + CliStruct.CurrentInput);
                var standardInput = Ram.ReadLong(cli + CliStruct.StandardInput);
                var inScript = currentInput != standardInput;
                if (!inScript && !interactiveShell)
                {
                    break;
                }

                if (!inScript && interactive)
                {
                    WritePrompt();
                }

                // input and output of each command are standard input and output of cli, as commands like
                // Execute can leave selected input or output closed
                Ram.WriteLong(Process + ProcessStruct.Cis, standardInput);
                Ram.WriteLong(Process + ProcessStruct.Cos, Ram.ReadLong(cli + CliStruct.StandardOutput));

                var line = ReadLine(currentInput);
                if (line == null)
                {
                    if (inScript)
                    {
                        EndScript();
                        continue;
                    }

                    break;
                }

                var returnCode = ExecuteLine(line);
                if (quitScript)
                {
                    quitScript = false;
                    if (inScript)
                    {
                        EndScript();
                    }

                    continue;
                }

                var failLevel = (int)Ram.ReadLong(cli + CliStruct.FailLevel);
                if (inScript && returnCode >= failLevel && failLevel > 0)
                {
                    WriteString(OutputHandle, $"{lastCommandName} failed returncode {returnCode}\n");
                    EndScript();
                }
            }
        }
        finally
        {
            catchCommandErrors = savedCatch;
        }

        FlushConsole();
        return lastReturnCode;
    }

    /// <summary>
    /// End script by closing current input, deleting temporary command file and restoring standard input.
    /// </summary>
    private void EndScript()
    {
        var cli = CliAddress;
        var currentInput = Ram.ReadLong(cli + CliStruct.CurrentInput);
        var standardInput = Ram.ReadLong(cli + CliStruct.StandardInput);
        if (currentInput != standardInput && currentInput != 0)
        {
            FreeFileHandle(currentInput);
        }

        Ram.WriteLong(cli + CliStruct.CurrentInput, standardInput);

        var commandFileBstr = Ram.ReadLong(cli + CliStruct.CommandFile);
        var commandFile = commandFileBstr == 0 ? null : Ram.ReadBString(BAddr(commandFileBstr));
        if (!string.IsNullOrEmpty(commandFile))
        {
            var path = ResolvePath(commandFile);
            if (path != null && File.Exists(path.HostPath))
            {
                try
                {
                    File.Delete(path.HostPath);
                    Metadata.Deleted(path.HostPath);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                }
            }

            Ram.WriteBString(BAddr(commandFileBstr), string.Empty);
        }
    }

    private void WritePrompt()
    {
        var cli = CliAddress;
        var prompt = Ram.ReadBString(BAddr(Ram.ReadLong(cli + CliStruct.Prompt))) ?? string.Empty;
        var currentDirectory = Ram.ReadBString(BAddr(Ram.ReadLong(cli + CliStruct.SetName))) ?? string.Empty;
        var text = prompt.Replace("%N", "1").Replace("%n", "1")
            .Replace("%S", currentDirectory).Replace("%s", currentDirectory)
            .Replace("%R", lastReturnCode.ToString()).Replace("%r", lastReturnCode.ToString());
        WriteString(OutputHandle, text);
        FlushConsole();
    }

    /// <summary>
    /// Read line from file handle.
    /// </summary>
    /// <returns>Line without newline or null at end of file.</returns>
    private string ReadLine(uint fileHandle)
    {
        if (!files.TryGetValue(fileHandle, out var file))
        {
            return null;
        }

        var builder = new StringBuilder();
        var read = false;
        while (true)
        {
            int character;
            try
            {
                character = file.ReadChar();
            }
            catch (IOException)
            {
                character = -1;
            }

            if (character < 0)
            {
                break;
            }

            read = true;
            if (character == '\n')
            {
                return builder.ToString();
            }

            builder.Append((char)character);
        }

        return read ? builder.ToString() : null;
    }

    /// <summary>
    /// Read next line from current input of cli, used by commands skipping lines like If and Skip.
    /// </summary>
    private string ReadScriptLine() => ReadLine(Ram.ReadLong(CliAddress + CliStruct.CurrentInput));

    /// <summary>
    /// Execute command line with variables, backquotes, aliases and redirection.
    /// </summary>
    /// <returns>Return code of command.</returns>
    public int ExecuteLine(string line, uint inputHandle = 0, uint outputHandle = 0)
    {
        line = ShellLineParser.StripComment(line.TrimEnd('\r')).Trim();
        if (line.Length == 0)
        {
            return lastReturnCode;
        }

        line = ShellLineParser.ExpandVariables(line, GetVariableText);
        line = ShellLineParser.SubstituteBackquotes(line, CaptureOutput);
        line = ExpandAlias(line);

        var parsed = ShellLineParser.Parse(line);
        if (parsed.Command.Length == 0)
        {
            return lastReturnCode;
        }

        lastCommandName = parsed.Command;

        var savedInput = InputHandle;
        var savedOutput = OutputHandle;
        uint redirectedInput = 0;
        uint redirectedOutput = 0;
        try
        {
            if (parsed.InputFile != null)
            {
                redirectedInput = Open(parsed.InputFile, ModeOldFile);
                if (redirectedInput == 0)
                {
                    WriteString(savedOutput, $"Unable to open redirection file {parsed.InputFile}\n");
                    SetReturnCode(ReturnFail, IoErrValue);
                    return ReturnFail;
                }
            }

            if (parsed.OutputFile != null)
            {
                redirectedOutput = Open(parsed.OutputFile, parsed.AppendOutput ? ModeReadWrite : ModeNewFile);
                if (redirectedOutput == 0)
                {
                    if (redirectedInput != 0)
                    {
                        FreeFileHandle(redirectedInput);
                        redirectedInput = 0;
                    }

                    WriteString(savedOutput, $"Unable to open redirection file {parsed.OutputFile}\n");
                    SetReturnCode(ReturnFail, IoErrValue);
                    return ReturnFail;
                }

                if (parsed.AppendOutput && files.TryGetValue(redirectedOutput, out var appendFile))
                {
                    appendFile.Position = appendFile.Length;
                }
            }

            var input = redirectedInput != 0 ? redirectedInput : inputHandle;
            var output = redirectedOutput != 0 ? redirectedOutput : outputHandle;
            if (input != 0)
            {
                Ram.WriteLong(Process + ProcessStruct.Cis, input);
            }

            if (output != 0)
            {
                Ram.WriteLong(Process + ProcessStruct.Cos, output);
            }

            return RunCommandName(parsed.Command, parsed.Arguments);
        }
        finally
        {
            Ram.WriteLong(Process + ProcessStruct.Cis, savedInput);
            Ram.WriteLong(Process + ProcessStruct.Cos, savedOutput);
            if (redirectedInput != 0)
            {
                FreeFileHandle(redirectedInput);
            }

            if (redirectedOutput != 0)
            {
                FreeFileHandle(redirectedOutput);
            }
        }
    }

    /// <summary>
    /// Run built-in, resident or disk command with arguments.
    /// </summary>
    private int RunCommandName(string command, string arguments)
    {
        if (TryRunBuiltinCommand(command, arguments, out var builtinReturnCode))
        {
            SetReturnCode(builtinReturnCode, builtinReturnCode == 0 ? 0 : IoErrValue);
            return builtinReturnCode;
        }

        if (residentSegments.TryGetValue(command, out var residentSegList))
        {
            return RunCommandSegment(residentSegList, command, arguments);
        }

        var path = FindCommand(command);
        if (path == null)
        {
            // directory name as command changes current directory
            var directory = arguments.Length == 0 ? ResolvePath(command) : null;
            if (directory != null && Directory.Exists(directory.HostPath))
            {
                return SetCurrentDirectory(directory);
            }

            WriteString(OutputHandle, $"Unknown command {command}\n");
            SetReturnCode(ReturnFail, DosErrors.ObjectNotFound);
            return ReturnFail;
        }

        // script files with script protection bit are run with execute
        if ((Metadata.Get(path.HostPath).Protection & ProtectionScript) != 0)
        {
            var execute = FindCommand("Execute");
            if (execute != null)
            {
                return RunFileCommand(execute, "Execute", $"{Quote(path.FullPath)} {arguments}".TrimEnd());
            }
        }

        return RunFileCommand(path, command, arguments);
    }

    private int RunFileCommand(AmigaPath path, string command, string arguments)
    {
        uint segList;
        try
        {
            using var stream = File.OpenRead(path.HostPath);
            segList = loader.Load(stream).GetAwaiter().GetResult().Bptr;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or VmException)
        {
            WriteString(OutputHandle, $"{command}: file is not executable\n");
            SetReturnCode(ReturnFail, DosErrors.FileNotObject);
            return ReturnFail;
        }

        try
        {
            return RunCommandSegment(segList, command, arguments);
        }
        finally
        {
            loader.Unload(segList);
        }
    }

    /// <summary>
    /// Run command segment list, reporting errors stopping command, if shell catches command errors.
    /// </summary>
    private int RunCommandSegment(uint segList, string command, string arguments)
    {
        if (!catchCommandErrors)
        {
            return RunSegment(segList, command, arguments);
        }

        try
        {
            return RunSegment(segList, command, arguments);
        }
        catch (Exception e) when (e is CpuException or UnimplementedFunctionException or BusErrorException)
        {
            FlushConsole();
            Context.Log($"{command}: {e.Message}");
            SetReturnCode(ReturnFail, IoErrValue);
            return ReturnFail;
        }
    }

    /// <summary>
    /// Find command file in current directory, command path and C:.
    /// </summary>
    private AmigaPath FindCommand(string command)
    {
        if (command.IndexOf(':') >= 0 || command.IndexOf('/') >= 0)
        {
            var direct = ResolvePath(command);
            return direct != null && File.Exists(direct.HostPath) ? direct : null;
        }

        foreach (var directory in new[] { string.Empty }.Concat(commandPath).Concat(new[] { "C:" }))
        {
            var path = ResolvePath(directory.Length == 0 ? command : AddPart(directory, command));
            if (path != null && File.Exists(path.HostPath))
            {
                return path;
            }
        }

        return null;
    }

    private static string Quote(string value) => value.IndexOfAny(new[] { ' ', '"', '=' }) >= 0
        ? $"\"{value.Replace("*", "**").Replace("\"", "*\"")}\""
        : value;

    private string ExpandAlias(string line)
    {
        var position = 0;
        while (position < line.Length && line[position] != ' ' && line[position] != '\t')
        {
            position++;
        }

        var name = line.Substring(0, position);
        if (!aliases.TryGetValue(name, out var alias))
        {
            return line;
        }

        var rest = line.Substring(position).Trim();
        var bracket = alias.IndexOf("[]", StringComparison.Ordinal);
        return bracket >= 0
            ? alias.Substring(0, bracket) + rest + alias.Substring(bracket + 2)
            : $"{alias} {rest}".TrimEnd();
    }

    /// <summary>
    /// Get local or global variable as text up to first linefeed.
    /// </summary>
    /// <returns>Value of variable or null, if variable doesn't exist.</returns>
    private string GetVariableText(string name)
    {
        byte[] value = null;
        if (localVariables.TryGetValue(name, out var localValue))
        {
            value = localValue;
        }
        else
        {
            var path = GetEnvPath(name, false);
            if (path != null && File.Exists(path.HostPath))
            {
                try
                {
                    value = File.ReadAllBytes(path.HostPath);
                }
                catch (IOException)
                {
                }
            }
        }

        if (value == null)
        {
            return null;
        }

        var text = Ram.GetString(value, 0, value.Length);
        var lineFeed = text.IndexOf('\n');
        return lineFeed >= 0 ? text.Substring(0, lineFeed) : text;
    }

    /// <summary>
    /// Run command line and capture output written to output handle.
    /// </summary>
    private string CaptureOutput(string line)
    {
        var stream = new MemoryStream();
        var handle = CreateFileHandle(address => new DosFile(address, null, stream, false, false, null, false));
        try
        {
            ExecuteLine(line, 0, handle);
        }
        finally
        {
            FreeFileHandle(handle);
        }

        var bytes = stream.ToArray();
        return Ram.GetString(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Change current directory of process.
    /// </summary>
    private int SetCurrentDirectory(AmigaPath directory)
    {
        var newLock = CreateLock(directory, SharedLock);
        if (newLock == 0)
        {
            return ReturnError;
        }

        var oldLock = Ram.ReadLong(Process + ProcessStruct.CurrentDir);
        Ram.WriteLong(Process + ProcessStruct.CurrentDir, newLock);
        FreeLock(oldLock);
        SetCliString(CliStruct.SetName, directory.FullPath);
        SetReturnCode(0, 0);
        return 0;
    }
}
