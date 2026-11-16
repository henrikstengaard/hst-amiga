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
    - VPrintf, FPuts, FGets and other buffered functions.
    - Environment variables stored in ENV:.
- Emulated utility.library with tag item, date, 32-bit math and string functions.
- Emulated math libraries with Motorola Fast Floating Point, IEEE single and IEEE double precision: mathffp.library, mathtrans.library, mathieeesingbas.library, mathieeesingtrans.library, mathieeedoubbas.library and mathieeedoubtrans.library.
- Stub libraries, which can be opened, but stop the program if a function is called. Default stubs are intuition.library and graphics.library, which are often opened by compiler startup code.
- Volumes mapping Amiga volume names to host directories and assigns mapping names to Amiga paths.

Limitations:
- Multitasking isn't supported. Programs run as a single process and waiting for signals or messages, which will never arrive, stops the program.
- Devices and libraries other than exec.library, dos.library, utility.library and math libraries aren't available, e.g. icon.library and reqtools.library. OpenLibrary() returns NULL for these.
- Running other programs with Execute() and SystemTagList() isn't supported.
- Protection bits and comments are kept in memory only while the program runs.

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
