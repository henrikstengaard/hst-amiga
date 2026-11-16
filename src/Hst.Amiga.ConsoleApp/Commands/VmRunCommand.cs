namespace Hst.Amiga.ConsoleApp.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vm;
using Vm.FileSystem;
using Vm.Processor;

/// <summary>
/// Run Amiga CLI executable in virtual machine.
/// </summary>
public class VmRunCommand
{
    private readonly string executablePath;
    private readonly IEnumerable<string> arguments;
    private readonly VmOptions options;

    public VmRunCommand(string executablePath, IEnumerable<string> arguments, VmOptions options)
    {
        this.executablePath = executablePath;
        this.arguments = arguments ?? Enumerable.Empty<string>();
        this.options = options;
    }

    /// <summary>
    /// Create virtual machine options from command line values.
    /// </summary>
    /// <exception cref="ArgumentException">Invalid volume or assign.</exception>
    public static VmOptions CreateOptions(string cpu, int? memory, int? stack, IEnumerable<string> volumes,
        IEnumerable<string> assigns, IEnumerable<string> stubLibraries, string currentDirectory, long? maxInstructions,
        bool trace, bool traceLibraries, string uaeMetadata = null, bool writeUaeMetadata = false)
    {
        var options = new VmOptions
        {
            CpuType = cpu == "68000" ? CpuType.M68000 : CpuType.M68020,
            CurrentDirectory = currentDirectory,
            MaxInstructions = maxInstructions ?? 0,
            TraceInstructions = trace,
            TraceLibraryCalls = traceLibraries,
            UaeMetadata = ParseUaeMetadata(uaeMetadata),
            WriteUaeMetadata = writeUaeMetadata
        };

        if (memory.HasValue)
        {
            options.MemorySize = (uint)memory.Value * 1024 * 1024;
        }

        if (stack.HasValue)
        {
            options.StackSize = (uint)stack.Value;
        }

        foreach (var volume in volumes ?? Enumerable.Empty<string>())
        {
            var (name, path) = ParseMapping(volume, "volume");
            options.Volumes[name] = path;
        }

        foreach (var stubLibrary in stubLibraries ?? Enumerable.Empty<string>())
        {
            options.StubLibraries.Add(stubLibrary);
        }

        foreach (var assign in assigns ?? Enumerable.Empty<string>())
        {
            var (name, path) = ParseMapping(assign, "assign");
            options.Assigns[name] = path;
        }

        return options;
    }

    private static UaeMetadataMode ParseUaeMetadata(string value) =>
        (value ?? "auto").ToLowerInvariant() switch
        {
            "auto" => UaeMetadataMode.Auto,
            "uaefsdb" => UaeMetadataMode.UaeFsDb,
            "uaemetafile" => UaeMetadataMode.UaeMetafile,
            "none" => UaeMetadataMode.None,
            _ => throw new ArgumentException(
                $"Invalid UAE metadata '{value}', expected auto, uaefsdb, uaemetafile or none")
        };

    private static (string Name, string Path) ParseMapping(string value, string type)
    {
        var separator = value.IndexOf('=');
        if (separator <= 0 || separator == value.Length - 1)
        {
            throw new ArgumentException($"Invalid {type} '{value}', expected name=path");
        }

        return (value.Substring(0, separator).TrimEnd(':'), value.Substring(separator + 1));
    }

    /// <summary>
    /// Join arguments into Amiga argument string, quoting arguments with spaces or quotes.
    /// </summary>
    public static string JoinArguments(IEnumerable<string> arguments) =>
        string.Join(" ", arguments.Select(QuoteArgument));

    private static string QuoteArgument(string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
        {
            return argument;
        }

        var builder = new StringBuilder("\"");
        foreach (var c in argument)
        {
            switch (c)
            {
                case '"':
                    builder.Append("*\"");
                    break;
                case '*':
                    builder.Append("**");
                    break;
                case '\n':
                    builder.Append("*N");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.Append('"').ToString();
    }

    public async Task<VmResult> Execute(Action<AmigaVm> started, CancellationToken token)
    {
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException($"Executable '{executablePath}' not found", executablePath);
        }

        var vm = new AmigaVm(options);
        started?.Invoke(vm);
        return await vm.Run(executablePath, JoinArguments(arguments), token);
    }
}
