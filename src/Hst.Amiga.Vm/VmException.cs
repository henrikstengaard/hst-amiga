namespace Hst.Amiga.Vm;

using System;

/// <summary>
/// Exception thrown when program execution fails in emulated libraries or virtual machine.
/// </summary>
public class VmException : Exception
{
    public VmException(string message) : base(message)
    {
    }
}
