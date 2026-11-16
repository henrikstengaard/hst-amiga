namespace Hst.Amiga.Vm.Libraries.MathLibraries;

using System;
using Exec;

/// <summary>
/// Emulated basic math library with conversion, compare and arithmetic functions:
/// mathffp.library for FFP, mathieeesingbas.library for IEEE single and mathieeedoubbas.library for IEEE double.
/// </summary>
public class MathBasLibrary : MathLibrary
{
    private static readonly string[] Functions =
    {
        "Fix", "Flt", "Cmp", "Tst", "Abs", "Neg", "Add", "Sub", "Mul", "Div", "Floor", "Ceil"
    };

    public MathBasLibrary(VmContext context, ExecLibrary exec, MathFormat format)
        : base(context, exec, format, Functions)
    {
    }

    public override string Name => Format switch
    {
        MathFormat.Ffp => "mathffp.library",
        MathFormat.IeeeSingle => "mathieeesingbas.library",
        _ => "mathieeedoubbas.library"
    };

    protected override void RegisterFunctions()
    {
        RegisterFunction("Fix", () => SetInteger(Fix(Left)));
        RegisterFunction("Flt", () => SetFloat((int)D0));

        // ffp compare and test have operands in d1,d0 and d1, ieee in d0,d1 and d0
        RegisterFunction("Cmp", () => SetInteger(Format == MathFormat.Ffp
            ? Compare(Right, Left)
            : Compare(Left, Right)));
        RegisterFunction("Tst", () => SetInteger(Compare(Format == MathFormat.Ffp ? Right : Left, 0)));

        RegisterFunction("Abs", () => SetFloat(Math.Abs(Left)));
        RegisterFunction("Neg", () => SetFloat(-Left));
        RegisterFunction("Add", () => SetFloat(Left + Right));
        RegisterFunction("Sub", () => SetFloat(Left - Right));
        RegisterFunction("Mul", () => SetFloat(Left * Right));
        RegisterFunction("Div", () => SetFloat(Left / Right));
        RegisterFunction("Floor", () => SetFloat(Math.Floor(Left)));
        RegisterFunction("Ceil", () => SetFloat(Math.Ceiling(Left)));
    }
}
