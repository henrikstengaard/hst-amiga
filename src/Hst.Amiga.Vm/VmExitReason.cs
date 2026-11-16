namespace Hst.Amiga.Vm;

public enum VmExitReason
{
    /// <summary>
    /// Program returned from start code.
    /// </summary>
    Returned,

    /// <summary>
    /// Program called dos.library Exit().
    /// </summary>
    Exit,

    /// <summary>
    /// Cpu exception without exception handler, e.g. illegal instruction or address error.
    /// </summary>
    CpuException,

    /// <summary>
    /// Program called a library function, which is not implemented.
    /// </summary>
    UnimplementedFunction,

    /// <summary>
    /// Program execution failed in emulated library or virtual machine.
    /// </summary>
    Error,

    /// <summary>
    /// Max number of instructions was executed.
    /// </summary>
    InstructionLimit,

    /// <summary>
    /// Program execution was cancelled.
    /// </summary>
    Cancelled
}
