namespace Hst.Amiga.Vm.Libraries;

/// <summary>
/// Exec node types.
/// </summary>
public static class NodeTypes
{
    public const byte Unknown = 0;
    public const byte Task = 1;
    public const byte Interrupt = 2;
    public const byte Device = 3;
    public const byte MsgPort = 4;
    public const byte Message = 5;
    public const byte FreeMsg = 6;
    public const byte ReplyMsg = 7;
    public const byte Resource = 8;
    public const byte Library = 9;
    public const byte Memory = 10;
    public const byte SoftInt = 11;
    public const byte Font = 12;
    public const byte Process = 13;
    public const byte Semaphore = 14;
    public const byte SignalSem = 15;
}
