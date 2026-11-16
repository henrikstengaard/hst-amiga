namespace Hst.Amiga.Vm.Processor;

using System;

public sealed partial class M68kCpu
{
    private const int EaKindDataRegister = 0;
    private const int EaKindAddressRegister = 1;
    private const int EaKindMemory = 2;
    private const int EaKindImmediate = 3;

    // decoded effective address
    private int eaKind;
    private int eaRegister;
    private uint eaAddress;
    private uint eaImmediate;

    /// <summary>
    /// Decode effective address, fetching extension words and applying predecrement and postincrement.
    /// </summary>
    private void DecodeEa(int mode, int reg, int size)
    {
        switch (mode)
        {
            case 0:
                eaKind = EaKindDataRegister;
                eaRegister = reg;
                return;
            case 1:
                eaKind = EaKindAddressRegister;
                eaRegister = reg;
                return;
            case 2:
                eaKind = EaKindMemory;
                eaAddress = A[reg];
                return;
            case 3:
                eaKind = EaKindMemory;
                eaAddress = A[reg];
                A[reg] += reg == 7 && size == 1 ? 2u : (uint)size;
                return;
            case 4:
                eaKind = EaKindMemory;
                A[reg] -= reg == 7 && size == 1 ? 2u : (uint)size;
                eaAddress = A[reg];
                return;
            case 5:
                eaKind = EaKindMemory;
                eaAddress = A[reg] + (uint)(short)FetchWord();
                return;
            case 6:
                eaKind = EaKindMemory;
                eaAddress = IndexedAddress(A[reg]);
                return;
        }

        switch (reg)
        {
            case 0:
                eaKind = EaKindMemory;
                eaAddress = (uint)(short)FetchWord();
                return;
            case 1:
                eaKind = EaKindMemory;
                eaAddress = FetchLong();
                return;
            case 2:
            {
                eaKind = EaKindMemory;
                var basePc = PC;
                eaAddress = basePc + (uint)(short)FetchWord();
                return;
            }
            case 3:
            {
                eaKind = EaKindMemory;
                var basePc = PC;
                eaAddress = IndexedAddress(basePc);
                return;
            }
            case 4:
                eaKind = EaKindImmediate;
                eaImmediate = size switch
                {
                    1 => FetchWord() & 0xFFu,
                    2 => FetchWord(),
                    _ => FetchLong()
                };
                return;
            default:
                throw new InvalidOperationException($"Invalid effective address mode 7 register {reg}");
        }
    }

    /// <summary>
    /// Calculate address for indexed addressing modes with brief extension word and 68020 full extension word.
    /// </summary>
    private uint IndexedAddress(uint baseAddress)
    {
        var ext = FetchWord();
        var indexRegister = (ext >> 12) & 7;
        var index = (ext & 0x8000) != 0 ? A[indexRegister] : D[indexRegister];
        if ((ext & 0x800) == 0)
        {
            index = (uint)(short)index;
        }

        if (is68000)
        {
            return baseAddress + index + (uint)(sbyte)ext;
        }

        index <<= (ext >> 9) & 3;

        // brief extension word
        if ((ext & 0x100) == 0)
        {
            return baseAddress + index + (uint)(sbyte)ext;
        }

        // full extension word
        if ((ext & 0x80) != 0)
        {
            baseAddress = 0;
        }

        var indexSuppress = (ext & 0x40) != 0;
        if (indexSuppress)
        {
            index = 0;
        }

        var baseDisplacement = ((ext >> 4) & 3) switch
        {
            2 => (uint)(short)FetchWord(),
            3 => FetchLong(),
            _ => 0u
        };

        var indirect = ext & 7;
        if (indirect == 0)
        {
            return baseAddress + baseDisplacement + index;
        }

        var outerDisplacement = (indirect & 3) switch
        {
            2 => (uint)(short)FetchWord(),
            3 => FetchLong(),
            _ => 0u
        };

        // memory indirect post-indexed
        if (!indexSuppress && (indirect & 4) != 0)
        {
            return Read32(baseAddress + baseDisplacement) + index + outerDisplacement;
        }

        // memory indirect pre-indexed
        return Read32(baseAddress + baseDisplacement + index) + outerDisplacement;
    }

    private uint ReadEaValue(int size)
    {
        switch (eaKind)
        {
            case EaKindDataRegister:
                return D[eaRegister] & SizeMask(size);
            case EaKindAddressRegister:
                return A[eaRegister] & SizeMask(size);
            case EaKindMemory:
                return Read(eaAddress, size);
            default:
                return eaImmediate;
        }
    }

    private void WriteEaValue(int size, uint value)
    {
        switch (eaKind)
        {
            case EaKindDataRegister:
                var mask = SizeMask(size);
                D[eaRegister] = (D[eaRegister] & ~mask) | (value & mask);
                break;
            case EaKindAddressRegister:
                A[eaRegister] = value;
                break;
            case EaKindMemory:
                Write(eaAddress, size, value);
                break;
            default:
                throw new InvalidOperationException("Write to immediate effective address");
        }
    }

    private uint ReadEa(int mode, int reg, int size)
    {
        DecodeEa(mode, reg, size);
        return ReadEaValue(size);
    }

    private void WriteEa(int mode, int reg, int size, uint value)
    {
        DecodeEa(mode, reg, size);
        WriteEaValue(size, value);
    }

    /// <summary>
    /// Calculate address of control addressing mode.
    /// </summary>
    private uint EaAddress(int mode, int reg)
    {
        DecodeEa(mode, reg, 4);
        return eaAddress;
    }
}
