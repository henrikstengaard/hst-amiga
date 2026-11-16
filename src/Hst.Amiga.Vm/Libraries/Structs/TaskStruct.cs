namespace Hst.Amiga.Vm.Libraries.Structs;

/// <summary>
/// Offsets of struct Task.
/// </summary>
public static class TaskStruct
{
    public const int Node = 0;
    public const int Flags = 14;
    public const int State = 15;
    public const int IdNestCnt = 16;
    public const int TdNestCnt = 17;
    public const int SigAlloc = 18;
    public const int SigWait = 22;
    public const int SigRecvd = 26;
    public const int SigExcept = 30;
    public const int TrapAlloc = 34;
    public const int TrapAble = 36;
    public const int ExceptData = 38;
    public const int ExceptCode = 42;
    public const int TrapData = 46;
    public const int TrapCode = 50;
    public const int SpReg = 54;
    public const int SpLower = 58;
    public const int SpUpper = 62;
    public const int Switch = 66;
    public const int Launch = 70;
    public const int MemEntry = 74;
    public const int UserData = 88;
    public const int Size = 92;
}
