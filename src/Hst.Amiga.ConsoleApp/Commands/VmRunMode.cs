namespace Hst.Amiga.ConsoleApp.Commands;

/// <summary>
/// What virtual machine command runs.
/// </summary>
public enum VmRunMode
{
    /// <summary>
    /// Run Amiga executable.
    /// </summary>
    Run,

    /// <summary>
    /// Execute AmigaDOS script with Execute command from C:.
    /// </summary>
    Execute,

    /// <summary>
    /// Run interactive AmigaDOS shell.
    /// </summary>
    Shell
}
