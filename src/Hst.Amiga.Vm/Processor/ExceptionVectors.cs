namespace Hst.Amiga.Vm.Processor;

public static class ExceptionVectors
{
    public const int BusError = 2;
    public const int AddressError = 3;
    public const int IllegalInstruction = 4;
    public const int ZeroDivide = 5;
    public const int Chk = 6;
    public const int TrapV = 7;
    public const int PrivilegeViolation = 8;
    public const int Trace = 9;
    public const int LineA = 10;
    public const int LineF = 11;
    public const int FormatError = 14;
    public const int Trap0 = 32;
}
