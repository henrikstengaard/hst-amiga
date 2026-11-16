namespace Hst.Amiga.Tests.VmTests;

using Vm;
using Vm.Processor;

internal static class CpuTestHelper
{
    public const uint CodeAddress = 0x1000;
    public const uint DefaultExceptionHandler = 0x400;
    public const uint SupervisorStack = 0x80000;
    public const uint UserStack = 0x70000;

    public static M68kCpu CreateCpu(CpuType type, params ushort[] code)
    {
        var bytes = new byte[code.Length * 2];
        for (var i = 0; i < code.Length; i++)
        {
            bytes[i * 2] = (byte)(code[i] >> 8);
            bytes[i * 2 + 1] = (byte)code[i];
        }

        return CreateCpu(type, bytes);
    }

    public static M68kCpu CreateCpu(CpuType type, byte[] code)
    {
        var ram = new Ram(0x100000, type == CpuType.M68000 ? 0xFFFFFFu : 0xFFFFFFFFu);
        var cpu = new M68kCpu(ram, type)
        {
            DefaultExceptionHandler = DefaultExceptionHandler
        };

        for (var vector = 0U; vector < 256; vector++)
        {
            ram.WriteLong(vector * 4, DefaultExceptionHandler);
        }

        ram.WriteBytes(CodeAddress, code);
        cpu.A[7] = SupervisorStack;
        cpu.PC = CodeAddress;
        return cpu;
    }

    /// <summary>
    /// Switch cpu to user mode with user stack.
    /// </summary>
    public static void EnterUserMode(M68kCpu cpu)
    {
        cpu.Sr = 0x0000;
        cpu.A[7] = UserStack;
    }

    public static void Step(M68kCpu cpu, int count)
    {
        for (var i = 0; i < count; i++)
        {
            cpu.Step();
        }
    }
}
