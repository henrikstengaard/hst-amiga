namespace Hst.Amiga.Vm.Libraries.Structs;

/// <summary>
/// Offsets of struct Process.
/// </summary>
public static class ProcessStruct
{
    public const int Task = 0;
    public const int MsgPort = 92;
    public const int Pad = 126;
    public const int SegList = 128;
    public const int StackSize = 132;
    public const int GlobVec = 136;
    public const int TaskNum = 140;
    public const int StackBase = 144;
    public const int Result2 = 148;
    public const int CurrentDir = 152;
    public const int Cis = 156;
    public const int Cos = 160;
    public const int ConsoleTask = 164;
    public const int FileSystemTask = 168;
    public const int Cli = 172;
    public const int ReturnAddr = 176;
    public const int PktWait = 180;
    public const int WindowPtr = 184;
    public const int HomeDir = 188;
    public const int Flags = 192;
    public const int ExitCode = 196;
    public const int ExitData = 200;
    public const int Arguments = 204;
    public const int LocalVars = 208;
    public const int ShellPrivate = 220;
    public const int Ces = 224;
    public const int Size = 228;
}
