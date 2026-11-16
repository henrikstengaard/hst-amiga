namespace Hst.Amiga.Vm;

using System;

/// <summary>
/// Exception thrown to unwind execution, when program exits.
/// </summary>
internal class ProgramExitException : Exception
{
    public ProgramExitException(int returnCode, bool calledExit) : base($"Program exited with return code {returnCode}")
    {
        ReturnCode = returnCode;
        CalledExit = calledExit;
    }

    public int ReturnCode { get; }

    /// <summary>
    /// Program called dos.library Exit().
    /// </summary>
    public bool CalledExit { get; }
}
