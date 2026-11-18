namespace Hst.Amiga.Vm;

using System;
using System.Collections.Generic;
using System.Threading;
using Libraries;
using Processor;
using Roms;
using Traps;

/// <summary>
/// Context of virtual machine with memory, cpu, traps and libraries shared by emulated libraries.
/// </summary>
public sealed class VmContext
{
    /// <summary>
    /// Address of default exception handler. Exception vectors point to default exception handler.
    /// </summary>
    public const uint DefaultExceptionHandlerAddress = 0x400;

    /// <summary>
    /// Start address of memory available for allocations, memory below is used by exception vectors and stubs.
    /// </summary>
    public const uint HeapStart = 0x1000;

    private const uint ExitStubAddress = 0x404;
    private const uint CallbackStubAddress = 0x408;
    private const int CancellationCheckInterval = 0x10000;

    private readonly Dictionary<string, Library> libraries = new(StringComparer.OrdinalIgnoreCase);
    private bool callbackReturned;
    private int callDepth;
    private int pendingBreakSignals;

    public VmContext(VmOptions options, CancellationToken cancellationToken = default)
    {
        Options = options;
        CancellationToken = cancellationToken;
        var addressMask = options.CpuType == CpuType.M68000 ? 0x00FFFFFFu : 0xFFFFFFFFu;
        if (options.CpuType == CpuType.M68000 && options.MemorySize > 0x1000000)
        {
            throw new ArgumentException("Memory size for 68000 is max 16 MB");
        }

        var kickstartRom = options.KickstartRom;
        if (kickstartRom != null)
        {
            if (options.MemorySize > kickstartRom.BaseAddress)
            {
                throw new ArgumentException(
                    $"Memory size {options.MemorySize / 1024 / 1024} MB overlaps Kickstart ROM at 0x{kickstartRom.BaseAddress:X6}");
            }

            Ram = new Ram(options.MemorySize, addressMask, kickstartRom.BaseAddress, kickstartRom.Bytes);
        }
        else
        {
            Ram = new Ram(options.MemorySize, addressMask);
        }
        Cpu = new M68kCpu(Ram, options.CpuType)
        {
            DefaultExceptionHandler = DefaultExceptionHandlerAddress
        };
        Allocator = new MemoryAllocator(Ram, HeapStart, options.MemorySize);
        Traps = new TrapTable();
        Cpu.LineAHandler = (cpu, opcode) => Traps.Handle(cpu, opcode);

        // all exception vectors point to default exception handler
        for (var vector = 0U; vector < 256; vector++)
        {
            Ram.WriteLong(vector * 4, DefaultExceptionHandlerAddress);
        }

        // illegal instruction at default exception handler, never executed as cpu stops on default handler
        Ram.WriteWord(DefaultExceptionHandlerAddress, 0x4AFC);

        // stub returned to, when program returns from start code
        Ram.WriteWord(ExitStubAddress, Traps.Register("ExitStub", cpu =>
            throw new ProgramExitException((int)cpu.D[0], false)));

        // stub returned to, when subroutine called from emulated function returns
        Ram.WriteWord(CallbackStubAddress, Traps.Register("CallbackStub", _ => callbackReturned = true));

        if (options.TraceInstructions)
        {
            Cpu.Trace = cpu => Log($"{cpu.PC:X8}  {SafeReadWord(cpu.PC):X4}  {cpu.DumpRegisters().Replace(Environment.NewLine, " ")}");
        }
    }

    public VmOptions Options { get; }

    /// <summary>
    /// Kickstart ROM mapped read-only at its base address, if any.
    /// </summary>
    public KickstartRom KickstartRom => Options.KickstartRom;
    public CancellationToken CancellationToken { get; }
    public Ram Ram { get; }
    public M68kCpu Cpu { get; }
    public MemoryAllocator Allocator { get; }
    public TrapTable Traps { get; }

    /// <summary>
    /// Address of stub, which exits program when returned to.
    /// </summary>
    public uint ExitStub => ExitStubAddress;

    public IEnumerable<Library> Libraries => libraries.Values;

    public void AddLibrary(Library library)
    {
        libraries[library.Name] = library;
    }

    public Library FindLibrary(string name) => libraries.TryGetValue(name, out var library) ? library : null;

    public T GetLibrary<T>() where T : Library
    {
        foreach (var library in libraries.Values)
        {
            if (library is T typed)
            {
                return typed;
            }
        }

        return null;
    }

    public void Log(string message)
    {
        (Options.Log ?? (m => Console.Error.WriteLine(m)))(message);
    }

    private ushort SafeReadWord(uint address)
    {
        try
        {
            return Ram.ReadWord(address);
        }
        catch (BusErrorException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Send break signals, e.g. ctrl-c, to program. Thread safe.
    /// </summary>
    public void SendBreak(uint signals)
    {
        int initial, computed;
        do
        {
            initial = pendingBreakSignals;
            computed = initial | (int)signals;
        } while (Interlocked.CompareExchange(ref pendingBreakSignals, computed, initial) != initial);
    }

    /// <summary>
    /// Take pending break signals sent to program.
    /// </summary>
    public uint TakeBreakSignals() => (uint)Interlocked.Exchange(ref pendingBreakSignals, 0);

    /// <summary>
    /// Execute one instruction, checking cancellation and max instructions periodically.
    /// </summary>
    public void Step()
    {
        Cpu.Step();

        if (Options.MaxInstructions > 0 && Options.MaxInstructions < CancellationCheckInterval)
        {
            CheckInstructionLimit();
        }

        if (Cpu.InstructionCount % CancellationCheckInterval != 0)
        {
            return;
        }

        CancellationToken.ThrowIfCancellationRequested();
        CheckInstructionLimit();
    }

    private void CheckInstructionLimit()
    {
        if (Options.MaxInstructions > 0 && Cpu.InstructionCount >= Options.MaxInstructions)
        {
            throw new InstructionLimitException(Options.MaxInstructions);
        }
    }

    /// <summary>
    /// Run program until it exits.
    /// </summary>
    public void Run()
    {
        if (Options.MaxInstructions > 0 && Options.MaxInstructions < CancellationCheckInterval)
        {
            while (true)
            {
                Cpu.Step();
                CheckInstructionLimit();
            }
        }

        while (true)
        {
            Step();
        }
    }

    /// <summary>
    /// Call 68k subroutine from emulated function and run until it returns. Registers are set by caller.
    /// </summary>
    /// <returns>Value returned in D0.</returns>
    public uint CallSubroutine(uint address)
    {
        if (callDepth > 1000)
        {
            throw new VmException("Max nested subroutine calls exceeded");
        }

        var savedPc = Cpu.PC;
        Cpu.Push32(CallbackStubAddress);
        Cpu.PC = address;
        callDepth++;
        try
        {
            while (!callbackReturned)
            {
                Step();
            }
        }
        finally
        {
            callbackReturned = false;
            callDepth--;
        }

        Cpu.PC = savedPc;
        return Cpu.D[0];
    }
}
