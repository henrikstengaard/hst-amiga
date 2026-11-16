namespace Hst.Amiga.Vm.Processor;

public sealed partial class M68kCpu
{
    private uint Abcd(uint source, uint destination)
    {
        var result = (source & 0x0F) + (destination & 0x0F) + (x ? 1u : 0u);
        var undefinedV = ~result;
        if (result > 9)
        {
            result += 6;
        }

        result += (source & 0xF0) + (destination & 0xF0);
        c = x = result > 0x99;
        if (c)
        {
            result -= 0xA0;
        }

        v = (undefinedV & result & 0x80) != 0;
        n = (result & 0x80) != 0;
        result &= 0xFF;
        if (result != 0)
        {
            z = false;
        }

        return result;
    }

    private uint Sbcd(uint source, uint destination)
    {
        var result = (destination & 0x0F) - (source & 0x0F) - (x ? 1u : 0u);
        var undefinedV = ~result;
        if (result > 9)
        {
            result -= 6;
        }

        result += (destination & 0xF0) - (source & 0xF0);
        c = x = result > 0x99;
        if (c)
        {
            result += 0xA0;
        }

        v = (undefinedV & result & 0x80) != 0;
        n = (result & 0x80) != 0;
        result &= 0xFF;
        if (result != 0)
        {
            z = false;
        }

        return result;
    }

    private void OpAbcd()
    {
        var rx = Reg911;
        var ry = EaReg;
        if ((opcode & 8) == 0)
        {
            D[rx] = (D[rx] & 0xFFFFFF00) | Abcd(D[ry] & 0xFF, D[rx] & 0xFF);
            return;
        }

        var source = ReadEa(4, ry, 1);
        DecodeEa(4, rx, 1);
        WriteEaValue(1, Abcd(source, ReadEaValue(1)));
    }

    private void OpSbcd()
    {
        var rx = Reg911;
        var ry = EaReg;
        if ((opcode & 8) == 0)
        {
            D[rx] = (D[rx] & 0xFFFFFF00) | Sbcd(D[ry] & 0xFF, D[rx] & 0xFF);
            return;
        }

        var source = ReadEa(4, ry, 1);
        DecodeEa(4, rx, 1);
        WriteEaValue(1, Sbcd(source, ReadEaValue(1)));
    }

    private void OpNbcd()
    {
        DecodeEa(EaMode, EaReg, 1);
        var destination = ReadEaValue(1);
        var result = (0x9Au - destination - (x ? 1u : 0u)) & 0xFF;
        if (result != 0x9A)
        {
            var undefinedV = ~result;
            if ((result & 0x0F) == 0x0A)
            {
                result = (result & 0xF0) + 0x10;
            }

            result &= 0xFF;
            v = (undefinedV & result & 0x80) != 0;
            WriteEaValue(1, result);
            if (result != 0)
            {
                z = false;
            }

            c = true;
            x = true;
        }
        else
        {
            v = false;
            c = false;
            x = false;
        }

        n = (result & 0x80) != 0;
    }

    private void OpPack()
    {
        var adjustment = FetchWord();
        var rx = EaReg;
        var ry = Reg911;
        if ((opcode & 8) == 0)
        {
            var value = (D[rx] + adjustment) & 0xFFFF;
            D[ry] = (D[ry] & 0xFFFFFF00) | ((value >> 4) & 0xF0) | (value & 0x0F);
            return;
        }

        // first byte read from predecremented address is low order byte
        var low = ReadEa(4, rx, 1);
        var high = ReadEa(4, rx, 1);
        var source = ((high << 8) | low) + adjustment;
        WriteEa(4, ry, 1, ((source >> 4) & 0xF0) | (source & 0x0F));
    }

    private void OpUnpk()
    {
        var adjustment = FetchWord();
        var rx = EaReg;
        var ry = Reg911;
        if ((opcode & 8) == 0)
        {
            var source = D[rx];
            var value = ((((source << 4) & 0x0F00) | (source & 0x000F)) + adjustment) & 0xFFFF;
            D[ry] = (D[ry] & 0xFFFF0000) | value;
            return;
        }

        var byteValue = ReadEa(4, rx, 1);
        var result = (((byteValue << 4) & 0x0F00) | (byteValue & 0x000F)) + adjustment;
        WriteEa(4, ry, 1, result);
        WriteEa(4, ry, 1, result >> 8);
    }
}
