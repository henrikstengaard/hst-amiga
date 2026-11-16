namespace Hst.Amiga.Vm.Processor;

using System;

/// <summary>
/// Exception thrown when cpu raises an exception without an exception handler installed.
/// </summary>
public class CpuException : Exception
{
    public CpuException(int vector, uint pc, ushort opcode, string registers, string detail = null)
        : base(
            $"{VectorName(vector)} (vector {vector}) at PC ${pc:X8}, opcode ${opcode:X4}{(string.IsNullOrEmpty(detail) ? string.Empty : $", {detail}")}")
    {
        Vector = vector;
        Pc = pc;
        Opcode = opcode;
        Registers = registers;
    }

    public int Vector { get; }
    public uint Pc { get; }
    public ushort Opcode { get; }

    /// <summary>
    /// Register dump at time of exception.
    /// </summary>
    public string Registers { get; }

    public static string VectorName(int vector) => vector switch
    {
        ExceptionVectors.BusError => "Bus error",
        ExceptionVectors.AddressError => "Address error",
        ExceptionVectors.IllegalInstruction => "Illegal instruction",
        ExceptionVectors.ZeroDivide => "Divide by zero",
        ExceptionVectors.Chk => "CHK instruction",
        ExceptionVectors.TrapV => "TRAPV/TRAPcc instruction",
        ExceptionVectors.PrivilegeViolation => "Privilege violation",
        ExceptionVectors.Trace => "Trace",
        ExceptionVectors.LineA => "Line-A emulator",
        ExceptionVectors.LineF => "Line-F emulator",
        ExceptionVectors.FormatError => "Format error",
        >= ExceptionVectors.Trap0 and <= ExceptionVectors.Trap0 + 15 => $"TRAP #{vector - ExceptionVectors.Trap0}",
        _ => "Exception"
    };
}
