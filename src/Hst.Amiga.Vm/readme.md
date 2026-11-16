# Hst Amiga Vm

Hst Amiga Vm is a virtual machine for running Amiga CLI executables on Windows, macOS and Linux, similar to vamos from amitools.
Amiga hunk executables are loaded into emulated memory and run on an interpreted Motorola 68000 or 68020 cpu, while library calls are handled by exec.library, dos.library, utility.library and math libraries emulated in C#.

Features:
- Interpreting 68000 and 68020 cpu with full integer instruction set, including 68020 bit field, 64-bit multiply and divide, CAS/CAS2, CHK2/CMP2 and memory indirect addressing modes. FPU and MMU is not supported.
- Loads hunk executables with code, data, bss, reloc32, reloc32 short, symbol, debug and name hunks.
- Emulated exec.library with memory, memory pools, lists, signals, message ports, RawDoFmt() and SetFunction().
- Emulated dos.library mapping files, directories, locks and console to host:
    - Open, read, write, seek and close files and console.
    - Lock, examine, ExNext, ExAll, create, delete and rename files and directories.
    - ReadArgs templates, MatchFirst/MatchNext and AmigaDOS patterns.
    - VPrintf, VFWritef, FPuts, FGets and other buffered functions.
    - Environment variables stored in ENV:.
- Emulated utility.library with tag item, date, 32-bit math and string functions.
- Emulated math libraries with Motorola Fast Floating Point, IEEE single and IEEE double precision: mathffp.library, mathtrans.library, mathieeesingbas.library, mathieeesingtrans.library, mathieeedoubbas.library and mathieeedoubtrans.library.
- Stub libraries, which can be opened, but stop the program if a function is called. Default stubs are intuition.library and graphics.library, which are often opened by compiler startup code.
- Volumes mapping Amiga volume names to host directories and assigns mapping names to Amiga paths.
- UAE metadata with Amiga names, protection bits, comments and dates stored as UAEFSDB `_UAEFSDB.___` files used by WinUAE or UAE metafiles `.uaem` used by FS-UAE.
- AmigaDOS shell running scripts and interactive command lines:
    - Scripts run with Execute command from C:, e.g. from an AmigaOS 3.1 Workbench installation, including `.KEY` arguments handled by Execute.
    - Shell internal commands of AmigaOS 3.1 shell: Alias, Ask, CD, Echo, Else, EndCLI, EndIf, EndShell, EndSkip, FailAt, Fault, Get, GetEnv, If, Lab, Path, Prompt, Quit, Resident, Run, Set, SetEnv, Skip, Stack, UnAlias, UnSet, UnSetEnv and Why.
    - Local and global variables with `$name`, command output substitution with backquotes, aliases and redirection with `<`, `>` and `>>` directly after command name.
    - Commands are found in current directory, command path and C:. Files with script protection bit are run with Execute.
    - dos.library RunCommand(), Execute(), SystemTagList() and ReadItem() run commands from programs.

Limitations:
- Multitasking isn't supported. Programs run as a single process and waiting for signals or messages, which will never arrive, stops the program. Commands started with Run, Execute() or SystemTagList() run synchronously.
- Devices and libraries other than exec.library, dos.library, utility.library and math libraries aren't available, e.g. icon.library and reqtools.library. OpenLibrary() returns NULL for these.
- NewShell, NewCLI and pipes aren't supported.
- UAEFSDB stored as NTFS alternate data streams by WinUAE isn't supported, only `_UAEFSDB.___` files.

## Usage

Example of running an Amiga executable with SYS: mapped to a host directory:
```csharp
using Hst.Amiga.Vm;
using Hst.Amiga.Vm.Processor;

var options = new VmOptions
{
    CpuType = CpuType.M68020
};
options.Volumes["SYS"] = @"c:\amiga\sys";

var vm = new AmigaVm(options);
var result = await vm.Run(@"c:\amiga\sys\c\list", "SYS:");

Console.WriteLine($"Return code {result.ReturnCode}");
```

Default volumes and assigns:
- SYS: is mapped to the current directory, if no volumes are defined.
- RAM: is mapped to a temporary directory, which is deleted after the program exits.
- PROGDIR: is the directory of the executable.
- C:, S:, L:, LIBS:, DEVS:, FONTS: and LOCALE: are assigned to directories in SYS:.
- ENV: is assigned to RAM:Env, ENVARC: to SYS:Prefs/Env-Archive and T: to RAM:T.

Console input and output defaults to standard input and output, and can be redirected with the `Input`, `Output` and `Error` streams in `VmOptions`.

Example of running an AmigaDOS script with Execute command from C: and an interactive shell:
```csharp
var options = new VmOptions();
options.Volumes["SYS"] = @"c:\amiga\sys";

// script can be a host path or an Amiga path
var result = await new AmigaVm(options).Execute("SYS:S/SPat", "list sys:c/a#?");

// interactive shell reading command lines from console input
await new AmigaVm(options).Shell();
```

A program, which sets current input of the command line interface to a script like Execute does, continues running the script in the shell after the program returns, so `Run` of the Execute command runs scripts too.

## UAE metadata

Host file systems can't store Amiga protection bits and comments, and Amiga names can contain characters like `*`, `?` and `<`, which are invalid on host file systems. WinUAE and FS-UAE store these as metadata files in directories added as harddrives and the virtual machine uses the same metadata, set with `UaeMetadata` in `VmOptions`:
- `Auto` (default): Detects metadata per directory. Directories with a `_UAEFSDB.___` file use UAEFSDB and directories with `.uaem` files use UAE metafiles. Directories without metadata use the format of their parent directories within the volume, otherwise UAEFSDB.
- `UaeFsDb`: UAEFSDB with one `_UAEFSDB.___` file per directory used by WinUAE. Amiga names with special characters are stored as host names prefixed with `__uae___`.
- `UaeMetafile`: UAE metafiles with one `.uaem` file per file used by FS-UAE. Amiga names with special characters are stored as host names with characters encoded as `%xx`.
- `None`: Host names are used as Amiga names and metadata files are shown as regular files.

Metadata files are hidden from directory listings, when UAE metadata is used.

Writing metadata is disabled by default with `WriteUaeMetadata` set to false, so metadata files are never created, changed or deleted and changes to protection bits and comments are kept in memory only while the program runs. Setting `WriteUaeMetadata` to true writes changes from e.g. `Protect`, `FileNote` and `Rename` to metadata files.
