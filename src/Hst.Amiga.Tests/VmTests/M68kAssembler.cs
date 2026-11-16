namespace Hst.Amiga.Tests.VmTests;

using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Minimal 68k instruction encoder for writing test programs.
/// </summary>
public class M68kAssembler
{
    public enum Size
    {
        Byte,
        Word,
        Long
    }

    /// <summary>
    /// Effective address with mode, register and extension words.
    /// </summary>
    public class Ea
    {
        public int Mode { get; init; }
        public int Reg { get; init; }
        public ushort[] Extension { get; init; } = Array.Empty<ushort>();
        public uint? Immediate { get; init; }
        public string PcLabel { get; init; }
    }

    private readonly List<byte> bytes = new();
    private readonly Dictionary<string, int> labels = new();
    private readonly List<(int Position, int Base, string Label, bool IsLong)> fixups = new();

    public int Position => bytes.Count;

    public static Ea D(int n) => new() { Mode = 0, Reg = n };
    public static Ea A(int n) => new() { Mode = 1, Reg = n };
    public static Ea Ind(int n) => new() { Mode = 2, Reg = n };
    public static Ea PostInc(int n) => new() { Mode = 3, Reg = n };
    public static Ea PreDec(int n) => new() { Mode = 4, Reg = n };
    public static Ea Disp(short displacement, int n) => new() { Mode = 5, Reg = n, Extension = new[] { (ushort)displacement } };

    public static Ea Index(sbyte displacement, int an, int xn, bool indexIsAddress = false, bool longIndex = false,
        int scale = 1) =>
        new()
        {
            Mode = 6, Reg = an, Extension = new[] { BriefExtension(displacement, xn, indexIsAddress, longIndex, scale) }
        };

    public static Ea AbsW(short address) => new() { Mode = 7, Reg = 0, Extension = new[] { (ushort)address } };

    public static Ea AbsL(uint address) =>
        new() { Mode = 7, Reg = 1, Extension = new[] { (ushort)(address >> 16), (ushort)address } };

    public static Ea PcDisp(short displacement) => new() { Mode = 7, Reg = 2, Extension = new[] { (ushort)displacement } };
    public static Ea Pc(string label) => new() { Mode = 7, Reg = 2, PcLabel = label };
    public static Ea Imm(uint value) => new() { Mode = 7, Reg = 4, Immediate = value };

    public static ushort BriefExtension(sbyte displacement, int xn, bool indexIsAddress, bool longIndex, int scale)
    {
        var scaleBits = scale switch
        {
            2 => 1,
            4 => 2,
            8 => 3,
            _ => 0
        };

        return (ushort)((indexIsAddress ? 0x8000 : 0) | (xn << 12) | (longIndex ? 0x800 : 0) | (scaleBits << 9) |
                        (byte)displacement);
    }

    public byte[] ToArray()
    {
        foreach (var fixup in fixups)
        {
            if (!labels.TryGetValue(fixup.Label, out var target))
            {
                throw new InvalidOperationException($"Label '{fixup.Label}' is not defined");
            }

            var displacement = target - fixup.Base;
            if (fixup.IsLong)
            {
                bytes[fixup.Position] = (byte)(displacement >> 24);
                bytes[fixup.Position + 1] = (byte)(displacement >> 16);
                bytes[fixup.Position + 2] = (byte)(displacement >> 8);
                bytes[fixup.Position + 3] = (byte)displacement;
            }
            else
            {
                bytes[fixup.Position] = (byte)(displacement >> 8);
                bytes[fixup.Position + 1] = (byte)displacement;
            }
        }

        return bytes.ToArray();
    }

    public M68kAssembler Label(string name)
    {
        labels[name] = bytes.Count;
        return this;
    }

    public M68kAssembler Word(params ushort[] words)
    {
        foreach (var word in words)
        {
            bytes.Add((byte)(word >> 8));
            bytes.Add((byte)word);
        }

        return this;
    }

    public M68kAssembler Long(uint value) => Word((ushort)(value >> 16), (ushort)value);

    public M68kAssembler String(string value)
    {
        bytes.AddRange(Encoding.ASCII.GetBytes(value));
        bytes.Add(0);
        return this;
    }

    public M68kAssembler Bytes(params byte[] values)
    {
        bytes.AddRange(values);
        return this;
    }

    public M68kAssembler Align()
    {
        while (bytes.Count % 4 != 0)
        {
            bytes.Add(0);
        }

        return this;
    }

    private void Extensions(Ea ea, Size size)
    {
        if (ea.Immediate.HasValue)
        {
            if (size == Size.Long)
            {
                Long(ea.Immediate.Value);
            }
            else
            {
                Word((ushort)(size == Size.Byte ? ea.Immediate.Value & 0xFF : ea.Immediate.Value));
            }

            return;
        }

        if (ea.PcLabel != null)
        {
            fixups.Add((bytes.Count, bytes.Count, ea.PcLabel, false));
            Word(0);
            return;
        }

        Word(ea.Extension);
    }

    private static int EaBits(Ea ea) => (ea.Mode << 3) | ea.Reg;

    private static int SizeBits(Size size) => size switch
    {
        Size.Byte => 0,
        Size.Word => 1,
        _ => 2
    };

    public M68kAssembler Move(Size size, Ea source, Ea destination)
    {
        var sizeBits = size switch
        {
            Size.Byte => 1,
            Size.Word => 3,
            _ => 2
        };

        Word((ushort)((sizeBits << 12) | (destination.Reg << 9) | (destination.Mode << 6) | EaBits(source)));
        Extensions(source, size);
        Extensions(destination, size);
        return this;
    }

    public M68kAssembler Moveq(sbyte value, int dn) => Word((ushort)(0x7000 | (dn << 9) | (byte)value));

    public M68kAssembler Lea(Ea source, int an)
    {
        Word((ushort)(0x41C0 | (an << 9) | EaBits(source)));
        Extensions(source, Size.Long);
        return this;
    }

    public M68kAssembler Pea(Ea source)
    {
        Word((ushort)(0x4840 | EaBits(source)));
        Extensions(source, Size.Long);
        return this;
    }

    public M68kAssembler Jsr(Ea target)
    {
        Word((ushort)(0x4E80 | EaBits(target)));
        Extensions(target, Size.Long);
        return this;
    }

    /// <summary>
    /// Call library function with offset relative to library base in a6.
    /// </summary>
    public M68kAssembler CallLib(int offset) => Jsr(Disp((short)offset, 6));

    public M68kAssembler Jmp(Ea target)
    {
        Word((ushort)(0x4EC0 | EaBits(target)));
        Extensions(target, Size.Long);
        return this;
    }

    public M68kAssembler Rts() => Word(0x4E75);
    public M68kAssembler Nop() => Word(0x4E71);
    public M68kAssembler Trap(int vector) => Word((ushort)(0x4E40 | vector));

    private M68kAssembler Arithmetic(int line, Size size, Ea source, int dn)
    {
        Word((ushort)((line << 12) | (dn << 9) | (SizeBits(size) << 6) | EaBits(source)));
        Extensions(source, size);
        return this;
    }

    public M68kAssembler Add(Size size, Ea source, int dn) => Arithmetic(0xD, size, source, dn);
    public M68kAssembler Sub(Size size, Ea source, int dn) => Arithmetic(0x9, size, source, dn);
    public M68kAssembler Cmp(Size size, Ea source, int dn) => Arithmetic(0xB, size, source, dn);
    public M68kAssembler And(Size size, Ea source, int dn) => Arithmetic(0xC, size, source, dn);
    public M68kAssembler Or(Size size, Ea source, int dn) => Arithmetic(0x8, size, source, dn);

    public M68kAssembler Addq(Size size, int value, Ea destination)
    {
        Word((ushort)(0x5000 | ((value & 7) << 9) | (SizeBits(size) << 6) | EaBits(destination)));
        Extensions(destination, size);
        return this;
    }

    public M68kAssembler Subq(Size size, int value, Ea destination)
    {
        Word((ushort)(0x5100 | ((value & 7) << 9) | (SizeBits(size) << 6) | EaBits(destination)));
        Extensions(destination, size);
        return this;
    }

    public M68kAssembler Tst(Size size, Ea ea)
    {
        Word((ushort)(0x4A00 | (SizeBits(size) << 6) | EaBits(ea)));
        Extensions(ea, size);
        return this;
    }

    public M68kAssembler Clr(Size size, Ea ea)
    {
        Word((ushort)(0x4200 | (SizeBits(size) << 6) | EaBits(ea)));
        Extensions(ea, size);
        return this;
    }

    /// <summary>
    /// Branch with 16-bit displacement to label.
    /// </summary>
    public M68kAssembler Bcc(int condition, string label)
    {
        Word((ushort)(0x6000 | (condition << 8)));
        fixups.Add((bytes.Count, bytes.Count, label, false));
        Word(0);
        return this;
    }

    public M68kAssembler Bra(string label) => Bcc(0, label);
    public M68kAssembler Bsr(string label) => Bcc(1, label);
    public M68kAssembler Bne(string label) => Bcc(6, label);
    public M68kAssembler Beq(string label) => Bcc(7, label);

    public M68kAssembler Dbf(int dn, string label)
    {
        Word((ushort)(0x51C8 | dn));
        fixups.Add((bytes.Count, bytes.Count, label, false));
        Word(0);
        return this;
    }

    public M68kAssembler MovemToMemory(Size size, ushort mask, Ea destination)
    {
        Word((ushort)(0x4880 | (size == Size.Long ? 0x40 : 0) | EaBits(destination)), mask);
        Extensions(destination, size);
        return this;
    }

    public M68kAssembler MovemToRegisters(Size size, ushort mask, Ea source)
    {
        Word((ushort)(0x4C80 | (size == Size.Long ? 0x40 : 0) | EaBits(source)), mask);
        Extensions(source, size);
        return this;
    }
}
