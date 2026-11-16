namespace Hst.Amiga.Vm.Libraries.MathLibraries;

/// <summary>
/// Floating point format used by math libraries.
/// </summary>
public enum MathFormat
{
    /// <summary>
    /// Motorola Fast Floating Point used by mathffp.library and mathtrans.library.
    /// </summary>
    Ffp,

    /// <summary>
    /// IEEE single precision used by mathieeesingbas.library and mathieeesingtrans.library.
    /// </summary>
    IeeeSingle,

    /// <summary>
    /// IEEE double precision used by mathieeedoubbas.library and mathieeedoubtrans.library.
    /// </summary>
    IeeeDouble
}
