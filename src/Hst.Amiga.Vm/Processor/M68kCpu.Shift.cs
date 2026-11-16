namespace Hst.Amiga.Vm.Processor;

public sealed partial class M68kCpu
{
    private void OpShiftRegister()
    {
        var size = Size76;
        var countOrReg = Reg911;
        var count = (opcode & 0x20) != 0 ? (int)(D[countOrReg] & 63) : countOrReg == 0 ? 8 : countOrReg;
        var left = (opcode & 0x100) != 0;
        var type = (opcode >> 3) & 3;
        var reg = EaReg;
        var mask = SizeMask(size);
        var result = Shift(type, left, D[reg] & mask, size, count);
        D[reg] = (D[reg] & ~mask) | result;
    }

    private void OpShiftMemory()
    {
        var type = (opcode >> 9) & 3;
        var left = (opcode & 0x100) != 0;
        DecodeEa(EaMode, EaReg, 2);
        WriteEaValue(2, Shift(type, left, ReadEaValue(2), 2, 1));
    }

    /// <summary>
    /// Shift or rotate value setting flags.
    /// </summary>
    /// <param name="type">0 arithmetic shift, 1 logical shift, 2 rotate with extend, 3 rotate.</param>
    private uint Shift(int type, bool left, uint value, int size, int count)
    {
        var mask = SizeMask(size);
        var msb = MsbMask(size);
        value &= mask;
        v = false;

        if (count == 0)
        {
            // carry is cleared for zero count, except for rotate with extend where carry is set to extend
            c = type == 2 && x;
            SetNz(value, size);
            return value;
        }

        switch (type)
        {
            case 0 when left:
                for (var i = 0; i < count; i++)
                {
                    var bit = (value & msb) != 0;
                    value = (value << 1) & mask;
                    c = bit;
                    if (((value & msb) != 0) != bit)
                    {
                        // overflow, if msb changes at any time during shift
                        v = true;
                    }
                }

                x = c;
                break;
            case 0:
                var sign = value & msb;
                for (var i = 0; i < count; i++)
                {
                    c = (value & 1) != 0;
                    value = (value >> 1) | sign;
                }

                x = c;
                break;
            case 1 when left:
                for (var i = 0; i < count; i++)
                {
                    c = (value & msb) != 0;
                    value = (value << 1) & mask;
                }

                x = c;
                break;
            case 1:
                for (var i = 0; i < count; i++)
                {
                    c = (value & 1) != 0;
                    value >>= 1;
                }

                x = c;
                break;
            case 2 when left:
                for (var i = 0; i < count; i++)
                {
                    var bit = (value & msb) != 0;
                    value = ((value << 1) & mask) | (x ? 1u : 0u);
                    x = bit;
                }

                c = x;
                break;
            case 2:
                for (var i = 0; i < count; i++)
                {
                    var bit = (value & 1) != 0;
                    value = (value >> 1) | (x ? msb : 0u);
                    x = bit;
                }

                c = x;
                break;
            case 3 when left:
                for (var i = 0; i < count; i++)
                {
                    var bit = (value & msb) != 0;
                    value = ((value << 1) & mask) | (bit ? 1u : 0u);
                    c = bit;
                }

                break;
            default:
                for (var i = 0; i < count; i++)
                {
                    var bit = (value & 1) != 0;
                    value = (value >> 1) | (bit ? msb : 0u);
                    c = bit;
                }

                break;
        }

        SetNz(value, size);
        return value;
    }
}
