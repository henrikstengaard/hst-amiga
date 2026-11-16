namespace Hst.Amiga.Vm.Processor;

public sealed partial class M68kCpu
{
    private void OpBcc()
    {
        var condition = (opcode >> 8) & 15;
        var basePc = PC;
        var displacement = (uint)(sbyte)opcode;
        switch (opcode & 0xFF)
        {
            case 0:
                displacement = (uint)(short)FetchWord();
                break;
            case 0xFF when !is68000:
                displacement = FetchLong();
                break;
        }

        // bsr is encoded as condition false
        if (condition == 1)
        {
            Push32(PC);
            PC = basePc + displacement;
            return;
        }

        if (TestCondition(condition))
        {
            PC = basePc + displacement;
        }
    }

    private void OpDbcc()
    {
        var basePc = PC;
        var displacement = (uint)(short)FetchWord();
        if (TestCondition((opcode >> 8) & 15))
        {
            return;
        }

        var reg = EaReg;
        var counter = (ushort)(D[reg] - 1);
        D[reg] = (D[reg] & 0xFFFF0000) | counter;
        if (counter != 0xFFFF)
        {
            PC = basePc + displacement;
        }
    }

    private void OpScc()
    {
        WriteEa(EaMode, EaReg, 1, TestCondition((opcode >> 8) & 15) ? 0xFFu : 0u);
    }

    private void OpTrapcc()
    {
        switch (opcode & 7)
        {
            case 2:
                FetchWord();
                break;
            case 3:
                FetchLong();
                break;
        }

        if (TestCondition((opcode >> 8) & 15))
        {
            RaiseException(ExceptionVectors.TrapV, PC, format2: true);
        }
    }

    private void OpJmp()
    {
        PC = EaAddress(EaMode, EaReg);
    }

    private void OpJsr()
    {
        var address = EaAddress(EaMode, EaReg);
        Push32(PC);
        PC = address;
    }

    private void OpRts()
    {
        PC = Pop32();
    }

    private void OpRtr()
    {
        Ccr = (ushort)(Pop16() & 0x1F);
        PC = Pop32();
    }

    private void OpRtd()
    {
        var displacement = (uint)(short)FetchWord();
        PC = Pop32();
        A[7] += displacement;
    }

    private void OpLink()
    {
        var reg = EaReg;
        var displacement = (uint)(short)FetchWord();
        A[7] -= 4;
        Write32(A[7], A[reg]);
        A[reg] = A[7];
        A[7] += displacement;
    }

    private void OpLinkLong()
    {
        var reg = EaReg;
        var displacement = FetchLong();
        A[7] -= 4;
        Write32(A[7], A[reg]);
        A[reg] = A[7];
        A[7] += displacement;
    }

    private void OpUnlk()
    {
        var reg = EaReg;
        A[7] = A[reg];
        A[reg] = Pop32();
    }

    private void OpTrap()
    {
        RaiseException(ExceptionVectors.Trap0 + (opcode & 15), PC);
    }

    private void OpTrapv()
    {
        if (v)
        {
            RaiseException(ExceptionVectors.TrapV, PC, format2: true);
        }
    }

    private void OpTas()
    {
        DecodeEa(EaMode, EaReg, 1);
        var value = ReadEaValue(1);
        SetLogicFlags(value, 1);
        WriteEaValue(1, value | 0x80);
    }

    private void OpLineA()
    {
        if (LineAHandler != null && LineAHandler(this, opcode))
        {
            return;
        }

        RaiseException(ExceptionVectors.LineA, instructionPc, "unimplemented line-a opcode");
    }

    private void OpLineF()
    {
        RaiseException(ExceptionVectors.LineF, instructionPc, "unimplemented line-f opcode, fpu is not supported");
    }
}
