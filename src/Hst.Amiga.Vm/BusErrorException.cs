namespace Hst.Amiga.Vm;

using System;

/// <summary>
/// Exception thrown when accessing memory outside of available memory.
/// </summary>
public class BusErrorException : Exception
{
    public BusErrorException(uint address, bool write)
        : base($"Bus error {(write ? "writing" : "reading")} address ${address:X8}")
    {
        Address = address;
        Write = write;
    }

    public uint Address { get; }
    public bool Write { get; }
}
