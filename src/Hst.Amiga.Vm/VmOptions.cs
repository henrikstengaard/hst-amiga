namespace Hst.Amiga.Vm;

using System;
using System.Collections.Generic;
using System.IO;
using FileSystem;
using Processor;
using Roms;

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
    /// Adf, ISO 9660 CD and disk images mounted as volumes. Adf images are mounted as DF0:, DF1: etc., CD images
    /// as CD0:, CD1: etc. and partitions in disk images with rigid disk block are mounted with their device names,
    /// e.g. DH0:. CD images are always write protected. Volumes are added with their
    /// volume names and device names are assigns to volumes. SYS: is assigned to first image, which is an adf, a CD image or
    /// the bootable partition with highest boot priority, if SYS isn't defined by volumes or assigns.
    /// </summary>
    public IList<VmImage> Images { get; set; } = new List<VmImage>();

    /// <summary>
    /// Assigns mapping Amiga assign names to Amiga paths, e.g. "C" to "SYS:C". Multiple paths separated with ;
    /// create a multi assign, e.g. "SYS:C;Work:C". Paths starting with + are added to the default assign instead
    /// of replacing it like Assign ADD, e.g. "+Work:C" for C: assigned to SYS:C and Work:C.
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
    /// Kickstart ROM mapped read-only at its base address, e.g. 0xF80000, read with
    /// <see cref="KickstartRomReader"/>. Resident modules in ROM are added to resident modules in ExecBase, so
    /// programs like Version and GetSCSI find them with FindResident(), and ExecBase tells Kickstart version of ROM.
    /// Code in ROM is not executed. Memory size must not exceed ROM base address. Default is no ROM.
    /// </summary>
    public KickstartRom KickstartRom { get; set; }

    /// <summary>
    /// UAE metadata used for Amiga names, protection bits, comments and dates of files in volume directories.
    /// Default is auto, which detects UAEFSDB and UAE metafiles per directory.
    /// </summary>
    public UaeMetadataMode UaeMetadata { get; set; } = UaeMetadataMode.Auto;

    /// <summary>
    /// Write changes to UAE metadata files. Default is false, which never creates, changes or deletes
    /// metadata files and keeps changes to protection bits and comments in memory while the program runs.
    /// </summary>
    public bool WriteUaeMetadata { get; set; }

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
    /// Translate Amiga console control sequences in console output and error output to ANSI terminal sequences,
    /// e.g. single byte control sequence introducer 0x9B, form feed clearing the window and colors selecting
    /// Workbench pens, see <see cref="AmigaConsoleOutputStream"/>. Default is true for standard output and
    /// standard error, which are not redirected, and false for streams set in options.
    /// </summary>
    public bool? TranslateConsoleOutput { get; set; }

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
