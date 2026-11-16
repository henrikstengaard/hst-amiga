namespace Hst.Amiga.Vm.Processor;

using System;
using System.Text;

/// <summary>
/// Interpreting Motorola 68000/68020 cpu emulating integer instruction set without fpu and mmu.
/// </summary>
public sealed partial class M68kCpu
{
    private readonly Ram ram;
    private readonly Action<M68kCpu>[] opcodeTable;
    private readonly bool is68000;

    /// <summary>
    /// Data registers D0-D7.
    /// </summary>
    public readonly uint[] D = new uint[8];

    /// <summary>
    /// Address registers A0-A7, A7 is active stack pointer.
    /// </summary>
    public readonly uint[] A = new uint[8];

    /// <summary>
    /// Program counter.
    /// </summary>
    public uint PC;

    // condition codes
    private bool x, n, z, v, c;

    // system byte of status register
    private bool s, m, t1, t0;
    private int interruptMask;

    // inactive stack pointers
    private uint usp, isp, msp;

    // current instruction
    private ushort opcode;
    private uint instructionPc;

    public M68kCpu(Ram ram, CpuType type)
    {
        this.ram = ram;
        Type = type;
        is68000 = type == CpuType.M68000;
        opcodeTable = OpcodeTableBuilder.GetTable(type);
        s = true;
        interruptMask = 7;
    }

    public CpuType Type { get; }
    public Ram Ram => ram;

    /// <summary>
    /// Vector base register (68020).
    /// </summary>
    public uint Vbr { get; set; }

    public uint Sfc { get; set; }
    public uint Dfc { get; set; }
    public uint Cacr { get; set; }
    public uint Caar { get; set; }

    /// <summary>
    /// Number of instructions executed.
    /// </summary>
    public long InstructionCount { get; private set; }

    /// <summary>
    /// Address of current instruction being executed.
    /// </summary>
    public uint InstructionPc => instructionPc;

    /// <summary>
    /// Opcode of current instruction being executed.
    /// </summary>
    public ushort Opcode => opcode;

    /// <summary>
    /// Handler for line-A opcodes used as traps to emulated functions. Returns true, if opcode was handled,
    /// otherwise line-A exception is raised.
    /// </summary>
    public Func<M68kCpu, ushort, bool> LineAHandler { get; set; }

    /// <summary>
    /// Address of default exception handler. Exceptions with vector pointing to default exception handler or
    /// zero are not processed by the cpu, but throws a <see cref="CpuException"/>.
    /// </summary>
    public uint DefaultExceptionHandler { get; set; }

    /// <summary>
    /// Trace action called before each instruction is executed.
    /// </summary>
    public Action<M68kCpu> Trace { get; set; }

    public bool X { get => x; set => x = value; }
    public bool N { get => n; set => n = value; }
    public bool Z { get => z; set => z = value; }
    public bool V { get => v; set => v = value; }
    public bool C { get => c; set => c = value; }

    /// <summary>
    /// Supervisor mode.
    /// </summary>
    public bool Supervisor => s;

    public ushort Ccr
    {
        get => (ushort)((x ? 0x10 : 0) | (n ? 8 : 0) | (z ? 4 : 0) | (v ? 2 : 0) | (c ? 1 : 0));
        set
        {
            x = (value & 0x10) != 0;
            n = (value & 8) != 0;
            z = (value & 4) != 0;
            v = (value & 2) != 0;
            c = (value & 1) != 0;
        }
    }

    /// <summary>
    /// Status register. Changing supervisor and master bits switches active stack pointer.
    /// </summary>
    public ushort Sr
    {
        get => (ushort)((t1 ? 0x8000 : 0) | (t0 ? 0x4000 : 0) | (s ? 0x2000 : 0) | (m ? 0x1000 : 0) |
                        (interruptMask << 8) | Ccr);
        set
        {
            SaveStackPointer();
            t1 = (value & 0x8000) != 0;
            t0 = !is68000 && (value & 0x4000) != 0;
            s = (value & 0x2000) != 0;
            m = !is68000 && (value & 0x1000) != 0;
            interruptMask = (value >> 8) & 7;
            Ccr = value;
            LoadStackPointer();
        }
    }

    /// <summary>
    /// User stack pointer.
    /// </summary>
    public uint Usp
    {
        get => s ? usp : A[7];
        set
        {
            if (s)
            {
                usp = value;
            }
            else
            {
                A[7] = value;
            }
        }
    }

    /// <summary>
    /// Supervisor stack pointer, interrupt stack pointer for 68020.
    /// </summary>
    public uint Ssp
    {
        get => s && !m ? A[7] : isp;
        set
        {
            if (s && !m)
            {
                A[7] = value;
            }
            else
            {
                isp = value;
            }
        }
    }

    /// <summary>
    /// Master stack pointer (68020).
    /// </summary>
    public uint Msp
    {
        get => s && m ? A[7] : msp;
        set
        {
            if (s && m)
            {
                A[7] = value;
            }
            else
            {
                msp = value;
            }
        }
    }

    private void SaveStackPointer()
    {
        if (!s)
        {
            usp = A[7];
        }
        else if (m)
        {
            msp = A[7];
        }
        else
        {
            isp = A[7];
        }
    }

    private void LoadStackPointer()
    {
        A[7] = !s ? usp : m ? msp : isp;
    }

    /// <summary>
    /// Execute one instruction.
    /// </summary>
    public void Step()
    {
        Trace?.Invoke(this);

        instructionPc = PC;
        try
        {
            opcode = FetchWord();
            opcodeTable[opcode](this);
        }
        catch (AddressErrorException e)
        {
            RaiseException(ExceptionVectors.AddressError, instructionPc, $"address ${e.Address:X8}");
        }
        catch (BusErrorException e)
        {
            RaiseException(ExceptionVectors.BusError, instructionPc, e.Message);
        }

        InstructionCount++;
    }

    /// <summary>
    /// Raise exception with vector, building an exception stack frame and continuing at exception handler.
    /// </summary>
    /// <param name="vector">Exception vector number.</param>
    /// <param name="pc">Program counter pushed to exception stack frame.</param>
    /// <param name="detail">Detail used in exception message, if no exception handler is installed.</param>
    /// <param name="format2">Use 68020 format $2 stack frame with address of instruction causing exception.</param>
    public void RaiseException(int vector, uint pc, string detail = null, bool format2 = false)
    {
        var handler = ram.ReadLong(Vbr + (uint)vector * 4);
        if (handler == 0 || handler == DefaultExceptionHandler)
        {
            throw new CpuException(vector, instructionPc, opcode, DumpRegisters(), detail);
        }

        var oldSr = Sr;

        // enter supervisor mode with trace disabled
        Sr = (ushort)((oldSr | 0x2000) & 0x3FFF);

        if (!is68000)
        {
            if (format2)
            {
                Push32(instructionPc);
                Push16((ushort)(0x2000 | (vector * 4)));
            }
            else
            {
                Push16((ushort)(vector * 4));
            }
        }

        Push32(pc);
        Push16(oldSr);
        PC = handler;
    }

    /// <summary>
    /// Raise privilege violation exception, if not in supervisor mode.
    /// </summary>
    /// <returns>True, if in supervisor mode.</returns>
    private bool CheckSupervisor()
    {
        if (s)
        {
            return true;
        }

        RaiseException(ExceptionVectors.PrivilegeViolation, instructionPc);
        return false;
    }

    private void IllegalInstruction()
    {
        RaiseException(ExceptionVectors.IllegalInstruction, instructionPc, "illegal instruction");
    }

    public string DumpRegisters()
    {
        var builder = new StringBuilder();
        for (var i = 0; i < 8; i++)
        {
            builder.Append($"D{i}={D[i]:X8} ");
        }

        builder.AppendLine();
        for (var i = 0; i < 8; i++)
        {
            builder.Append($"A{i}={A[i]:X8} ");
        }

        builder.AppendLine();
        builder.Append(
            $"PC={PC:X8} SR={Sr:X4} USP={Usp:X8} SSP={Ssp:X8} {(x ? 'X' : '-')}{(n ? 'N' : '-')}{(z ? 'Z' : '-')}{(v ? 'V' : '-')}{(c ? 'C' : '-')}");
        return builder.ToString();
    }

    // memory access

    internal byte Read8(uint address) => ram.ReadByte(address);

    internal ushort Read16(uint address)
    {
        if (is68000 && (address & 1) != 0)
        {
            throw new AddressErrorException(address);
        }

        return ram.ReadWord(address);
    }

    internal uint Read32(uint address)
    {
        if (is68000 && (address & 1) != 0)
        {
            throw new AddressErrorException(address);
        }

        return ram.ReadLong(address);
    }

    internal void Write8(uint address, uint value) => ram.WriteByte(address, (byte)value);

    internal void Write16(uint address, uint value)
    {
        if (is68000 && (address & 1) != 0)
        {
            throw new AddressErrorException(address);
        }

        ram.WriteWord(address, (ushort)value);
    }

    internal void Write32(uint address, uint value)
    {
        if (is68000 && (address & 1) != 0)
        {
            throw new AddressErrorException(address);
        }

        ram.WriteLong(address, value);
    }

    internal uint Read(uint address, int size) => size switch
    {
        1 => Read8(address),
        2 => Read16(address),
        _ => Read32(address)
    };

    internal void Write(uint address, int size, uint value)
    {
        switch (size)
        {
            case 1:
                Write8(address, value);
                break;
            case 2:
                Write16(address, value);
                break;
            default:
                Write32(address, value);
                break;
        }
    }

    internal ushort FetchWord()
    {
        if ((PC & 1) != 0)
        {
            throw new AddressErrorException(PC);
        }

        var value = ram.ReadWord(PC);
        PC += 2;
        return value;
    }

    internal uint FetchLong()
    {
        var high = (uint)FetchWord();
        return (high << 16) | FetchWord();
    }

    public void Push16(uint value)
    {
        A[7] -= 2;
        Write16(A[7], value);
    }

    public void Push32(uint value)
    {
        A[7] -= 4;
        Write32(A[7], value);
    }

    public ushort Pop16()
    {
        var value = Read16(A[7]);
        A[7] += 2;
        return value;
    }

    public uint Pop32()
    {
        var value = Read32(A[7]);
        A[7] += 4;
        return value;
    }

    // size helpers

    internal static uint SizeMask(int size) => size switch
    {
        1 => 0xFFu,
        2 => 0xFFFFu,
        _ => 0xFFFFFFFFu
    };

    internal static uint MsbMask(int size) => size switch
    {
        1 => 0x80u,
        2 => 0x8000u,
        _ => 0x80000000u
    };

    internal static uint SignExtend(uint value, int size) => size switch
    {
        1 => (uint)(sbyte)value,
        2 => (uint)(short)value,
        _ => value
    };

    /// <summary>
    /// Size encoded in bits 7-6 as 00 byte, 01 word, 10 long.
    /// </summary>
    private int Size76 => ((opcode >> 6) & 3) switch
    {
        0 => 1,
        1 => 2,
        _ => 4
    };

    private int EaMode => (opcode >> 3) & 7;
    private int EaReg => opcode & 7;
    private int Reg911 => (opcode >> 9) & 7;

    // flags

    private void SetNz(uint result, int size)
    {
        result &= SizeMask(size);
        n = (result & MsbMask(size)) != 0;
        z = result == 0;
    }

    private void SetLogicFlags(uint result, int size)
    {
        SetNz(result, size);
        v = false;
        c = false;
    }

    private bool TestCondition(int condition) => condition switch
    {
        0 => true,
        1 => false,
        2 => !c && !z,
        3 => c || z,
        4 => !c,
        5 => c,
        6 => !z,
        7 => z,
        8 => !v,
        9 => v,
        10 => !n,
        11 => n,
        12 => n == v,
        13 => n != v,
        14 => !z && n == v,
        _ => z || n != v
    };
}
