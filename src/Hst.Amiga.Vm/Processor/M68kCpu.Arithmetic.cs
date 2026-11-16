namespace Hst.Amiga.Vm.Processor;

public sealed partial class M68kCpu
{
    /// <summary>
    /// Add source to destination setting flags X, N, Z, V and C.
    /// </summary>
    private uint Add(uint source, uint destination, int size, bool extend = false)
    {
        var mask = SizeMask(size);
        var msb = MsbMask(size);
        source &= mask;
        destination &= mask;
        var sum = (ulong)source + destination + (extend && x ? 1UL : 0UL);
        var result = (uint)sum & mask;
        c = x = sum > mask;
        v = ((source ^ result) & (destination ^ result) & msb) != 0;
        n = (result & msb) != 0;
        if (extend)
        {
            // zero flag is only cleared, if result is non-zero
            if (result != 0)
            {
                z = false;
            }
        }
        else
        {
            z = result == 0;
        }

        return result;
    }

    /// <summary>
    /// Subtract source from destination setting flags N, Z, V, C and X, unless compare.
    /// </summary>
    private uint Sub(uint source, uint destination, int size, bool extend = false, bool compare = false)
    {
        var mask = SizeMask(size);
        var msb = MsbMask(size);
        source &= mask;
        destination &= mask;
        var subtrahend = (ulong)source + (extend && x ? 1UL : 0UL);
        var result = (uint)(destination - subtrahend) & mask;
        c = subtrahend > destination;
        if (!compare)
        {
            x = c;
        }

        v = ((source ^ destination) & (result ^ destination) & msb) != 0;
        n = (result & msb) != 0;
        if (extend)
        {
            if (result != 0)
            {
                z = false;
            }
        }
        else
        {
            z = result == 0;
        }

        return result;
    }

    private void OpAddi()
    {
        var size = Size76;
        var source = size == 4 ? FetchLong() : FetchWord() & SizeMask(size);
        DecodeEa(EaMode, EaReg, size);
        WriteEaValue(size, Add(source, ReadEaValue(size), size));
    }

    private void OpSubi()
    {
        var size = Size76;
        var source = size == 4 ? FetchLong() : FetchWord() & SizeMask(size);
        DecodeEa(EaMode, EaReg, size);
        WriteEaValue(size, Sub(source, ReadEaValue(size), size));
    }

    private void OpCmpi()
    {
        var size = Size76;
        var source = size == 4 ? FetchLong() : FetchWord() & SizeMask(size);
        Sub(source, ReadEa(EaMode, EaReg, size), size, compare: true);
    }

    private uint QuickData
    {
        get
        {
            var data = (uint)Reg911;
            return data == 0 ? 8u : data;
        }
    }

    private void OpAddq()
    {
        var data = QuickData;
        if (EaMode == 1)
        {
            // address register, no flags affected and always long
            A[EaReg] += data;
            return;
        }

        var size = Size76;
        DecodeEa(EaMode, EaReg, size);
        WriteEaValue(size, Add(data, ReadEaValue(size), size));
    }

    private void OpSubq()
    {
        var data = QuickData;
        if (EaMode == 1)
        {
            A[EaReg] -= data;
            return;
        }

        var size = Size76;
        DecodeEa(EaMode, EaReg, size);
        WriteEaValue(size, Sub(data, ReadEaValue(size), size));
    }

    private void OpAddToRegister()
    {
        var size = Size76;
        var source = ReadEa(EaMode, EaReg, size);
        var reg = Reg911;
        var mask = SizeMask(size);
        D[reg] = (D[reg] & ~mask) | Add(source, D[reg], size);
    }

    private void OpAddToEa()
    {
        var size = Size76;
        DecodeEa(EaMode, EaReg, size);
        WriteEaValue(size, Add(D[Reg911], ReadEaValue(size), size));
    }

    private void OpSubToRegister()
    {
        var size = Size76;
        var source = ReadEa(EaMode, EaReg, size);
        var reg = Reg911;
        var mask = SizeMask(size);
        D[reg] = (D[reg] & ~mask) | Sub(source, D[reg], size);
    }

    private void OpSubToEa()
    {
        var size = Size76;
        DecodeEa(EaMode, EaReg, size);
        WriteEaValue(size, Sub(D[Reg911], ReadEaValue(size), size));
    }

    private int AddressSize => (opcode & 0x100) != 0 ? 4 : 2;

    private void OpAdda()
    {
        var size = AddressSize;
        var source = SignExtend(ReadEa(EaMode, EaReg, size), size);
        A[Reg911] += source;
    }

    private void OpSuba()
    {
        var size = AddressSize;
        var source = SignExtend(ReadEa(EaMode, EaReg, size), size);
        A[Reg911] -= source;
    }

    private void OpCmpa()
    {
        var size = AddressSize;
        var source = SignExtend(ReadEa(EaMode, EaReg, size), size);
        Sub(source, A[Reg911], 4, compare: true);
    }

    private void OpCmp()
    {
        var size = Size76;
        var source = ReadEa(EaMode, EaReg, size);
        Sub(source, D[Reg911], size, compare: true);
    }

    private void OpCmpm()
    {
        var size = Size76;
        var source = ReadEa(3, EaReg, size);
        var destination = ReadEa(3, Reg911, size);
        Sub(source, destination, size, compare: true);
    }

    private void OpAddx()
    {
        var size = Size76;
        var rx = Reg911;
        var ry = EaReg;
        if ((opcode & 8) == 0)
        {
            var mask = SizeMask(size);
            D[rx] = (D[rx] & ~mask) | Add(D[ry], D[rx], size, true);
            return;
        }

        var source = ReadEa(4, ry, size);
        DecodeEa(4, rx, size);
        WriteEaValue(size, Add(source, ReadEaValue(size), size, true));
    }

    private void OpSubx()
    {
        var size = Size76;
        var rx = Reg911;
        var ry = EaReg;
        if ((opcode & 8) == 0)
        {
            var mask = SizeMask(size);
            D[rx] = (D[rx] & ~mask) | Sub(D[ry], D[rx], size, true);
            return;
        }

        var source = ReadEa(4, ry, size);
        DecodeEa(4, rx, size);
        WriteEaValue(size, Sub(source, ReadEaValue(size), size, true));
    }

    private void OpNeg()
    {
        var size = Size76;
        DecodeEa(EaMode, EaReg, size);
        WriteEaValue(size, Sub(ReadEaValue(size), 0, size));
    }

    private void OpNegx()
    {
        var size = Size76;
        DecodeEa(EaMode, EaReg, size);
        WriteEaValue(size, Sub(ReadEaValue(size), 0, size, true));
    }

    private void OpTst()
    {
        var size = Size76;
        SetLogicFlags(ReadEa(EaMode, EaReg, size), size);
    }

    private void OpChk(int size)
    {
        var bound = (int)SignExtend(ReadEa(EaMode, EaReg, size), size);
        var value = (int)SignExtend(D[Reg911] & SizeMask(size), size);
        z = value == 0;
        v = false;
        c = false;
        if (value < 0)
        {
            n = true;
            RaiseException(ExceptionVectors.Chk, PC, format2: true);
        }
        else if (value > bound)
        {
            n = false;
            RaiseException(ExceptionVectors.Chk, PC, format2: true);
        }
    }

    private void OpMuluWord()
    {
        var reg = Reg911;
        var result = (D[reg] & 0xFFFF) * ReadEa(EaMode, EaReg, 2);
        D[reg] = result;
        SetLogicFlags(result, 4);
    }

    private void OpMulsWord()
    {
        var reg = Reg911;
        var result = (uint)((short)D[reg] * (short)ReadEa(EaMode, EaReg, 2));
        D[reg] = result;
        SetLogicFlags(result, 4);
    }

    private void ZeroDivide()
    {
        c = false;
        RaiseException(ExceptionVectors.ZeroDivide, PC, format2: true);
    }

    private void DivideOverflow()
    {
        n = true;
        v = true;
        c = false;
    }

    private void OpDivuWord()
    {
        var reg = Reg911;
        var divisor = ReadEa(EaMode, EaReg, 2);
        if (divisor == 0)
        {
            ZeroDivide();
            return;
        }

        var dividend = D[reg];
        var quotient = dividend / divisor;
        if (quotient > 0xFFFF)
        {
            DivideOverflow();
            return;
        }

        var remainder = dividend % divisor;
        D[reg] = (remainder << 16) | quotient;
        n = (quotient & 0x8000) != 0;
        z = quotient == 0;
        v = false;
        c = false;
    }

    private void OpDivsWord()
    {
        var reg = Reg911;
        var divisor = (int)(short)ReadEa(EaMode, EaReg, 2);
        if (divisor == 0)
        {
            ZeroDivide();
            return;
        }

        var dividend = (int)D[reg];
        if (dividend == int.MinValue && divisor == -1)
        {
            DivideOverflow();
            return;
        }

        var quotient = dividend / divisor;
        if (quotient < short.MinValue || quotient > short.MaxValue)
        {
            DivideOverflow();
            return;
        }

        var remainder = dividend % divisor;
        D[reg] = ((uint)(ushort)remainder << 16) | (ushort)quotient;
        n = quotient < 0;
        z = quotient == 0;
        v = false;
        c = false;
    }
}
