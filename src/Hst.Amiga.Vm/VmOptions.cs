namespace Hst.Amiga.Vm;

using System;
using System.Collections.Generic;
using System.IO;
using Processor;

public class VmOptions
{
    public const uint DefaultMemorySize = 8 * 1024 * 1024;
    public const uint DefaultStackSize = 64 * 1024;

    /// <summary>
    /// Cpu type to emulate.
    /// </summary>
    public CpuType CpuType { get; set; } = CpuType.M68020;

    /// <summary>
    /// Size of memory in bytes, max 16 MB for 68000.
    /// </summary>
    public uint MemorySize { get; set; } = DefaultMemorySize;

    /// <summary>
    /// Size of program stack in bytes.
    /// </summary>
    public uint StackSize { get; set; } = DefaultStackSize;

    /// <summary>
    /// Volumes mapping Amiga volume names to host directories, e.g. "SYS" to "c:\amiga\sys".
    /// If no volumes are defined, volume SYS is mapped to host current directory.
    /// </summary>
    public IDictionary<string, string> Volumes { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Assigns mapping Amiga assign names to Amiga paths, e.g. "C" to "SYS:C".
    /// </summary>
    public IDictionary<string, string> Assigns { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Names of stub libraries, which can be opened, but stops program with unimplemented function, if any
    /// function is called. Compiler startup code often opens libraries like intuition.library without using them
    /// in CLI programs. Default is intuition.library and graphics.library.
    /// </summary>
    public ISet<string> StubLibraries { get; set; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "intuition.library", "graphics.library" };

    /// <summary>
    /// Amiga path to current directory of program. Default is root of SYS.
    /// </summary>
    public string CurrentDirectory { get; set; }

    /// <summary>
    /// Stream used for program console input. Default is standard input.
    /// </summary>
    public Stream Input { get; set; }

    /// <summary>
    /// Stream used for program console output. Default is standard output.
    /// </summary>
    public Stream Output { get; set; }

    /// <summary>
    /// Stream used for program console error output. Default is standard error.
    /// </summary>
    public Stream Error { get; set; }

    /// <summary>
    /// Console input is interactive. Default is true, if standard input is not redirected.
    /// </summary>
    public bool? Interactive { get; set; }

    /// <summary>
    /// Max number of instructions to execute, zero for unlimited.
    /// </summary>
    public long MaxInstructions { get; set; }

    /// <summary>
    /// Trace each instruction executed with registers.
    /// </summary>
    public bool TraceInstructions { get; set; }

    /// <summary>
    /// Trace each library function called.
    /// </summary>
    public bool TraceLibraryCalls { get; set; }

    /// <summary>
    /// Action receiving trace and warning messages. Default writes to standard error.
    /// </summary>
    public Action<string> Log { get; set; }
}
