namespace Hst.Amiga.Vm.Libraries.Dos;

using System.IO;
using FileSystem;

/// <summary>
/// Settings for process running program.
/// </summary>
public class ProcessSetup
{
    /// <summary>
    /// Name of program used for task name and cli command name.
    /// </summary>
    public string ProgramName { get; set; }

    /// <summary>
    /// Argument string terminated with newline.
    /// </summary>
    public string Arguments { get; set; }

    public uint SegList { get; set; }
    public uint StackLower { get; set; }
    public uint StackUpper { get; set; }
    public uint StackSize { get; set; }
    public AmigaPath CurrentDirectory { get; set; }
    public AmigaPath HomeDirectory { get; set; }
    public Stream Input { get; set; }
    public Stream Output { get; set; }
    public Stream Error { get; set; }
    public bool Interactive { get; set; }
}
