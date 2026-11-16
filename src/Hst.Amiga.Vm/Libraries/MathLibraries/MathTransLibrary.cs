namespace Hst.Amiga.Vm.Libraries.MathLibraries;

using System;
using Exec;

/// <summary>
/// Emulated transcendental math library with trigonometric, exponential and logarithmic functions:
/// mathtrans.library for FFP, mathieeesingtrans.library for IEEE single and mathieeedoubtrans.library for
/// IEEE double.
/// </summary>
public class MathTransLibrary : MathLibrary
{
    private static readonly string[] Functions =
    {
        "Atan", "Sin", "Cos", "Tan", "Sincos", "Sinh", "Cosh", "Tanh", "Exp", "Log", "Pow", "Sqrt", "Tieee",
        "Fieee", "Asin", "Acos", "Log10"
    };

    public MathTransLibrary(VmContext context, ExecLibrary exec, MathFormat format)
        : base(context, exec, format, Functions)
    {
    }

    public override string Name => Format switch
    {
        MathFormat.Ffp => "mathtrans.library",
        MathFormat.IeeeSingle => "mathieeesingtrans.library",
        _ => "mathieeedoubtrans.library"
    };

    protected override void RegisterFunctions()
    {
        RegisterFunction("Atan", () => SetFloat(Math.Atan(Left)));
        RegisterFunction("Sin", () => SetFloat(Math.Sin(Left)));
        RegisterFunction("Cos", () => SetFloat(Math.Cos(Left)));
        RegisterFunction("Tan", () => SetFloat(Math.Tan(Left)));
        RegisterFunction("Sincos", () =>
        {
            // returns sine and writes cosine to pointer in d1 for ffp and a0 for ieee
            var value = Left;
            var cosinePointer = Format == MathFormat.Ffp ? D1 : A0;
            WriteFloat(cosinePointer, Math.Cos(value));
            SetFloat(Math.Sin(value));
        });
        RegisterFunction("Sinh", () => SetFloat(Math.Sinh(Left)));
        RegisterFunction("Cosh", () => SetFloat(Math.Cosh(Left)));
        RegisterFunction("Tanh", () => SetFloat(Math.Tanh(Left)));
        RegisterFunction("Exp", () => SetFloat(Math.Exp(Left)));
        RegisterFunction("Log", () => SetFloat(Math.Log(Left)));

        // argument in d0 (d0/d1) raised to power in d1 (d2/d3)
        RegisterFunction("Pow", () => SetFloat(Math.Pow(Left, Right)));
        RegisterFunction("Sqrt", () => SetFloat(Math.Sqrt(Left)));

        // convert to and from ieee single in d0
        RegisterFunction("Tieee", () =>
        {
            D0 = FromSingle((float)Left);
            Cpu.N = (D0 & 0x80000000) != 0;
            Cpu.Z = (D0 & 0x7FFFFFFF) == 0;
            Cpu.V = false;
            Cpu.C = false;
        });
        RegisterFunction("Fieee", () => SetFloat(ToSingle(D0)));

        RegisterFunction("Asin", () => SetFloat(Math.Asin(Left)));
        RegisterFunction("Acos", () => SetFloat(Math.Acos(Left)));
        RegisterFunction("Log10", () => SetFloat(Math.Log10(Left)));
    }
}
