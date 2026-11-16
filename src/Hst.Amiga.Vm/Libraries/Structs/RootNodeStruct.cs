namespace Hst.Amiga.Vm.Libraries.Structs;

/// <summary>
/// Offsets of struct RootNode.
/// </summary>
public static class RootNodeStruct
{
    public const int TaskArray = 0;
    public const int ConsoleSegment = 4;
    public const int Time = 8;
    public const int RestartSeg = 20;
    public const int Info = 24;
    public const int FileHandlerSegment = 28;
    public const int CliList = 32;
    public const int BootProc = 44;
    public const int ShellSegment = 48;
    public const int Flags = 52;
    public const int Size = 56;
}
