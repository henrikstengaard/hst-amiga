namespace Hst.Amiga.Vm.Libraries;

using System;
using System.Collections.Generic;
using System.Linq;
using Processor;
using Structs;

/// <summary>
/// Base class for libraries emulated in C#. Installing a library allocates a library base with a jump table,
/// where each function slot contains a trap calling the C# function handler followed by rts.
/// </summary>
public abstract class Library
{
    /// <summary>
    /// Offset of first library function after standard functions open, close, expunge and reserved.
    /// </summary>
    public const int FirstFunctionOffset = 30;

    public const int JumpTableEntrySize = 6;

    private static readonly string[] StandardFunctionNames = { "Open", "Close", "Expunge", "Reserved" };

    private readonly Dictionary<string, Action> handlers = new(StringComparer.Ordinal);

    protected Library(VmContext context)
    {
        Context = context;
    }

    protected VmContext Context { get; }
    protected Ram Ram => Context.Ram;
    protected M68kCpu Cpu => Context.Cpu;

    public abstract string Name { get; }
    public abstract ushort Version { get; }
    public virtual ushort Revision => 0;
    public virtual string IdString => $"{Name.Replace(".library", string.Empty)} {Version}.{Revision}";

    /// <summary>
    /// Size of library base structure following jump table.
    /// </summary>
    protected virtual int PositiveSize => LibraryStruct.Size;

    /// <summary>
    /// Names of library functions in order of jump table starting at offset -30.
    /// </summary>
    protected abstract string[] FunctionNames { get; }

    /// <summary>
    /// Address of library base.
    /// </summary>
    public uint BaseAddress { get; private set; }

    public bool IsInstalled => BaseAddress != 0;

    /// <summary>
    /// Get library vector offset of function.
    /// </summary>
    public int GetOffset(string functionName)
    {
        var index = Array.IndexOf(FunctionNames, functionName);
        if (index < 0)
        {
            throw new ArgumentException($"Function '{functionName}' doesn't exist in {Name}");
        }

        return -(FirstFunctionOffset + index * JumpTableEntrySize);
    }

    /// <summary>
    /// Register handler for library function.
    /// </summary>
    protected void Register(string functionName, Action handler)
    {
        if (!StandardFunctionNames.Contains(functionName) && Array.IndexOf(FunctionNames, functionName) < 0)
        {
            throw new ArgumentException($"Function '{functionName}' doesn't exist in {Name}");
        }

        handlers[functionName] = handler;
    }

    /// <summary>
    /// Register library function handlers.
    /// </summary>
    protected abstract void RegisterFunctions();

    /// <summary>
    /// Initialize library base structure after library is installed.
    /// </summary>
    protected virtual void Initialize()
    {
    }

    /// <summary>
    /// Install library in memory.
    /// </summary>
    public void Install(uint? address = null)
    {
        if (IsInstalled)
        {
            return;
        }

        Register("Open", () =>
        {
            Ram.WriteWord(BaseAddress + LibraryStruct.OpenCnt,
                (ushort)(Ram.ReadWord(BaseAddress + LibraryStruct.OpenCnt) + 1));
            D0 = BaseAddress;
        });
        Register("Close", () =>
        {
            var openCount = Ram.ReadWord(BaseAddress + LibraryStruct.OpenCnt);
            if (openCount > 0)
            {
                Ram.WriteWord(BaseAddress + LibraryStruct.OpenCnt, (ushort)(openCount - 1));
            }

            D0 = 0;
        });
        Register("Expunge", () => D0 = 0);
        Register("Reserved", () => D0 = 0);
        RegisterFunctions();

        var functionCount = StandardFunctionNames.Length + FunctionNames.Length;
        var negativeSize = (uint)((functionCount * JumpTableEntrySize + 3) & ~3);
        var positiveSize = (uint)((PositiveSize + 3) & ~3);

        uint memory;
        if (address.HasValue)
        {
            memory = Context.Allocator.AllocateAbsolute(address.Value - negativeSize, negativeSize + positiveSize);
            if (memory != address.Value - negativeSize)
            {
                throw new VmException($"Unable to allocate {Name} at address ${address.Value:X8}");
            }
        }
        else
        {
            memory = Context.Allocator.Allocate(negativeSize + positiveSize);
        }

        if (memory == 0)
        {
            throw new VmException($"Not enough memory to install {Name}");
        }

        BaseAddress = memory + negativeSize;

        for (var i = 0; i < functionCount; i++)
        {
            var offset = (i + 1) * JumpTableEntrySize;
            var functionName = i < StandardFunctionNames.Length
                ? StandardFunctionNames[i]
                : FunctionNames[i - StandardFunctionNames.Length];
            var slot = BaseAddress - (uint)offset;
            var handler = handlers.TryGetValue(functionName, out var h) ? h : null;
            var trapOpcode = Context.Traps.Register($"{Name}/{functionName}",
                _ => Call(functionName, -offset, handler));

            Ram.WriteWord(slot, trapOpcode);
            Ram.WriteWord(slot + 2, 0x4E75); // rts
            Ram.WriteWord(slot + 4, 0x4E71); // nop
        }

        var nameAddress = AllocateString(Name);
        var idStringAddress = AllocateString(IdString);

        Ram.WriteByte(BaseAddress + NodeStruct.Type, NodeTypes.Library);
        Ram.WriteLong(BaseAddress + NodeStruct.Name, nameAddress);
        Ram.WriteByte(BaseAddress + LibraryStruct.Flags, 0x06); // LIBF_SUMUSED | LIBF_CHANGED
        Ram.WriteWord(BaseAddress + LibraryStruct.NegSize, (ushort)negativeSize);
        Ram.WriteWord(BaseAddress + LibraryStruct.PosSize, (ushort)positiveSize);
        Ram.WriteWord(BaseAddress + LibraryStruct.Version, Version);
        Ram.WriteWord(BaseAddress + LibraryStruct.Revision, Revision);
        Ram.WriteLong(BaseAddress + LibraryStruct.IdString, idStringAddress);

        Initialize();
    }

    private void Call(string functionName, int offset, Action handler)
    {
        if (Context.Options.TraceLibraryCalls)
        {
            Context.Log(
                $"{Name}/{functionName}({offset}) D0={D0:X8} D1={D1:X8} D2={D2:X8} D3={D3:X8} A0={A0:X8} A1={A1:X8} from ${ReturnAddress:X8}");
        }

        if (handler == null)
        {
            throw new UnimplementedFunctionException(Name, functionName, offset);
        }

        handler();

        if (Context.Options.TraceLibraryCalls)
        {
            Context.Log($"{Name}/{functionName} = {D0:X8}");
        }
    }

    /// <summary>
    /// Return address of caller of library function on top of stack.
    /// </summary>
    private uint ReturnAddress
    {
        get
        {
            try
            {
                return Ram.ReadLong(Cpu.A[7]);
            }
            catch (BusErrorException)
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Allocate memory with zero terminated string.
    /// </summary>
    protected uint AllocateString(string value)
    {
        var bytes = Ram.GetBytes(value);
        var address = Context.Allocator.Allocate((uint)bytes.Length + 1);
        if (address == 0)
        {
            throw new VmException("Not enough memory to allocate string");
        }

        Ram.WriteBytes(address, bytes);
        return address;
    }

    /// <summary>
    /// Allocate memory with BCPL string.
    /// </summary>
    protected uint AllocateBString(string value, int size = 0)
    {
        var bytes = Ram.GetBytes(value);
        var address = Context.Allocator.Allocate((uint)Math.Max(Math.Min(bytes.Length, 255) + 1, size));
        if (address == 0)
        {
            throw new VmException("Not enough memory to allocate string");
        }

        Ram.WriteBString(address, value);
        return address;
    }

    // register accessors for function arguments and return values

    protected uint D0 { get => Cpu.D[0]; set => Cpu.D[0] = value; }
    protected uint D1 { get => Cpu.D[1]; set => Cpu.D[1] = value; }
    protected uint D2 => Cpu.D[2];
    protected uint D3 => Cpu.D[3];
    protected uint D4 => Cpu.D[4];
    protected uint D5 => Cpu.D[5];
    protected uint D6 => Cpu.D[6];
    protected uint D7 => Cpu.D[7];
    protected uint A0 { get => Cpu.A[0]; set => Cpu.A[0] = value; }
    protected uint A1 { get => Cpu.A[1]; set => Cpu.A[1] = value; }
    protected uint A2 => Cpu.A[2];
    protected uint A3 => Cpu.A[3];
    protected uint A5 => Cpu.A[5];

    /// <summary>
    /// Convert BCPL pointer to address.
    /// </summary>
    public static uint BAddr(uint bptr) => bptr << 2;

    /// <summary>
    /// Convert address to BCPL pointer, address must be long word aligned.
    /// </summary>
    public static uint MkBAddr(uint address) => address >> 2;
}
