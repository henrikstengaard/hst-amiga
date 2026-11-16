namespace Hst.Amiga.Vm.Libraries.Dos;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exec;
using FileSystem;
using Loader;
using Structs;

/// <summary>
/// Emulated dos.library mapping files, directories and console to host file system and streams.
/// </summary>
public partial class DosLibrary : Library
{
    public const int DosTrue = -1;
    public const int DosFalse = 0;

    public const int ModeOldFile = 1005;
    public const int ModeNewFile = 1006;
    public const int ModeReadWrite = 1004;

    public const int SharedLock = -2;
    public const int ExclusiveLock = -1;

    public const int OffsetBeginning = -1;
    public const int OffsetCurrent = 0;
    public const int OffsetEnd = 1;

    public const int StFile = -3;
    public const int StUserDir = 2;
    public const int StRoot = 1;

    private readonly ExecLibrary exec;
    private readonly VolumeManager volumes;
    private readonly HunkLoader loader;
    private readonly Dictionary<uint, DosFile> files = new();
    private readonly Dictionary<uint, DosLock> locks = new();
    private readonly Dictionary<string, uint> protections = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> comments = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Stream> consoleOutputs = new();

    private uint dummyPort;
    private Stream consoleInput;
    private Stream consoleOutput;
    private Stream consoleError;
    private bool interactive;

    public DosLibrary(VmContext context, ExecLibrary exec, VolumeManager volumes) : base(context)
    {
        this.exec = exec;
        this.volumes = volumes;
        loader = new HunkLoader(context.Ram, context.Allocator);
    }

    public override string Name => "dos.library";
    public override ushort Version => 40;
    public override ushort Revision => 3;
    public override string IdString => "dos 40.3 (1.4.93)";
    protected override int PositiveSize => DosLibraryStruct.Size;
    protected override string[] FunctionNames => DosFunctionNames.Names;

    /// <summary>
    /// Address of process running program.
    /// </summary>
    public uint Process { get; private set; }

    /// <summary>
    /// Address of command line interface structure of process.
    /// </summary>
    public uint CliAddress => BAddr(Ram.ReadLong(Process + ProcessStruct.Cli));

    /// <summary>
    /// Dos error code of last failed function, returned by IoErr().
    /// </summary>
    public int IoErrValue
    {
        get => Process == 0 ? 0 : (int)Ram.ReadLong(Process + ProcessStruct.Result2);
        set
        {
            if (Process != 0)
            {
                Ram.WriteLong(Process + ProcessStruct.Result2, (uint)value);
            }
        }
    }

    protected override void Initialize()
    {
        // dummy message port used as file system and console task ports
        dummyPort = Context.Allocator.Allocate(MsgPortStruct.Size);
        Ram.WriteByte(dummyPort + NodeStruct.Type, NodeTypes.MsgPort);
        Ram.WriteByte(dummyPort + MsgPortStruct.Flags, 2); // PA_IGNORE
        ExecLists.NewList(Ram, dummyPort + MsgPortStruct.MsgList);

        var rootNode = Context.Allocator.Allocate(RootNodeStruct.Size);
        var dosInfo = Context.Allocator.Allocate(64);
        Ram.WriteLong(rootNode + RootNodeStruct.Info, MkBAddr(dosInfo));
        ExecLists.NewList(Ram, rootNode + RootNodeStruct.CliList);
        Ram.WriteLong(BaseAddress + DosLibraryStruct.Root, rootNode);

        exec.AddLibraryNode(this);
    }

    /// <summary>
    /// Create process running program with console input and output and current directory.
    /// </summary>
    public void SetupProcess(ProcessSetup setup)
    {
        consoleInput = setup.Input;
        consoleOutput = setup.Output;
        consoleError = setup.Error;
        interactive = setup.Interactive;
        consoleOutputs.Add(consoleOutput);
        consoleOutputs.Add(consoleError);

        var process = Context.Allocator.Allocate(ProcessStruct.Size);
        if (process == 0)
        {
            throw new VmException("Not enough memory to create process");
        }

        Process = process;
        exec.ThisTask = process;

        var programName = setup.ProgramName ?? string.Empty;
        Ram.WriteByte(process + NodeStruct.Type, NodeTypes.Process);
        Ram.WriteLong(process + NodeStruct.Name, AllocateString(programName));
        Ram.WriteByte(process + TaskStruct.State, 2); // TS_RUN
        Ram.WriteLong(process + TaskStruct.SigAlloc, 0xFFFF);
        Ram.WriteWord(process + TaskStruct.TrapAlloc, 0x8000);
        Ram.WriteLong(process + TaskStruct.SpLower, setup.StackLower);
        Ram.WriteLong(process + TaskStruct.SpUpper, setup.StackUpper);
        Ram.WriteLong(process + TaskStruct.SpReg, setup.StackUpper);
        ExecLists.NewList(Ram, process + TaskStruct.MemEntry);

        // process message port uses signal SIGB_DOS
        exec.InitMsgPort(process + ProcessStruct.MsgPort, 8);
        Ram.WriteLong(process + TaskStruct.SigAlloc, 0xFFFF | (1 << 8));

        // segment array with program segment list
        var segArray = Context.Allocator.Allocate(16);
        Ram.WriteLong(segArray, 4);
        Ram.WriteLong(segArray + 12, setup.SegList);
        Ram.WriteLong(process + ProcessStruct.SegList, MkBAddr(segArray));
        Ram.WriteLong(process + ProcessStruct.StackSize, setup.StackSize);
        Ram.WriteLong(process + ProcessStruct.StackBase, MkBAddr(setup.StackLower & ~3u));
        Ram.WriteLong(process + ProcessStruct.TaskNum, 1);
        Ram.WriteLong(process + ProcessStruct.ConsoleTask, dummyPort);
        Ram.WriteLong(process + ProcessStruct.FileSystemTask, dummyPort);
        Ram.WriteLong(process + ProcessStruct.ReturnAddr, setup.StackUpper - 4);

        Ram.WriteLong(process + ProcessStruct.CurrentDir,
            setup.CurrentDirectory == null ? 0 : CreateLock(setup.CurrentDirectory, SharedLock));
        Ram.WriteLong(process + ProcessStruct.HomeDir,
            setup.HomeDirectory == null ? 0 : CreateLock(setup.HomeDirectory, SharedLock));

        var cis = CreateConsoleHandle(consoleInput, consoleOutput);
        var cos = CreateConsoleHandle(consoleInput, consoleOutput);
        var ces = CreateConsoleHandle(consoleInput, consoleError);
        Ram.WriteLong(process + ProcessStruct.Cis, cis);
        Ram.WriteLong(process + ProcessStruct.Cos, cos);
        Ram.WriteLong(process + ProcessStruct.Ces, ces);

        Ram.WriteLong(process + ProcessStruct.Arguments, AllocateString(setup.Arguments ?? "\n"));
        ExecLists.NewList(Ram, process + ProcessStruct.LocalVars);

        // command line interface
        var cli = Context.Allocator.Allocate(CliStruct.Size);
        Ram.WriteLong(cli + CliStruct.SetName,
            MkBAddr(AllocateBString(setup.CurrentDirectory?.FullPath ?? string.Empty, 256)));
        Ram.WriteLong(cli + CliStruct.CommandName, MkBAddr(AllocateBString(programName, 256)));
        Ram.WriteLong(cli + CliStruct.Prompt, MkBAddr(AllocateBString("%N> ", 256)));
        Ram.WriteLong(cli + CliStruct.CommandFile, MkBAddr(AllocateBString(string.Empty, 256)));
        Ram.WriteLong(cli + CliStruct.FailLevel, 10);
        Ram.WriteLong(cli + CliStruct.StandardInput, cis);
        Ram.WriteLong(cli + CliStruct.CurrentInput, cis);
        Ram.WriteLong(cli + CliStruct.StandardOutput, cos);
        Ram.WriteLong(cli + CliStruct.CurrentOutput, cos);
        Ram.WriteLong(cli + CliStruct.Interactive, interactive ? unchecked((uint)DosTrue) : 0);
        Ram.WriteLong(cli + CliStruct.DefaultStack, setup.StackSize / 4);
        Ram.WriteLong(cli + CliStruct.Module, setup.SegList);
        Ram.WriteLong(process + ProcessStruct.Cli, MkBAddr(cli));
    }

    /// <summary>
    /// Flush and close all open files.
    /// </summary>
    public void CloseAll()
    {
        foreach (var file in files.Values)
        {
            file.Dispose();
        }

        files.Clear();
        FlushConsole();
    }

    /// <summary>
    /// Flush console output streams.
    /// </summary>
    public void FlushConsole()
    {
        foreach (var output in consoleOutputs.Where(x => x != null))
        {
            try
            {
                output.Flush();
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private void SetIoErr(int error) => IoErrValue = error;

    private DosFile GetFile(uint fileHandle)
    {
        if (fileHandle != 0 && files.TryGetValue(fileHandle, out var file))
        {
            return file;
        }

        SetIoErr(DosErrors.InvalidLock);
        return null;
    }

    private bool TryGetLock(uint lockBptr, out DosLock dosLock)
    {
        if (lockBptr == 0)
        {
            var root = ZeroLockPath;
            dosLock = root == null ? null : new DosLock(0, root, true, SharedLock);
            return dosLock != null;
        }

        if (locks.TryGetValue(lockBptr, out dosLock))
        {
            return true;
        }

        SetIoErr(DosErrors.InvalidLock);
        return false;
    }

    /// <summary>
    /// Path of zero lock, root of SYS: or first volume.
    /// </summary>
    private AmigaPath ZeroLockPath =>
        volumes.GetRoot("SYS") ?? volumes.VolumeNames.Select(x => volumes.GetRoot(x)).FirstOrDefault();

    /// <summary>
    /// Current directory of process.
    /// </summary>
    public AmigaPath CurrentDirectory
    {
        get
        {
            var currentDir = Process == 0 ? 0 : Ram.ReadLong(Process + ProcessStruct.CurrentDir);
            return currentDir != 0 && locks.TryGetValue(currentDir, out var dosLock) ? dosLock.Path : ZeroLockPath;
        }
    }

    /// <summary>
    /// Resolve Amiga path relative to current directory.
    /// </summary>
    public AmigaPath ResolvePath(string name) => volumes.Resolve(name ?? string.Empty, CurrentDirectory);

    private AmigaPath ResolveOrFail(string name)
    {
        var path = ResolvePath(name);
        if (path != null)
        {
            return path;
        }

        var colon = (name ?? string.Empty).IndexOf(':');
        SetIoErr(colon > 0 && !volumes.HasVolume(name.Substring(0, colon)) &&
                 !volumes.HasAssign(name.Substring(0, colon))
            ? DosErrors.DeviceNotMounted
            : DosErrors.ObjectNotFound);
        return null;
    }

    private static bool Exists(AmigaPath path) => File.Exists(path.HostPath) || Directory.Exists(path.HostPath);

    private uint CreateLock(AmigaPath path, int access)
    {
        var address = Context.Allocator.Allocate(FileLockStruct.Size);
        if (address == 0)
        {
            SetIoErr(DosErrors.NoFreeStore);
            return 0;
        }

        var bptr = MkBAddr(address);
        Ram.WriteLong(address + FileLockStruct.Key, (uint)(path.FullPath.GetHashCode() & 0x7FFFFFFF));
        Ram.WriteLong(address + FileLockStruct.Access, (uint)access);
        Ram.WriteLong(address + FileLockStruct.Task, dummyPort);
        locks[bptr] = new DosLock(address, path, Directory.Exists(path.HostPath), access);
        return bptr;
    }

    private void FreeLock(uint lockBptr)
    {
        if (lockBptr == 0 || !locks.TryGetValue(lockBptr, out var dosLock))
        {
            return;
        }

        locks.Remove(lockBptr);
        Context.Allocator.Free(dosLock.Address);
    }

    private uint CreateFileHandle(Func<uint, DosFile> createFile)
    {
        var address = Context.Allocator.Allocate(FileHandleStruct.Size);
        if (address == 0)
        {
            SetIoErr(DosErrors.NoFreeStore);
            return 0;
        }

        var bptr = MkBAddr(address);
        var file = createFile(address);
        files[bptr] = file;
        Ram.WriteLong(address + FileHandleStruct.Type, dummyPort);
        Ram.WriteLong(address + FileHandleStruct.Port, file.IsInteractive ? unchecked((uint)DosTrue) : 0);
        Ram.WriteLong(address + FileHandleStruct.Arg1, bptr);
        return bptr;
    }

    private uint CreateConsoleHandle(Stream input, Stream output) =>
        CreateFileHandle(address => new DosFile(address, input, output, true, interactive, null, false)
        {
            BeforeConsoleRead = FlushConsole
        });

    private void FreeFileHandle(uint fileHandle)
    {
        if (!files.TryGetValue(fileHandle, out var file))
        {
            return;
        }

        files.Remove(fileHandle);
        file.Dispose();
        Context.Allocator.Free(file.Address);
    }

    /// <summary>
    /// Map host exception to dos error code.
    /// </summary>
    private static int MapException(Exception exception) => exception switch
    {
        FileNotFoundException => DosErrors.ObjectNotFound,
        DirectoryNotFoundException => DosErrors.DirNotFound,
        UnauthorizedAccessException => DosErrors.WriteProtected,
        PathTooLongException => DosErrors.LineTooLong,
        IOException => DosErrors.ObjectInUse,
        _ => DosErrors.ObjectNotFound
    };

    private uint WriteDateStamp(uint address, DateTime date)
    {
        var amigaDate = DateHelper.ConvertToAmigaDate(date);
        Ram.WriteLong(address + DateStampStruct.Days, (uint)amigaDate.Days);
        Ram.WriteLong(address + DateStampStruct.Minute, (uint)amigaDate.Minutes);
        Ram.WriteLong(address + DateStampStruct.Tick, (uint)amigaDate.Ticks);
        return address;
    }

    private DateTime ReadDateStamp(uint address) =>
        DateHelper.ConvertToDate((int)Ram.ReadLong(address + DateStampStruct.Days),
            (int)Ram.ReadLong(address + DateStampStruct.Minute), (int)Ram.ReadLong(address + DateStampStruct.Tick));

    /// <summary>
    /// Write zero terminated string to buffer with max length including zero termination.
    /// </summary>
    /// <returns>True, if string fits in buffer.</returns>
    private bool WriteString(uint buffer, int length, string value)
    {
        if (length <= 0)
        {
            return false;
        }

        var bytes = Ram.GetBytes(value);
        var fits = bytes.Length < length;
        var count = Math.Min(bytes.Length, length - 1);
        Ram.WriteBytes(buffer, bytes, 0, count);
        Ram.WriteByte(buffer + (uint)count, 0);
        return fits;
    }

    private uint OutputHandle => Ram.ReadLong(Process + ProcessStruct.Cos);
    private uint InputHandle => Ram.ReadLong(Process + ProcessStruct.Cis);

    /// <summary>
    /// Write string to output of process.
    /// </summary>
    public void WriteOutput(string value)
    {
        var file = files.TryGetValue(OutputHandle, out var f) ? f : null;
        if (file == null || !file.CanWrite)
        {
            return;
        }

        var bytes = Ram.GetBytes(value);
        file.Write(bytes, 0, bytes.Length);
    }

    protected override void RegisterFunctions()
    {
        RegisterFileFunctions();
        RegisterLockFunctions();
        RegisterProcessFunctions();
        RegisterPatternFunctions();
        RegisterMiscFunctions();
    }
}
