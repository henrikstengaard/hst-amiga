namespace Hst.Amiga.Vm;

public class VmResult
{
    /// <summary>
    /// Reason program execution stopped.
    /// </summary>
    public VmExitReason ExitReason { get; set; }

    /// <summary>
    /// Return code of program returned in D0 or passed to dos.library Exit(). Return codes are 0 ok, 5 warn,
    /// 10 error and 20 failure.
    /// </summary>
    public int ReturnCode { get; set; }

    /// <summary>
    /// Last dos error code returned by IoErr().
    /// </summary>
    public int IoErr { get; set; }

    /// <summary>
    /// Error message, if program execution stopped with an error.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Number of instructions executed.
    /// </summary>
    public long InstructionCount { get; set; }

    public bool IsSuccess => ExitReason is VmExitReason.Returned or VmExitReason.Exit;
}
