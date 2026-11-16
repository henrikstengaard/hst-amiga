namespace Hst.Amiga.Vm;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FileSystem;
using Libraries.Dos;
using Libraries.Exec;
using Libraries.MathLibraries;
using Libraries.Utility;
using Loader;
using Processor;

/// <summary>
/// Virtual machine running Amiga CLI executables on an emulated 68000/68020 cpu with emulated exec.library,
/// dos.library and utility.library mapping volumes to host directories and console to host streams.
/// </summary>
public class AmigaVm
{
    private const uint SupervisorStackSize = 8 * 1024;

    private static readonly (string Name, string Path)[] DefaultSysAssigns =
    {
        ("C", "SYS:C"), ("S", "SYS:S"), ("L", "SYS:L"), ("LIBS", "SYS:Libs"), ("DEVS", "SYS:Devs"),
        ("FONTS", "SYS:Fonts"), ("LOCALE", "SYS:Locale"), ("ENVARC", "SYS:Prefs/Env-Archive")
    };

    private readonly VmOptions options;
    private VmContext context;

    public AmigaVm(VmOptions options = null)
    {
        this.options = options ?? new VmOptions();
    }

    /// <summary>
    /// Send break signals to running program, default is ctrl-c. Thread safe.
    /// </summary>
    public void SendBreak(uint signals = ExecLibrary.SigBreakCtrlC)
    {
        context?.SendBreak(signals);
    }

    /// <summary>
    /// Run Amiga executable from host file.
    /// </summary>
    /// <param name="executablePath">Host path to Amiga hunk executable.</param>
    /// <param name="arguments">Argument string passed to program.</param>
    /// <param name="cancellationToken">Cancellation token stopping program.</param>
    public async Task<VmResult> Run(string executablePath, string arguments = "",
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(executablePath);
        var bytes = await ReadAllBytes(fullPath, cancellationToken);
        return await Run(bytes, Path.GetFileName(fullPath), arguments, Path.GetDirectoryName(fullPath),
            cancellationToken);
    }

    /// <summary>
    /// Run Amiga executable from bytes.
    /// </summary>
    /// <param name="executable">Amiga hunk executable.</param>
    /// <param name="programName">Name of program.</param>
    /// <param name="arguments">Argument string passed to program.</param>
    /// <param name="programDirectory">Host directory of program used for PROGDIR:, if any.</param>
    /// <param name="cancellationToken">Cancellation token stopping program.</param>
    public Task<VmResult> Run(byte[] executable, string programName, string arguments,
        string programDirectory = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Start(programName, programDirectory, executable, (dos, segmentList, result) =>
        {
            var argumentString = (arguments ?? string.Empty).TrimEnd('\n') + "\n";
            var returnCode = dos.RunSegment(segmentList.Bptr, programName, argumentString, options.StackSize);
            result.ExitReason = dos.LastCommandCalledExit ? VmExitReason.Exit : VmExitReason.Returned;

            // program like Execute can start a script, which is run by the shell after program returns
            return dos.IsScriptRunning ? dos.RunShell(false) : returnCode;
        }, cancellationToken));
    }

    /// <summary>
    /// Run AmigaDOS script with Execute command from C:.
    /// </summary>
    /// <param name="script">Host path or Amiga path to script.</param>
    /// <param name="arguments">Arguments passed to script.</param>
    /// <param name="cancellationToken">Cancellation token stopping script.</param>
    public Task<VmResult> Execute(string script, string arguments = "", CancellationToken cancellationToken = default)
    {
        string programDirectory = null;
        var scriptPath = script;
        if (File.Exists(script))
        {
            var fullPath = Path.GetFullPath(script);
            programDirectory = Path.GetDirectoryName(fullPath);
            scriptPath = $"PROGDIR:{Path.GetFileName(fullPath)}";
        }

        return Task.FromResult(Start("Execute", programDirectory, null, (dos, _, result) =>
        {
            if (!dos.CommandExists("Execute"))
            {
                throw new VmException("Execute command not found in C:, which is required to run scripts");
            }

            var quoted = $"\"{scriptPath.Replace("*", "**").Replace("\"", "*\"")}\"";
            dos.ExecuteLine($"Execute {quoted} {arguments}".TrimEnd());
            result.ExitReason = VmExitReason.Returned;
            return dos.RunShell(false);
        }, cancellationToken));
    }

    /// <summary>
    /// Run interactive AmigaDOS shell reading commands from console input.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token stopping shell.</param>
    public Task<VmResult> Shell(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Start("Shell", null, null, (dos, _, result) =>
        {
            result.ExitReason = VmExitReason.Returned;
            return dos.RunShell(true);
        }, cancellationToken));
    }

    /// <summary>
    /// Setup virtual machine with libraries, volumes and process and run body.
    /// </summary>
    private VmResult Start(string programName, string programDirectory, byte[] executable,
        Func<DosLibrary, SegmentList, VmResult, int> body, CancellationToken cancellationToken)
    {
        var result = new VmResult();
        string ramDirectory = null;
        DosLibrary dos = null;

        try
        {
            context = new VmContext(options, cancellationToken);
            var volumes = new VolumeManager(new HostMetadata(options.UaeMetadata, options.WriteUaeMetadata,
                context.Log));
            ramDirectory = SetupVolumes(volumes, programDirectory);

            var exec = new ExecLibrary(context);
            exec.Install();
            context.AddLibrary(exec);
            exec.DebugOutput = options.Error;

            dos = new DosLibrary(context, exec, volumes);
            dos.Install();
            context.AddLibrary(dos);

            var utility = new UtilityLibrary(context, exec);
            utility.Install();
            context.AddLibrary(utility);

            var version = new Libraries.VersionLibrary(context, exec);
            version.Install();
            context.AddLibrary(version);

            foreach (var format in new[] { MathFormat.Ffp, MathFormat.IeeeSingle, MathFormat.IeeeDouble })
            {
                var basLibrary = new MathBasLibrary(context, exec, format);
                basLibrary.Install();
                context.AddLibrary(basLibrary);

                var transLibrary = new MathTransLibrary(context, exec, format);
                transLibrary.Install();
                context.AddLibrary(transLibrary);
            }

            foreach (var name in options.StubLibraries ?? Enumerable.Empty<string>())
            {
                if (context.FindLibrary(name) != null)
                {
                    continue;
                }

                var stubLibrary = new Libraries.StubLibrary(context, exec, name);
                stubLibrary.Install();
                context.AddLibrary(stubLibrary);
            }

            exec.SetResidentModules(context.Libraries);

            var segmentList = executable == null ? null : LoadExecutable(executable);

            var stack = context.Allocator.Allocate(options.StackSize);
            var supervisorStack = context.Allocator.Allocate(SupervisorStackSize);
            if (stack == 0 || supervisorStack == 0)
            {
                throw new VmException("Not enough memory to allocate stack");
            }

            var stackUpper = stack + options.StackSize;

            var currentDirectory = FindCurrentDirectory(volumes);
            var homeDirectory = programDirectory == null ? null : volumes.Resolve("PROGDIR:", currentDirectory);

            var cpu = context.Cpu;

            // cpu starts in supervisor mode, set supervisor stack and switch to user mode
            cpu.A[7] = supervisorStack + SupervisorStackSize;
            cpu.Sr = 0x0000;
            cpu.A[7] = stackUpper;

            var interactive = options.Interactive ?? (options.Input == null && !Console.IsInputRedirected);
            dos.SetupProcess(new ProcessSetup
            {
                ProgramName = programName,
                Arguments = "\n",
                SegList = segmentList?.Bptr ?? 0,
                StackLower = stack,
                StackUpper = stackUpper,
                StackSize = options.StackSize,
                CurrentDirectory = currentDirectory,
                HomeDirectory = homeDirectory,
                Input = options.Input ?? CreateConsoleInput(),
                Output = options.Output ?? new BufferedStream(Console.OpenStandardOutput()),
                Error = options.Error ?? new BufferedStream(Console.OpenStandardError()),
                Interactive = interactive
            });

            // commands run as subroutines with their own stack, returning to exit stub if stack is unwound
            cpu.Push32(options.StackSize);
            cpu.Push32(context.ExitStub);
            cpu.A[6] = exec.BaseAddress;

            result.ReturnCode = body(dos, segmentList, result);
        }
        catch (ProgramExitException e)
        {
            result.ExitReason = e.CalledExit ? VmExitReason.Exit : VmExitReason.Returned;
            result.ReturnCode = e.ReturnCode;
        }
        catch (CpuException e)
        {
            result.ExitReason = VmExitReason.CpuException;
            result.ReturnCode = 20;
            result.Error = $"{e.Message}{Environment.NewLine}{e.Registers}";
        }
        catch (UnimplementedFunctionException e)
        {
            result.ExitReason = VmExitReason.UnimplementedFunction;
            result.ReturnCode = 20;
            result.Error = e.Message;
        }
        catch (InstructionLimitException e)
        {
            result.ExitReason = VmExitReason.InstructionLimit;
            result.ReturnCode = 20;
            result.Error = e.Message;
        }
        catch (OperationCanceledException)
        {
            result.ExitReason = VmExitReason.Cancelled;
            result.ReturnCode = 20;
            result.Error = "Program execution was cancelled";
        }
        catch (Exception e) when (e is VmException or BusErrorException or IOException)
        {
            result.ExitReason = VmExitReason.Error;
            result.ReturnCode = 20;
            result.Error = e.Message;
        }
        finally
        {
            if (dos != null)
            {
                result.IoErr = dos.IoErrValue;
                dos.CloseAll();
            }

            if (context != null)
            {
                result.InstructionCount = context.Cpu.InstructionCount;
            }

            DeleteRamDirectory(ramDirectory);
        }

        return result;
    }

    private static async Task<byte[]> ReadAllBytes(string path, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(path);
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, 81920, cancellationToken);
        return memoryStream.ToArray();
    }

    private SegmentList LoadExecutable(byte[] executable)
    {
        var loader = new HunkLoader(context.Ram, context.Allocator);
        using var stream = new MemoryStream(executable);
        try
        {
            return loader.Load(stream).GetAwaiter().GetResult();
        }
        catch (IOException e)
        {
            throw new VmException($"Invalid Amiga executable: {e.Message}");
        }
    }

    private static Stream CreateConsoleInput()
    {
        var input = Console.OpenStandardInput();
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new CarriageReturnFilterStream(input) : input;
    }

    /// <summary>
    /// Setup volumes and assigns from options and defaults.
    /// </summary>
    /// <returns>Host directory created for RAM: volume, if any.</returns>
    private string SetupVolumes(VolumeManager volumes, string programDirectory)
    {
        foreach (var volume in options.Volumes)
        {
            volumes.AddVolume(volume.Key, volume.Value);
        }

        if (!volumes.VolumeNames.Any())
        {
            volumes.AddVolume("SYS", Directory.GetCurrentDirectory());
        }

        string ramDirectory = null;
        if (!volumes.HasVolume("RAM"))
        {
            ramDirectory = Path.Combine(Path.GetTempPath(), $"hst-amiga-vm-ram-{Guid.NewGuid():N}");
            Directory.CreateDirectory(ramDirectory);
            Directory.CreateDirectory(Path.Combine(ramDirectory, "T"));
            Directory.CreateDirectory(Path.Combine(ramDirectory, "Env"));
            volumes.AddVolume("RAM", ramDirectory);
        }

        var assigns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (volumes.HasVolume("SYS"))
        {
            foreach (var (name, path) in DefaultSysAssigns)
            {
                assigns[name] = path;
            }
        }

        assigns["T"] = "RAM:T";
        assigns["ENV"] = "RAM:Env";

        if (programDirectory != null && !options.Assigns.ContainsKey("PROGDIR") && !volumes.HasVolume("PROGDIR"))
        {
            var programPath = FindAmigaPath(volumes, programDirectory);
            if (programPath != null)
            {
                assigns["PROGDIR"] = programPath;
            }
            else
            {
                volumes.AddVolume("PROGDIR", programDirectory);
            }
        }

        foreach (var assign in options.Assigns)
        {
            assigns[assign.Key.TrimEnd(':')] = assign.Value;
        }

        foreach (var assign in assigns)
        {
            foreach (var path in assign.Value.Split(';').Where(x => x.Length > 0))
            {
                volumes.AddAssign(assign.Key, path);
            }
        }

        return ramDirectory;
    }

    /// <summary>
    /// Find Amiga path of host directory, if it's within a volume.
    /// </summary>
    private static string FindAmigaPath(VolumeManager volumes, string hostDirectory)
    {
        var fullPath = Path.GetFullPath(hostDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        foreach (var volume in volumes.VolumeNames.Where(x => !string.Equals(x, "RAM", StringComparison.OrdinalIgnoreCase)))
        {
            var root = volumes.GetVolumeHostPath(volume).TrimEnd(Path.DirectorySeparatorChar);
            if (string.Equals(fullPath, root, comparison))
            {
                return $"{volume}:";
            }

            if (fullPath.StartsWith(root + Path.DirectorySeparatorChar, comparison))
            {
                var relative = fullPath.Substring(root.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
                return $"{volume}:{relative}";
            }
        }

        return null;
    }

    private AmigaPath FindCurrentDirectory(VolumeManager volumes)
    {
        var root = volumes.GetRoot("SYS") ?? volumes.GetRoot(volumes.VolumeNames.First());
        if (!string.IsNullOrEmpty(options.CurrentDirectory))
        {
            var path = volumes.Resolve(options.CurrentDirectory, root);
            if (path == null || !Directory.Exists(path.HostPath))
            {
                throw new VmException($"Current directory '{options.CurrentDirectory}' doesn't exist");
            }

            return path;
        }

        var hostCurrentDirectory = FindAmigaPath(volumes, Directory.GetCurrentDirectory());
        return hostCurrentDirectory != null ? volumes.Resolve(hostCurrentDirectory, root) ?? root : root;
    }

    private static void DeleteRamDirectory(string ramDirectory)
    {
        if (ramDirectory == null)
        {
            return;
        }

        try
        {
            Directory.Delete(ramDirectory, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
