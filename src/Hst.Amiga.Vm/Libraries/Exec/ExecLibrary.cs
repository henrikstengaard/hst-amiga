namespace Hst.Amiga.Vm.Libraries.Exec;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Processor;
using Structs;

/// <summary>
/// Emulated exec.library with memory, list, signal, message port and library functions for a single task.
/// </summary>
public class ExecLibrary : Library
{
    public const uint SigBreakCtrlC = 1 << 12;
    public const uint SigBreakCtrlD = 1 << 13;
    public const uint SigBreakCtrlE = 1 << 14;
    public const uint SigBreakCtrlF = 1 << 15;

    private const ushort AttnFlags68010 = 1 << 0;
    private const ushort AttnFlags68020 = 1 << 1;

    /// <summary>
    /// Kickstart revision in ExecBase SoftVer, Kickstart 3.1 is 40.68.
    /// </summary>
    public const ushort KickstartRevision = 68;

    private readonly Dictionary<uint, HashSet<uint>> pools = new();

    public ExecLibrary(VmContext context) : base(context)
    {
    }

    public override string Name => "exec.library";
    public override ushort Version => 40;
    public override ushort Revision => 10;
    public override string IdString => "exec 40.10 (15.7.93)";
    protected override int PositiveSize => ExecBaseStruct.Size;
    protected override string[] FunctionNames => ExecFunctionNames.Names;

    /// <summary>
    /// Address of task executing program.
    /// </summary>
    public uint ThisTask
    {
        get => Ram.ReadLong(BaseAddress + ExecBaseStruct.ThisTask);
        set => Ram.WriteLong(BaseAddress + ExecBaseStruct.ThisTask, value);
    }

    /// <summary>
    /// Output for debug functions RawPutChar.
    /// </summary>
    public Stream DebugOutput { get; set; }

    protected override void Initialize()
    {
        var execBase = BaseAddress;
        Ram.WriteLong(4, execBase);
        Ram.WriteWord(execBase + ExecBaseStruct.SoftVer, KickstartRevision);
        Ram.WriteLong(execBase + ExecBaseStruct.ChkBase, ~execBase);
        Ram.WriteLong(execBase + ExecBaseStruct.MaxLocMem, Ram.Size);
        Ram.WriteWord(execBase + ExecBaseStruct.AttnFlags,
            Cpu.Type == CpuType.M68020 ? (ushort)(AttnFlags68010 | AttnFlags68020) : (ushort)0);
        Ram.WriteLong(execBase + ExecBaseStruct.TaskSigAlloc, 0xFFFF);
        Ram.WriteWord(execBase + ExecBaseStruct.TaskTrapAlloc, 0x8000);
        Ram.WriteByte(execBase + ExecBaseStruct.VBlankFrequency, 50);
        Ram.WriteByte(execBase + ExecBaseStruct.PowerSupplyFrequency, 50);
        Ram.WriteLong(execBase + ExecBaseStruct.EClockFrequency, 709379);

        foreach (var list in new[]
                 {
                     ExecBaseStruct.MemList, ExecBaseStruct.ResourceList, ExecBaseStruct.DeviceList,
                     ExecBaseStruct.IntrList, ExecBaseStruct.LibList, ExecBaseStruct.PortList,
                     ExecBaseStruct.TaskReady, ExecBaseStruct.TaskWait, ExecBaseStruct.SemaphoreList
                 })
        {
            ExecLists.NewList(Ram, execBase + (uint)list);
        }

        Ram.WriteByte(execBase + ExecBaseStruct.LibList + ListStruct.Type, NodeTypes.Library);
        Ram.WriteByte(execBase + ExecBaseStruct.PortList + ListStruct.Type, NodeTypes.MsgPort);
        Ram.WriteByte(execBase + ExecBaseStruct.MemList + ListStruct.Type, NodeTypes.Memory);
        ExecLists.NewList(Ram, execBase + ExecBaseStruct.MemHandlers);

        AddLibraryNode(this);
    }

    /// <summary>
    /// Add library to exec library list.
    /// </summary>
    public void AddLibraryNode(Library library)
    {
        ExecLists.AddTail(Ram, BaseAddress + ExecBaseStruct.LibList, library.BaseAddress);
    }

    // struct Resident offsets
    private const int RtMatchWord = 0;
    private const int RtMatchTag = 2;
    private const int RtEndSkip = 6;
    private const int RtFlags = 10;
    private const int RtVersion = 11;
    private const int RtType = 12;
    private const int RtName = 14;
    private const int RtIdString = 18;
    private const int ResidentSize = 26;
    private const ushort RtcMatchWord = 0x4AFC;
    private const byte RtfAutoInit = 0x80;

    /// <summary>
    /// Create resident modules in ExecBase ResModules for libraries like in Kickstart ROM. Used by programs like
    /// Version finding resident modules by name.
    /// </summary>
    public void SetResidentModules(IEnumerable<Library> libraries)
    {
        var residents = new List<uint>();
        foreach (var library in libraries.Where(x => x.IsInstalled && x is not StubLibrary))
        {
            var resident = Context.Allocator.Allocate(ResidentSize);
            if (resident == 0)
            {
                throw new VmException("Not enough memory to create resident modules");
            }

            Ram.WriteWord(resident + RtMatchWord, RtcMatchWord);
            Ram.WriteLong(resident + RtMatchTag, resident);
            Ram.WriteLong(resident + RtEndSkip, resident + ResidentSize);
            Ram.WriteByte(resident + RtFlags, RtfAutoInit);
            Ram.WriteByte(resident + RtVersion, (byte)library.Version);
            Ram.WriteByte(resident + RtType, NodeTypes.Library);
            Ram.WriteLong(resident + RtName, Ram.ReadLong(library.BaseAddress + NodeStruct.Name));
            Ram.WriteLong(resident + RtIdString, Ram.ReadLong(library.BaseAddress + LibraryStruct.IdString));
            residents.Add(resident);
        }

        // array of resident module pointers terminated by zero
        var array = Context.Allocator.Allocate((uint)(residents.Count + 1) * 4);
        if (array == 0)
        {
            throw new VmException("Not enough memory to create resident modules");
        }

        for (var i = 0; i < residents.Count; i++)
        {
            Ram.WriteLong(array + (uint)i * 4, residents[i]);
        }

        Ram.WriteLong(array + (uint)residents.Count * 4, 0);
        Ram.WriteLong(BaseAddress + ExecBaseStruct.ResModules, array);
    }

    /// <summary>
    /// Find resident module by name in ResModules.
    /// </summary>
    private uint FindResident(string name)
    {
        var entry = Ram.ReadLong(BaseAddress + ExecBaseStruct.ResModules);
        while (entry != 0)
        {
            var resident = Ram.ReadLong(entry);
            if (resident == 0)
            {
                break;
            }

            // high bit set is a link to next array
            if ((resident & 0x80000000) != 0)
            {
                entry = resident & 0x7FFFFFFF;
                continue;
            }

            if (string.Equals(Ram.ReadCString(Ram.ReadLong(resident + RtName)), name, StringComparison.Ordinal))
            {
                return resident;
            }

            entry += 4;
        }

        return 0;
    }

    protected override void RegisterFunctions()
    {
        // interrupts and multitasking aren't emulated, single task program runs without interruption
        Register("Disable", () => { });
        Register("Enable", () => { });
        Register("Forbid", () => { });
        Register("Permit", () => { });
        Register("Supervisor", Supervisor);
        Register("SuperState", SuperState);
        Register("UserState", UserState);
        Register("SetSR", () =>
        {
            var oldSr = Cpu.Sr;
            var mask = D1 & 0x071F; // condition codes and interrupt mask, supervisor bit can't be changed
            Cpu.Sr = (ushort)((oldSr & ~mask) | (D0 & mask));
            D0 = oldSr;
        });
        Register("GetCC", () => D0 = Cpu.Ccr);
        Register("Alert", () => throw new VmException($"Alert ${D7:X8}"));
        Register("ColdReboot", () => throw new ProgramExitException(20, true));
        Register("Debug", () => { });
        Register("RawIOInit", () => { });
        Register("RawMayGetChar", () => D0 = 0xFFFFFFFF);
        Register("RawPutChar", () => DebugOutput?.WriteByte((byte)D0));
        Register("FindResident", () => D0 = FindResident(Ram.ReadCString(A1) ?? string.Empty));
        Register("OpenResource", () => D0 = 0);
        Register("SumKickData", () => D0 = 0);
        Register("SumLibrary", () => { });
        Register("CacheClearU", () => { });
        Register("CacheClearE", () => { });
        Register("CacheControl", () =>
        {
            var oldCacr = Cpu.Cacr;
            Cpu.Cacr = (oldCacr & ~D1) | (D0 & D1);
            D0 = oldCacr;
        });
        Register("CachePreDMA", () => D0 = A0);
        Register("CachePostDMA", () => { });
        Register("AddMemHandler", () => { });
        Register("RemMemHandler", () => { });
        Register("ObtainQuickVector", () => D0 = 0);

        // memory
        Register("AllocMem", () => D0 = Context.Allocator.Allocate(D0));
        Register("AllocAbs", () => D0 = Context.Allocator.AllocateAbsolute(A1, D0));
        Register("FreeMem", () => FreeMemory(A1));
        Register("AvailMem", AvailMem);
        Register("TypeOfMem", () =>
            D0 = A1 >= VmContext.HeapStart && A1 < Ram.Size ? MemoryFlags.Public | MemoryFlags.Fast : 0);
        Register("AllocVec", () => D0 = AllocVec(D0));
        Register("FreeVec", () =>
        {
            if (A1 != 0)
            {
                FreeMemory(A1 - 8);
            }
        });
        Register("CopyMem", () => Ram.Copy(A0, A1, D0));
        Register("CopyMemQuick", () => Ram.Copy(A0, A1, D0));
        Register("CreatePool", CreatePool);
        Register("DeletePool", DeletePool);
        Register("AllocPooled", AllocPooled);
        Register("FreePooled", FreePooled);
        Register("StackSwap", StackSwap);

        // lists
        Register("Insert", () => ExecLists.Insert(Ram, A0, A1, A2));
        Register("AddHead", () => ExecLists.AddHead(Ram, A0, A1));
        Register("AddTail", () => ExecLists.AddTail(Ram, A0, A1));
        Register("Remove", () => ExecLists.Remove(Ram, A1));
        Register("RemHead", () => D0 = ExecLists.RemHead(Ram, A0));
        Register("RemTail", () => D0 = ExecLists.RemTail(Ram, A0));
        Register("Enqueue", () => ExecLists.Enqueue(Ram, A0, A1));
        Register("FindName", () => D0 = ExecLists.FindName(Ram, A0, Ram.ReadCString(A1)));

        // tasks and signals
        Register("FindTask", FindTask);
        Register("SetTaskPri", () =>
        {
            var oldPriority = (sbyte)Ram.ReadByte(A1 + NodeStruct.Pri);
            Ram.WriteByte(A1 + NodeStruct.Pri, (byte)D0);
            D0 = (uint)oldPriority;
        });
        Register("SetSignal", () =>
        {
            var oldSignals = SignalsReceived;
            SignalsReceived = (oldSignals & ~D1) | (D0 & D1);
            D0 = oldSignals;
        });
        Register("SetExcept", () =>
        {
            var task = ThisTask;
            var oldSignals = Ram.ReadLong(task + TaskStruct.SigExcept);
            Ram.WriteLong(task + TaskStruct.SigExcept, (oldSignals & ~D1) | (D0 & D1));
            D0 = oldSignals;
        });
        Register("Wait", Wait);
        Register("Signal", () =>
        {
            if (A1 == ThisTask)
            {
                SignalsReceived |= D0;
            }
        });
        Register("AllocSignal", () => D0 = (uint)AllocSignal((sbyte)D0));
        Register("FreeSignal", () => FreeSignal((sbyte)D0));
        Register("AllocTrap", () => D0 = (uint)AllocTrap((sbyte)D0));
        Register("FreeTrap", () =>
        {
            var trap = (sbyte)D0;
            if (trap is >= 0 and < 16)
            {
                var task = ThisTask;
                Ram.WriteWord(task + TaskStruct.TrapAlloc,
                    (ushort)(Ram.ReadWord(task + TaskStruct.TrapAlloc) & ~(1 << trap)));
            }
        });

        // message ports
        Register("AddPort", () =>
        {
            Ram.WriteByte(A1 + NodeStruct.Type, NodeTypes.MsgPort);
            ExecLists.NewList(Ram, A1 + MsgPortStruct.MsgList);
            ExecLists.Enqueue(Ram, BaseAddress + ExecBaseStruct.PortList, A1);
        });
        Register("RemPort", () => ExecLists.Remove(Ram, A1));
        Register("FindPort", () =>
            D0 = ExecLists.FindName(Ram, BaseAddress + ExecBaseStruct.PortList, Ram.ReadCString(A1)));
        Register("PutMsg", () => PutMsg(A0, A1));
        Register("GetMsg", () => D0 = ExecLists.RemHead(Ram, A0 + MsgPortStruct.MsgList));
        Register("ReplyMsg", ReplyMsg);
        Register("WaitPort", () =>
        {
            if (ExecLists.IsEmpty(Ram, A0 + MsgPortStruct.MsgList))
            {
                throw new VmException(
                    "WaitPort() on empty message port would wait forever, multitasking is not supported");
            }

            D0 = Ram.ReadLong(A0 + MsgPortStruct.MsgList + ListStruct.Head);
        });
        Register("CreateMsgPort", CreateMsgPort);
        Register("DeleteMsgPort", () => DeleteMsgPort(A0));
        Register("CreateIORequest", () =>
        {
            if (A0 == 0)
            {
                D0 = 0;
                return;
            }

            var ioRequest = Context.Allocator.Allocate(Math.Max(D0, MessageStruct.Size));
            if (ioRequest != 0)
            {
                Ram.WriteByte(ioRequest + NodeStruct.Type, NodeTypes.ReplyMsg);
                Ram.WriteLong(ioRequest + MessageStruct.ReplyPort, A0);
                Ram.WriteWord(ioRequest + MessageStruct.Length, (ushort)D0);
            }

            D0 = ioRequest;
        });
        Register("DeleteIORequest", () =>
        {
            if (A0 != 0)
            {
                Context.Allocator.Free(A0);
            }
        });

        // devices are not supported
        Register("OpenDevice", () =>
        {
            if (A1 != 0)
            {
                Ram.WriteByte(A1 + IoRequestStruct.Error, 0xFF); // IOERR_OPENFAIL
            }

            D0 = 0xFFFFFFFF;
        });
        Register("CloseDevice", () => { });

        // libraries
        Register("OpenLibrary", () => D0 = OpenLibrary(Ram.ReadCString(A1), D0));
        Register("OldOpenLibrary", () => D0 = OpenLibrary(Ram.ReadCString(A1), 0));
        Register("CloseLibrary", CloseLibrary);
        Register("SetFunction", SetFunction);
        Register("AddLibrary", () => ExecLists.Enqueue(Ram, BaseAddress + ExecBaseStruct.LibList, A1));
        Register("RemLibrary", () => D0 = 0);

        // semaphores are not needed for single task
        Register("InitSemaphore", () =>
        {
            Ram.WriteByte(A0 + NodeStruct.Type, NodeTypes.SignalSem);
        });
        Register("ObtainSemaphore", () => { });
        Register("ObtainSemaphoreShared", () => { });
        Register("ReleaseSemaphore", () => { });
        Register("AttemptSemaphore", () => D0 = 1);
        Register("AttemptSemaphoreShared", () => D0 = 1);
        Register("ObtainSemaphoreList", () => { });
        Register("ReleaseSemaphoreList", () => { });
        Register("AddSemaphore", () =>
        {
            Ram.WriteByte(A1 + NodeStruct.Type, NodeTypes.SignalSem);
            ExecLists.Enqueue(Ram, BaseAddress + ExecBaseStruct.SemaphoreList, A1);
        });
        Register("RemSemaphore", () => ExecLists.Remove(Ram, A1));
        Register("FindSemaphore", () =>
            D0 = ExecLists.FindName(Ram, BaseAddress + ExecBaseStruct.SemaphoreList, Ram.ReadCString(A1)));

        // formatting
        Register("RawDoFmt", RawDoFmt);
    }

    /// <summary>
    /// Signals received by task including break signals sent to program.
    /// </summary>
    public uint SignalsReceived
    {
        get
        {
            var task = ThisTask;
            var signals = Ram.ReadLong(task + TaskStruct.SigRecvd) | Context.TakeBreakSignals();
            Ram.WriteLong(task + TaskStruct.SigRecvd, signals);
            return signals;
        }
        set => Ram.WriteLong(ThisTask + TaskStruct.SigRecvd, value);
    }

    private void FreeMemory(uint address)
    {
        if (address == 0)
        {
            return;
        }

        if (!Context.Allocator.Free(address))
        {
            Context.Log($"Warning: free of memory ${address:X8}, which is not allocated");
        }
    }

    private void AvailMem()
    {
        var requirements = D1;
        if ((requirements & MemoryFlags.Total) != 0)
        {
            D0 = Context.Allocator.Total;
        }
        else if ((requirements & MemoryFlags.Largest) != 0)
        {
            D0 = Context.Allocator.Largest;
        }
        else
        {
            D0 = Context.Allocator.Available;
        }
    }

    /// <summary>
    /// Allocate memory remembering size before memory returned.
    /// </summary>
    public uint AllocVec(uint size)
    {
        if (size == 0)
        {
            return 0;
        }

        var memory = Context.Allocator.Allocate(size + 8);
        if (memory == 0)
        {
            return 0;
        }

        Ram.WriteLong(memory + 4, size + 8);
        return memory + 8;
    }

    public void FreeVec(uint address)
    {
        if (address != 0)
        {
            FreeMemory(address - 8);
        }
    }

    private void CreatePool()
    {
        var pool = Context.Allocator.Allocate(16);
        if (pool != 0)
        {
            pools[pool] = new HashSet<uint>();
        }

        D0 = pool;
    }

    private void DeletePool()
    {
        if (A0 == 0 || !pools.TryGetValue(A0, out var allocations))
        {
            return;
        }

        foreach (var allocation in allocations)
        {
            Context.Allocator.Free(allocation);
        }

        pools.Remove(A0);
        Context.Allocator.Free(A0);
    }

    private void AllocPooled()
    {
        if (!pools.TryGetValue(A0, out var allocations))
        {
            D0 = 0;
            return;
        }

        var memory = Context.Allocator.Allocate(D0);
        if (memory != 0)
        {
            allocations.Add(memory);
        }

        D0 = memory;
    }

    private void FreePooled()
    {
        if (pools.TryGetValue(A0, out var allocations) && allocations.Remove(A1))
        {
            Context.Allocator.Free(A1);
        }
    }

    private void StackSwap()
    {
        var stackSwap = A0;
        var task = ThisTask;

        // return address of caller is on old stack
        var returnAddress = Cpu.Pop32();
        var oldLower = Ram.ReadLong(task + TaskStruct.SpLower);
        var oldUpper = Ram.ReadLong(task + TaskStruct.SpUpper);
        var oldPointer = Cpu.A[7];

        Ram.WriteLong(task + TaskStruct.SpLower, Ram.ReadLong(stackSwap + StackSwapStruct.Lower));
        Ram.WriteLong(task + TaskStruct.SpUpper, Ram.ReadLong(stackSwap + StackSwapStruct.Upper));
        Cpu.A[7] = Ram.ReadLong(stackSwap + StackSwapStruct.Pointer);

        Ram.WriteLong(stackSwap + StackSwapStruct.Lower, oldLower);
        Ram.WriteLong(stackSwap + StackSwapStruct.Upper, oldUpper);
        Ram.WriteLong(stackSwap + StackSwapStruct.Pointer, oldPointer);

        Cpu.Push32(returnAddress);
    }

    private void Supervisor()
    {
        // build exception stack frame returning to rts in jump table, then continue at user function in a5
        var oldSr = Cpu.Sr;
        var returnPc = Cpu.PC;
        Cpu.Sr = (ushort)(oldSr | 0x2000);
        if (Cpu.Type != CpuType.M68000)
        {
            Cpu.Push16(0x20); // format 0, vector 8
        }

        Cpu.Push32(returnPc);
        Cpu.Push16(oldSr);
        Cpu.PC = A5;
    }

    private void SuperState()
    {
        if (Cpu.Supervisor)
        {
            D0 = 0;
            return;
        }

        // supervisor mode continues using user stack
        var stack = Cpu.A[7];
        var oldSsp = Cpu.Ssp;
        Cpu.Sr = (ushort)(Cpu.Sr | 0x2000);
        Cpu.A[7] = stack;
        D0 = oldSsp;
    }

    private void UserState()
    {
        var stack = Cpu.A[7];
        var sysStack = D0;
        Cpu.Sr = (ushort)(Cpu.Sr & ~0x2000);
        Cpu.A[7] = stack;
        Cpu.Ssp = sysStack;
    }

    private void FindTask()
    {
        if (A1 == 0)
        {
            D0 = ThisTask;
            return;
        }

        var name = Ram.ReadCString(A1);
        var task = ThisTask;
        D0 = name == Ram.ReadCString(Ram.ReadLong(task + NodeStruct.Name)) ? task : 0;
    }

    private void Wait()
    {
        var signalSet = D0;
        var received = SignalsReceived & signalSet;
        if (received == 0)
        {
            throw new VmException(
                $"Wait() for signals ${signalSet:X8} not received would wait forever, multitasking is not supported");
        }

        SignalsReceived &= ~received;
        D0 = received;
    }

    public int AllocSignal(int signal)
    {
        var task = ThisTask;
        var allocated = Ram.ReadLong(task + TaskStruct.SigAlloc);
        if (signal < 0)
        {
            for (var i = 31; i >= 0; i--)
            {
                if ((allocated & (1u << i)) == 0)
                {
                    signal = i;
                    break;
                }
            }

            if (signal < 0)
            {
                return -1;
            }
        }
        else if (signal > 31 || (allocated & (1u << signal)) != 0)
        {
            return -1;
        }

        Ram.WriteLong(task + TaskStruct.SigAlloc, allocated | (1u << signal));
        SignalsReceived &= ~(1u << signal);
        return signal;
    }

    public void FreeSignal(int signal)
    {
        if (signal is < 0 or > 31)
        {
            return;
        }

        var task = ThisTask;
        Ram.WriteLong(task + TaskStruct.SigAlloc, Ram.ReadLong(task + TaskStruct.SigAlloc) & ~(1u << signal));
    }

    private int AllocTrap(int trap)
    {
        var task = ThisTask;
        var allocated = Ram.ReadWord(task + TaskStruct.TrapAlloc);
        if (trap < 0)
        {
            for (var i = 15; i >= 0; i--)
            {
                if ((allocated & (1 << i)) == 0)
                {
                    trap = i;
                    break;
                }
            }

            if (trap < 0)
            {
                return -1;
            }
        }
        else if (trap > 15 || (allocated & (1 << trap)) != 0)
        {
            return -1;
        }

        Ram.WriteWord(task + TaskStruct.TrapAlloc, (ushort)(allocated | (1 << trap)));
        return trap;
    }

    /// <summary>
    /// Initialize message port in memory.
    /// </summary>
    public void InitMsgPort(uint port, int signal)
    {
        Ram.WriteByte(port + NodeStruct.Type, NodeTypes.MsgPort);
        Ram.WriteByte(port + MsgPortStruct.Flags, 0); // PA_SIGNAL
        Ram.WriteByte(port + MsgPortStruct.SigBit, (byte)signal);
        Ram.WriteLong(port + MsgPortStruct.SigTask, ThisTask);
        ExecLists.NewList(Ram, port + MsgPortStruct.MsgList);
        Ram.WriteByte(port + MsgPortStruct.MsgList + ListStruct.Type, NodeTypes.Message);
    }

    private void CreateMsgPort()
    {
        var signal = AllocSignal(-1);
        if (signal < 0)
        {
            D0 = 0;
            return;
        }

        var port = Context.Allocator.Allocate(MsgPortStruct.Size);
        if (port == 0)
        {
            FreeSignal(signal);
            D0 = 0;
            return;
        }

        InitMsgPort(port, signal);
        D0 = port;
    }

    private void DeleteMsgPort(uint port)
    {
        if (port == 0)
        {
            return;
        }

        FreeSignal((sbyte)Ram.ReadByte(port + MsgPortStruct.SigBit));
        Context.Allocator.Free(port);
    }

    private void PutMsg(uint port, uint message)
    {
        Ram.WriteByte(message + NodeStruct.Type, NodeTypes.Message);
        ExecLists.AddTail(Ram, port + MsgPortStruct.MsgList, message);

        // signal task, if port signals task waiting
        if ((Ram.ReadByte(port + MsgPortStruct.Flags) & 3) == 0 &&
            Ram.ReadLong(port + MsgPortStruct.SigTask) == ThisTask)
        {
            SignalsReceived |= 1u << (Ram.ReadByte(port + MsgPortStruct.SigBit) & 31);
        }
    }

    private void ReplyMsg()
    {
        var message = A1;
        if (message == 0)
        {
            return;
        }

        var replyPort = Ram.ReadLong(message + MessageStruct.ReplyPort);
        if (replyPort == 0)
        {
            Ram.WriteByte(message + NodeStruct.Type, NodeTypes.FreeMsg);
            return;
        }

        PutMsg(replyPort, message);
        Ram.WriteByte(message + NodeStruct.Type, NodeTypes.ReplyMsg);
    }

    /// <summary>
    /// Open library by name, ignoring path.
    /// </summary>
    /// <returns>Library base or zero, if library doesn't exist or version is too low.</returns>
    public uint OpenLibrary(string name, uint version)
    {
        if (string.IsNullOrEmpty(name))
        {
            return 0;
        }

        var separator = Math.Max(name.LastIndexOf(':'), name.LastIndexOf('/'));
        if (separator >= 0)
        {
            name = name.Substring(separator + 1);
        }

        var library = Context.FindLibrary(name);
        if (library == null || library.Version < version)
        {
            if (library == null)
            {
                Context.Log($"Warning: OpenLibrary(\"{name}\", {version}) failed, library is not available");
            }

            return 0;
        }

        Ram.WriteWord(library.BaseAddress + LibraryStruct.OpenCnt,
            (ushort)(Ram.ReadWord(library.BaseAddress + LibraryStruct.OpenCnt) + 1));
        return library.BaseAddress;
    }

    private void CloseLibrary()
    {
        var library = A1;
        if (library == 0)
        {
            return;
        }

        var openCount = Ram.ReadWord(library + LibraryStruct.OpenCnt);
        if (openCount > 0)
        {
            Ram.WriteWord(library + LibraryStruct.OpenCnt, (ushort)(openCount - 1));
        }

        D0 = 0;
    }

    private void SetFunction()
    {
        var library = A1;
        var offset = (int)D0;
        var newFunction = A0;
        var slot = (uint)(library + offset);

        // old function is jump target, if slot is patched, otherwise slot itself calling trap
        var oldFunction = Ram.ReadWord(slot) == 0x4EF9 ? Ram.ReadLong(slot + 2) : slot;

        Ram.WriteWord(slot, 0x4EF9); // jmp abs.l
        Ram.WriteLong(slot + 2, newFunction);
        Ram.WriteByte(library + LibraryStruct.Flags, (byte)(Ram.ReadByte(library + LibraryStruct.Flags) | 0x02));
        D0 = oldFunction;
    }

    private void RawDoFmt()
    {
        var format = A0;
        var data = A1;
        var putChProc = A2;
        var putChData = A3;

        var output = RawDoFmtFormatter.Format(Ram, format, data, out var nextData);

        // emit formatted string including zero termination
        var bytes = new byte[output.Length + 1];
        Array.Copy(output, bytes, output.Length);
        WriteFormatted(bytes, putChProc, putChData);

        D0 = nextData;
    }

    /// <summary>
    /// Write formatted bytes using put char procedure called with character in D0 and data in A3.
    /// Put char procedure "move.b d0,(a3)+; rts" and zero are written directly to buffer in A3.
    /// </summary>
    internal void WriteFormatted(byte[] bytes, uint putChProc, uint putChData)
    {
        if (putChProc == 0 || (Ram.ReadWord(putChProc) == 0x16C0 && Ram.ReadWord(putChProc + 2) == 0x4E75))
        {
            Ram.WriteBytes(putChData, bytes);
            return;
        }

        var savedRegisters = new uint[16];
        Array.Copy(Cpu.D, savedRegisters, 8);
        Array.Copy(Cpu.A, 0, savedRegisters, 8, 8);

        Cpu.A[3] = putChData;
        foreach (var b in bytes)
        {
            Cpu.D[0] = b;
            Context.CallSubroutine(putChProc);
        }

        Array.Copy(savedRegisters, Cpu.D, 8);
        Array.Copy(savedRegisters, 8, Cpu.A, 0, 8);
    }
}
