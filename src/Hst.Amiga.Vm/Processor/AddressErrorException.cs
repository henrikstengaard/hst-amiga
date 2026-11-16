namespace Hst.Amiga.Vm.Processor;

using System;

/// <summary>
/// Exception thrown internally by cpu on word or long access of odd address.
/// </summary>
internal class AddressErrorException : Exception
{
    public AddressErrorException(uint address) : base($"Address error accessing ${address:X8}")
    {
        Address = address;
    }

    public uint Address { get; }
}
