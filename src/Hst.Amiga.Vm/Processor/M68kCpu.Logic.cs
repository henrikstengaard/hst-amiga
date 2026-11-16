namespace Hst.Amiga.Vm.Processor;

public sealed partial class M68kCpu
{
    private uint FetchImmediate(int size) => size == 4 ? FetchLong() : FetchWord() & SizeMask(size);

    private void OpOri()
    {
        var size = Size76;
        var source = FetchImmediate(size);
        DecodeEa(EaMode, EaReg, size);
        var result = ReadEaValue(size) | source;
        WriteEaValue(size, result);
        SetLogicFlags(result, size);
    }

    private void OpAndi()
    {
        var size = Size76;
        var source = FetchImmediate(size);
        DecodeEa(EaMode, EaReg, size);
        var result = ReadEaValue(size) & source;
        WriteEaValue(size, result);
        SetLogicFlags(result, size);
    }

    private void OpEori()
    {
        var size = Size76;
        var source = FetchImmediate(size);
        DecodeEa(EaMode, EaReg, size);
        var result = ReadEaValue(size) ^ source;
        WriteEaValue(size, result);
        SetLogicFlags(result, size);
    }

    private void OpOrToRegister()
    {
        var size = Size76;
        var reg = Reg911;
        var mask = SizeMask(size);
        var result = (ReadEa(EaMode, EaReg, size) | D[reg]) & mask;
        D[reg] = (D[reg] & ~mask) | result;
        SetLogicFlags(result, size);
    }

    private void OpOrToEa()
    {
        var size = Size76;
        DecodeEa(EaMode, EaReg, size);
        var result = ReadEaValue(size) | D[Reg911];
        WriteEaValue(size, result);
        SetLogicFlags(result, size);
    }

    private void OpAndToRegister()
    {
        var size = Size76;
        var reg = Reg911;
        var mask = SizeMask(size);
        var result = ReadEa(EaMode, EaReg, size) & D[reg] & mask;
        D[reg] = (D[reg] & ~mask) | result;
        SetLogicFlags(result, size);
    }

    private void OpAndToEa()
    {
        var size = Size76;
        DecodeEa(EaMode, EaReg, size);
        var result = ReadEaValue(size) & D[Reg911];
        WriteEaValue(size, result);
        SetLogicFlags(result, size);
    }

    private void OpEor()
    {
        var size = Size76;
        DecodeEa(EaMode, EaReg, size);
        var result = ReadEaValue(size) ^ D[Reg911];
        WriteEaValue(size, result);
        SetLogicFlags(result, size);
    }

    private void OpNot()
    {
        var size = Size76;
        DecodeEa(EaMode, EaReg, size);
        var result = ~ReadEaValue(size) & SizeMask(size);
        WriteEaValue(size, result);
        SetLogicFlags(result, size);
    }
}
