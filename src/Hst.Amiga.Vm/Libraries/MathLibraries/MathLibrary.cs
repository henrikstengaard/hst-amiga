namespace Hst.Amiga.Vm.Libraries.MathLibraries;

using System;
using System.Linq;
using Exec;

/// <summary>
/// Base class for math libraries with operand and result handling for FFP, IEEE single and IEEE double formats.
/// FFP and IEEE single values are passed in D0 and D1. IEEE double values are passed in D0/D1 and D2/D3 with high
/// long word in lowest register.
/// </summary>
public abstract class MathLibrary : Library
{
    private readonly ExecLibrary exec;
    private readonly string[] functionNames;

    protected MathLibrary(VmContext context, ExecLibrary exec, MathFormat format, string[] functions)
        : base(context)
    {
        this.exec = exec;
        Format = format;
        functionNames = functions.Select(x => Prefix + x).ToArray();
    }

    public MathFormat Format { get; }

    public override ushort Version => 40;

    protected override string[] FunctionNames => functionNames;

    /// <summary>
    /// Prefix of function names, e.g. SP for SPAdd.
    /// </summary>
    protected string Prefix => Format switch
    {
        MathFormat.Ffp => "SP",
        MathFormat.IeeeSingle => "IEEESP",
        _ => "IEEEDP"
    };

    protected override void Initialize()
    {
        exec.AddLibraryNode(this);
    }

    /// <summary>
    /// Register handler for function without prefix, e.g. Add for SPAdd.
    /// </summary>
    protected void RegisterFunction(string function, Action handler) => Register(Prefix + function, handler);

    /// <summary>
    /// First operand in D0 or D0/D1 for IEEE double.
    /// </summary>
    protected double Left => Format switch
    {
        MathFormat.Ffp => FfpConverter.ToDouble(D0),
        MathFormat.IeeeSingle => ToSingle(D0),
        _ => ToDouble(D0, D1)
    };

    /// <summary>
    /// Second operand in D1 or D2/D3 for IEEE double.
    /// </summary>
    protected double Right => Format switch
    {
        MathFormat.Ffp => FfpConverter.ToDouble(D1),
        MathFormat.IeeeSingle => ToSingle(D1),
        _ => ToDouble(D2, D3)
    };

    /// <summary>
    /// Set floating point result in D0 or D0/D1 for IEEE double and set condition codes.
    /// </summary>
    protected void SetFloat(double value)
    {
        var overflow = false;
        switch (Format)
        {
            case MathFormat.Ffp:
                D0 = FfpConverter.FromDouble(value, out overflow);
                Cpu.N = (D0 & 0x80) != 0;
                Cpu.Z = D0 == 0;
                break;
            case MathFormat.IeeeSingle:
                D0 = FromSingle((float)value);
                Cpu.N = (D0 & 0x80000000) != 0;
                Cpu.Z = (D0 & 0x7FFFFFFF) == 0;
                break;
            default:
                var bits = (ulong)BitConverter.DoubleToInt64Bits(value);
                D0 = (uint)(bits >> 32);
                D1 = (uint)bits;
                Cpu.N = (D0 & 0x80000000) != 0;
                Cpu.Z = (bits & 0x7FFFFFFFFFFFFFFFUL) == 0;
                break;
        }

        Cpu.V = overflow;
        Cpu.C = false;
    }

    /// <summary>
    /// Set integer result in D0 and set condition codes.
    /// </summary>
    protected void SetInteger(int value)
    {
        D0 = (uint)value;
        Cpu.N = value < 0;
        Cpu.Z = value == 0;
        Cpu.V = false;
        Cpu.C = false;
    }

    /// <summary>
    /// Write floating point value to memory.
    /// </summary>
    protected void WriteFloat(uint address, double value)
    {
        switch (Format)
        {
            case MathFormat.Ffp:
                Ram.WriteLong(address, FfpConverter.FromDouble(value, out _));
                break;
            case MathFormat.IeeeSingle:
                Ram.WriteLong(address, FromSingle((float)value));
                break;
            default:
                var bits = (ulong)BitConverter.DoubleToInt64Bits(value);
                Ram.WriteLong(address, (uint)(bits >> 32));
                Ram.WriteLong(address + 4, (uint)bits);
                break;
        }
    }

    /// <summary>
    /// Compare values returning 1, if left is greater than right, -1 if left is less than right, otherwise 0.
    /// </summary>
    protected static int Compare(double left, double right) => left > right ? 1 : left < right ? -1 : 0;

    /// <summary>
    /// Convert to integer truncating towards zero and saturating values out of range.
    /// </summary>
    protected static int Fix(double value)
    {
        if (double.IsNaN(value))
        {
            return 0;
        }

        if (value >= int.MaxValue)
        {
            return int.MaxValue;
        }

        if (value <= int.MinValue)
        {
            return int.MinValue;
        }

        return (int)value;
    }

    public static float ToSingle(uint bits) => BitConverter.Int32BitsToSingle((int)bits);

    public static uint FromSingle(float value) => (uint)BitConverter.SingleToInt32Bits(value);

    public static double ToDouble(uint high, uint low) =>
        BitConverter.Int64BitsToDouble((long)(((ulong)high << 32) | low));
}
