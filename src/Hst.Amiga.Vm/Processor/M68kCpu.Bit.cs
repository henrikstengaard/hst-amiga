namespace Hst.Amiga.Vm.Processor;

public sealed partial class M68kCpu
{
    private void OpBitDynamic()
    {
        BitOperation((opcode >> 6) & 3, D[Reg911]);
    }

    private void OpBitStatic()
    {
        var bitNumber = FetchWord() & 0xFFu;
        BitOperation((opcode >> 6) & 3, bitNumber);
    }

    /// <summary>
    /// Bit operation on long for data register and byte for memory.
    /// </summary>
    /// <param name="type">0 btst, 1 bchg, 2 bclr, 3 bset.</param>
    /// <param name="bitNumber">Bit number, modulo 32 for data register and modulo 8 for memory.</param>
    private void BitOperation(int type, uint bitNumber)
    {
        var size = EaMode == 0 ? 4 : 1;
        var bit = 1u << (int)(bitNumber & (size == 4 ? 31u : 7u));
        DecodeEa(EaMode, EaReg, size);
        var value = ReadEaValue(size);
        z = (value & bit) == 0;
        switch (type)
        {
            case 1:
                WriteEaValue(size, value ^ bit);
                break;
            case 2:
                WriteEaValue(size, value & ~bit);
                break;
            case 3:
                WriteEaValue(size, value | bit);
                break;
        }
    }
}
