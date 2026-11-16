namespace Hst.Amiga.Vm.Libraries.Structs;

/// <summary>
/// Offsets of struct IORequest.
/// </summary>
public static class IoRequestStruct
{
    public const int Message = 0;
    public const int Device = 20;
    public const int Unit = 24;
    public const int Command = 28;
    public const int Flags = 30;
    public const int Error = 31;
    public const int Size = 32;
}
