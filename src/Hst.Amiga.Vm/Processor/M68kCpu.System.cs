namespace Hst.Amiga.Vm.Processor;

public sealed partial class M68kCpu
{
    private void OpOriCcr()
    {
        Ccr = (ushort)((Ccr | FetchWord()) & 0x1F);
    }

    private void OpAndiCcr()
    {
        Ccr = (ushort)(Ccr & FetchWord() & 0x1F);
    }

    private void OpEoriCcr()
    {
        Ccr = (ushort)((Ccr ^ FetchWord()) & 0x1F);
    }

    private void OpOriSr()
    {
        if (!CheckSupervisor())
        {
            return;
        }

        Sr = (ushort)(Sr | FetchWord());
    }

    private void OpAndiSr()
    {
        if (!CheckSupervisor())
        {
            return;
        }

        Sr = (ushort)(Sr & FetchWord());
    }

    private void OpEoriSr()
    {
        if (!CheckSupervisor())
        {
            return;
        }

        Sr = (ushort)(Sr ^ FetchWord());
    }

    private void OpMoveFromSr()
    {
        // privileged on 68010 and later
        if (!is68000 && !CheckSupervisor())
        {
            return;
        }

        WriteEa(EaMode, EaReg, 2, Sr);
    }

    private void OpMoveFromCcr()
    {
        WriteEa(EaMode, EaReg, 2, Ccr);
    }

    private void OpMoveToCcr()
    {
        Ccr = (ushort)(ReadEa(EaMode, EaReg, 2) & 0x1F);
    }

    private void OpMoveToSr()
    {
        if (!CheckSupervisor())
        {
            return;
        }

        Sr = (ushort)ReadEa(EaMode, EaReg, 2);
    }

    private void OpMoveUsp()
    {
        if (!CheckSupervisor())
        {
            return;
        }

        if ((opcode & 8) == 0)
        {
            usp = A[EaReg];
        }
        else
        {
            A[EaReg] = usp;
        }
    }

    private void OpReset()
    {
        CheckSupervisor();
    }

    private void OpStop()
    {
        if (!CheckSupervisor())
        {
            return;
        }

        Sr = FetchWord();
        throw new CpuException(0, instructionPc, opcode, DumpRegisters(),
            "STOP instruction halts cpu without interrupts");
    }

    private void OpRte()
    {
        if (!CheckSupervisor())
        {
            return;
        }

        var sr = Pop16();
        var pc = Pop32();
        if (!is68000)
        {
            var format = Pop16() >> 12;
            switch (format)
            {
                case 0:
                    break;
                case 2:
                    A[7] += 4;
                    break;
                default:
                    RaiseException(ExceptionVectors.FormatError, instructionPc, $"unsupported stack frame format {format}");
                    return;
            }
        }

        Sr = sr;
        PC = pc;
    }

    private void OpMovec()
    {
        if (!CheckSupervisor())
        {
            return;
        }

        var ext = FetchWord();
        var reg = (ext >> 12) & 7;
        var isAddress = (ext & 0x8000) != 0;
        var controlRegister = ext & 0xFFF;

        if ((opcode & 1) == 0)
        {
            // control register to general register
            uint value;
            switch (controlRegister)
            {
                case 0x000:
                    value = Sfc;
                    break;
                case 0x001:
                    value = Dfc;
                    break;
                case 0x002:
                    value = Cacr;
                    break;
                case 0x800:
                    value = Usp;
                    break;
                case 0x801:
                    value = Vbr;
                    break;
                case 0x802:
                    value = Caar;
                    break;
                case 0x803:
                    value = Msp;
                    break;
                case 0x804:
                    value = Ssp;
                    break;
                default:
                    IllegalInstruction();
                    return;
            }

            if (isAddress)
            {
                A[reg] = value;
            }
            else
            {
                D[reg] = value;
            }

            return;
        }

        var source = isAddress ? A[reg] : D[reg];
        switch (controlRegister)
        {
            case 0x000:
                Sfc = source & 7;
                break;
            case 0x001:
                Dfc = source & 7;
                break;
            case 0x002:
                // 68020 cache control register has enable, freeze, clear entry and clear bits
                Cacr = source & 0x3;
                break;
            case 0x800:
                Usp = source;
                break;
            case 0x801:
                Vbr = source;
                break;
            case 0x802:
                Caar = source;
                break;
            case 0x803:
                Msp = source;
                break;
            case 0x804:
                Ssp = source;
                break;
            default:
                IllegalInstruction();
                break;
        }
    }

    private void OpMoves()
    {
        if (!CheckSupervisor())
        {
            return;
        }

        var size = Size76;
        var ext = FetchWord();
        var reg = (ext >> 12) & 7;
        var isAddress = (ext & 0x8000) != 0;

        // function codes are ignored as there is no mmu
        if ((ext & 0x800) != 0)
        {
            WriteEa(EaMode, EaReg, size, isAddress ? A[reg] : D[reg]);
            return;
        }

        var value = ReadEa(EaMode, EaReg, size);
        if (isAddress)
        {
            A[reg] = SignExtend(value, size);
        }
        else
        {
            var mask = SizeMask(size);
            D[reg] = (D[reg] & ~mask) | value;
        }
    }
}
