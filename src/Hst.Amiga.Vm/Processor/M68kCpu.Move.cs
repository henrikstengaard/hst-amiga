namespace Hst.Amiga.Vm.Processor;

public sealed partial class M68kCpu
{
    private void OpMove()
    {
        var size = ((opcode >> 12) & 3) switch
        {
            1 => 1,
            3 => 2,
            _ => 4
        };

        var value = ReadEa(EaMode, EaReg, size);
        var destinationMode = (opcode >> 6) & 7;
        var destinationReg = Reg911;

        if (destinationMode == 1)
        {
            // movea, no flags affected
            A[destinationReg] = SignExtend(value, size);
            return;
        }

        WriteEa(destinationMode, destinationReg, size, value);
        SetLogicFlags(value, size);
    }

    private void OpMoveq()
    {
        var value = (uint)(sbyte)opcode;
        D[Reg911] = value;
        SetLogicFlags(value, 4);
    }

    private void OpLea()
    {
        A[Reg911] = EaAddress(EaMode, EaReg);
    }

    private void OpPea()
    {
        var address = EaAddress(EaMode, EaReg);
        Push32(address);
    }

    private void OpClr()
    {
        var size = Size76;
        WriteEa(EaMode, EaReg, size, 0);
        n = false;
        z = true;
        v = false;
        c = false;
    }

    private void OpExg()
    {
        var rx = Reg911;
        var ry = EaReg;
        uint temp;
        switch ((opcode >> 3) & 0x1F)
        {
            case 0x08:
                temp = D[rx];
                D[rx] = D[ry];
                D[ry] = temp;
                break;
            case 0x09:
                temp = A[rx];
                A[rx] = A[ry];
                A[ry] = temp;
                break;
            default:
                temp = D[rx];
                D[rx] = A[ry];
                A[ry] = temp;
                break;
        }
    }

    private void OpSwap()
    {
        var reg = EaReg;
        var value = (D[reg] >> 16) | (D[reg] << 16);
        D[reg] = value;
        SetLogicFlags(value, 4);
    }

    private void OpExtWord()
    {
        var reg = EaReg;
        var value = (uint)(ushort)(sbyte)D[reg];
        D[reg] = (D[reg] & 0xFFFF0000) | value;
        SetLogicFlags(value, 2);
    }

    private void OpExtLong()
    {
        var reg = EaReg;
        var value = (uint)(short)D[reg];
        D[reg] = value;
        SetLogicFlags(value, 4);
    }

    private void OpExtbLong()
    {
        var reg = EaReg;
        var value = (uint)(sbyte)D[reg];
        D[reg] = value;
        SetLogicFlags(value, 4);
    }

    private void OpMovemToMemory()
    {
        var size = (opcode & 0x40) != 0 ? 4 : 2;
        var mask = FetchWord();
        var mode = EaMode;
        var reg = EaReg;

        if (mode == 4)
        {
            // predecrement mode with register mask reversed, bit 0 is a7 and bit 15 is d0
            var address = A[reg];
            for (var i = 0; i < 16; i++)
            {
                if ((mask & (1 << i)) == 0)
                {
                    continue;
                }

                address -= (uint)size;
                var value = i < 8 ? A[7 - i] : D[15 - i];
                Write(address, size, value);
            }

            A[reg] = address;
            return;
        }

        var target = EaAddress(mode, reg);
        for (var i = 0; i < 16; i++)
        {
            if ((mask & (1 << i)) == 0)
            {
                continue;
            }

            var value = i < 8 ? D[i] : A[i - 8];
            Write(target, size, value);
            target += (uint)size;
        }
    }

    private void OpMovemToRegisters()
    {
        var size = (opcode & 0x40) != 0 ? 4 : 2;
        var mask = FetchWord();
        var mode = EaMode;
        var reg = EaReg;

        var address = mode == 3 ? A[reg] : EaAddress(mode, reg);
        for (var i = 0; i < 16; i++)
        {
            if ((mask & (1 << i)) == 0)
            {
                continue;
            }

            // words are sign extended to long for both data and address registers
            var value = SignExtend(Read(address, size), size);
            if (i < 8)
            {
                D[i] = value;
            }
            else
            {
                A[i - 8] = value;
            }

            address += (uint)size;
        }

        if (mode == 3)
        {
            A[reg] = address;
        }
    }

    private void OpMovep()
    {
        var dataReg = Reg911;
        var address = A[EaReg] + (uint)(short)FetchWord();
        switch ((opcode >> 6) & 3)
        {
            case 0:
                D[dataReg] = (D[dataReg] & 0xFFFF0000) | ((uint)Read8(address) << 8) | Read8(address + 2);
                break;
            case 1:
                D[dataReg] = ((uint)Read8(address) << 24) | ((uint)Read8(address + 2) << 16) |
                             ((uint)Read8(address + 4) << 8) | Read8(address + 6);
                break;
            case 2:
                Write8(address, D[dataReg] >> 8);
                Write8(address + 2, D[dataReg]);
                break;
            default:
                Write8(address, D[dataReg] >> 24);
                Write8(address + 2, D[dataReg] >> 16);
                Write8(address + 4, D[dataReg] >> 8);
                Write8(address + 6, D[dataReg]);
                break;
        }
    }
}
