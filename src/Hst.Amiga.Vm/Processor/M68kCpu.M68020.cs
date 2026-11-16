namespace Hst.Amiga.Vm.Processor;

public sealed partial class M68kCpu
{
    private void OpMulLong()
    {
        var ext = FetchWord();
        var source = ReadEa(EaMode, EaReg, 4);
        var dl = (ext >> 12) & 7;
        var dh = ext & 7;
        var signed = (ext & 0x800) != 0;
        var quad = (ext & 0x400) != 0;
        c = false;

        if (signed)
        {
            var product = (long)(int)source * (int)D[dl];
            if (quad)
            {
                D[dh] = (uint)(product >> 32);
                D[dl] = (uint)product;
                n = product < 0;
                z = product == 0;
                v = false;
                return;
            }

            D[dl] = (uint)product;
            n = (int)D[dl] < 0;
            z = D[dl] == 0;
            v = product != (int)product;
            return;
        }

        var unsignedProduct = (ulong)source * D[dl];
        if (quad)
        {
            D[dh] = (uint)(unsignedProduct >> 32);
            D[dl] = (uint)unsignedProduct;
            n = (unsignedProduct & 0x8000000000000000UL) != 0;
            z = unsignedProduct == 0;
            v = false;
            return;
        }

        D[dl] = (uint)unsignedProduct;
        n = (D[dl] & 0x80000000) != 0;
        z = D[dl] == 0;
        v = unsignedProduct > 0xFFFFFFFFUL;
    }

    private void OpDivLong()
    {
        var ext = FetchWord();
        var divisor = ReadEa(EaMode, EaReg, 4);
        var dq = (ext >> 12) & 7;
        var dr = ext & 7;
        var signed = (ext & 0x800) != 0;
        var quad = (ext & 0x400) != 0;

        if (divisor == 0)
        {
            ZeroDivide();
            return;
        }

        uint quotient;
        uint remainder;
        if (signed)
        {
            var dividend = quad ? (long)(((ulong)D[dr] << 32) | D[dq]) : (int)D[dq];
            var signedDivisor = (long)(int)divisor;
            if (dividend == long.MinValue && signedDivisor == -1)
            {
                v = true;
                c = false;
                return;
            }

            var signedQuotient = dividend / signedDivisor;
            if (signedQuotient < int.MinValue || signedQuotient > int.MaxValue)
            {
                v = true;
                c = false;
                return;
            }

            quotient = (uint)signedQuotient;
            remainder = (uint)(dividend % signedDivisor);
        }
        else
        {
            var dividend = quad ? ((ulong)D[dr] << 32) | D[dq] : D[dq];
            var unsignedQuotient = dividend / divisor;
            if (unsignedQuotient > 0xFFFFFFFFUL)
            {
                v = true;
                c = false;
                return;
            }

            quotient = (uint)unsignedQuotient;
            remainder = (uint)(dividend % divisor);
        }

        // remainder is discarded, if quotient and remainder registers are the same
        if (dr != dq)
        {
            D[dr] = remainder;
        }

        D[dq] = quotient;
        n = (quotient & 0x80000000) != 0;
        z = quotient == 0;
        v = false;
        c = false;
    }

    private void OpBitField()
    {
        var ext = FetchWord();
        var type = (opcode >> 8) & 7;
        var offset = (ext & 0x800) != 0 ? (int)D[(ext >> 6) & 7] : (ext >> 6) & 31;
        var width = (ext & 0x20) != 0 ? (int)(D[ext & 7] & 31) : ext & 31;
        if (width == 0)
        {
            width = 32;
        }

        var reg = (ext >> 12) & 7;
        var widthMask = width == 32 ? 0xFFFFFFFFu : (1u << width) - 1;

        if (EaMode == 0)
        {
            var dataReg = EaReg;
            offset &= 31;
            var value = D[dataReg];
            var rotated = offset == 0 ? value : (value << offset) | (value >> (32 - offset));
            var field = rotated >> (32 - width);

            if (!BitFieldOperation(type, field, width, widthMask, offset, reg, out var newField))
            {
                return;
            }

            var topMask = widthMask << (32 - width);
            var top = (newField & widthMask) << (32 - width);
            var mask = offset == 0 ? topMask : (topMask >> offset) | (topMask << (32 - offset));
            var aligned = offset == 0 ? top : (top >> offset) | (top << (32 - offset));
            D[dataReg] = (value & ~mask) | (aligned & mask);
            return;
        }

        // memory bit field can span up to 5 bytes
        var address = EaAddress(EaMode, EaReg) + (uint)(offset >> 3);
        var bitOffset = offset & 7;
        var byteCount = (bitOffset + width + 7) / 8;
        var data = 0UL;
        for (var i = 0; i < byteCount; i++)
        {
            data = (data << 8) | Read8(address + (uint)i);
        }

        var shift = byteCount * 8 - bitOffset - width;
        var memoryField = (uint)((data >> shift) & widthMask);

        if (!BitFieldOperation(type, memoryField, width, widthMask, offset, reg, out var newMemoryField))
        {
            return;
        }

        var fieldMask = (ulong)widthMask << shift;
        data = (data & ~fieldMask) | (((ulong)newMemoryField << shift) & fieldMask);
        for (var i = byteCount - 1; i >= 0; i--)
        {
            Write8(address + (uint)i, (uint)data);
            data >>= 8;
        }
    }

    /// <summary>
    /// Perform bit field operation setting flags.
    /// </summary>
    /// <returns>True, if new field is written.</returns>
    private bool BitFieldOperation(int type, uint field, int width, uint widthMask, int offset, int reg,
        out uint newField)
    {
        var msb = 1u << (width - 1);
        n = (field & msb) != 0;
        z = field == 0;
        v = false;
        c = false;
        newField = field;

        switch (type)
        {
            case 0: // bftst
                return false;
            case 1: // bfextu
                D[reg] = field;
                return false;
            case 2: // bfchg
                newField = ~field & widthMask;
                return true;
            case 3: // bfexts
                D[reg] = n ? field | ~widthMask : field;
                return false;
            case 4: // bfclr
                newField = 0;
                return true;
            case 5: // bfffo
                var bit = 0;
                while (bit < width && (field & (msb >> bit)) == 0)
                {
                    bit++;
                }

                D[reg] = (uint)(offset + bit);
                return false;
            case 6: // bfset
                newField = widthMask;
                return true;
            default: // bfins
                newField = D[reg] & widthMask;
                n = (newField & msb) != 0;
                z = newField == 0;
                return true;
        }
    }

    private void OpCas()
    {
        var size = ((opcode >> 9) & 3) switch
        {
            1 => 1,
            2 => 2,
            _ => 4
        };

        var ext = FetchWord();
        var dc = ext & 7;
        var du = (ext >> 6) & 7;
        DecodeEa(EaMode, EaReg, size);
        var destination = ReadEaValue(size);
        Sub(D[dc], destination, size, compare: true);
        if (z)
        {
            WriteEaValue(size, D[du]);
        }
        else
        {
            var mask = SizeMask(size);
            D[dc] = (D[dc] & ~mask) | destination;
        }
    }

    private void OpCas2()
    {
        var size = opcode == 0x0CFC ? 2 : 4;
        var ext1 = FetchWord();
        var ext2 = FetchWord();
        var address1 = (ext1 & 0x8000) != 0 ? A[(ext1 >> 12) & 7] : D[(ext1 >> 12) & 7];
        var address2 = (ext2 & 0x8000) != 0 ? A[(ext2 >> 12) & 7] : D[(ext2 >> 12) & 7];
        var dc1 = ext1 & 7;
        var dc2 = ext2 & 7;
        var destination1 = Read(address1, size);
        var destination2 = Read(address2, size);

        Sub(D[dc1], destination1, size, compare: true);
        if (z)
        {
            Sub(D[dc2], destination2, size, compare: true);
            if (z)
            {
                Write(address1, size, D[(ext1 >> 6) & 7]);
                Write(address2, size, D[(ext2 >> 6) & 7]);
                return;
            }
        }

        var mask = SizeMask(size);
        D[dc1] = (D[dc1] & ~mask) | destination1;
        D[dc2] = (D[dc2] & ~mask) | destination2;
    }

    private void OpChk2Cmp2()
    {
        var size = ((opcode >> 9) & 3) switch
        {
            0 => 1,
            1 => 2,
            _ => 4
        };

        var ext = FetchWord();
        var reg = (ext >> 12) & 7;
        var isAddress = (ext & 0x8000) != 0;
        var isChk = (ext & 0x800) != 0;
        var address = EaAddress(EaMode, EaReg);
        var lower = Read(address, size);
        var upper = Read(address + (uint)size, size);

        uint compare;
        if (isAddress)
        {
            // address registers are compared as long with bounds sign extended
            compare = A[reg];
            lower = SignExtend(lower, size);
            upper = SignExtend(upper, size);
        }
        else
        {
            compare = D[reg] & SizeMask(size);
        }

        z = compare == lower || compare == upper;
        c = lower <= upper ? compare < lower || compare > upper : compare > upper && compare < lower;

        if (isChk && c)
        {
            RaiseException(ExceptionVectors.Chk, PC, format2: true);
        }
    }
}
