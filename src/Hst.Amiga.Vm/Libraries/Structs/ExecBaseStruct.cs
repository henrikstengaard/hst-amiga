namespace Hst.Amiga.Vm.Libraries.Structs;

/// <summary>
/// Offsets of struct ExecBase.
/// </summary>
public static class ExecBaseStruct
{
    public const int LibNode = 0;
    public const int SoftVer = 34;
    public const int LowMemChkSum = 36;
    public const int ChkBase = 38;
    public const int ColdCapture = 42;
    public const int CoolCapture = 46;
    public const int WarmCapture = 50;
    public const int SysStkUpper = 54;
    public const int SysStkLower = 58;
    public const int MaxLocMem = 62;
    public const int DebugEntry = 66;
    public const int DebugData = 70;
    public const int AlertData = 74;
    public const int MaxExtMem = 78;
    public const int ChkSum = 82;
    public const int IntVects = 84;
    public const int ThisTask = 276;
    public const int IdleCount = 280;
    public const int DispCount = 284;
    public const int Quantum = 288;
    public const int Elapsed = 290;
    public const int SysFlags = 292;
    public const int IdNestCnt = 294;
    public const int TdNestCnt = 295;
    public const int AttnFlags = 296;
    public const int AttnResched = 298;
    public const int ResModules = 300;
    public const int TaskTrapCode = 304;
    public const int TaskExceptCode = 308;
    public const int TaskExitCode = 312;
    public const int TaskSigAlloc = 316;
    public const int TaskTrapAlloc = 320;
    public const int MemList = 322;
    public const int ResourceList = 336;
    public const int DeviceList = 350;
    public const int IntrList = 364;
    public const int LibList = 378;
    public const int PortList = 392;
    public const int TaskReady = 406;
    public const int TaskWait = 420;
    public const int SoftInts = 434;
    public const int LastAlert = 514;
    public const int VBlankFrequency = 530;
    public const int PowerSupplyFrequency = 531;
    public const int SemaphoreList = 532;
    public const int KickMemPtr = 546;
    public const int KickTagPtr = 550;
    public const int KickCheckSum = 554;
    public const int Pad0 = 558;
    public const int LaunchPoint = 560;
    public const int RamLibPrivate = 564;
    public const int EClockFrequency = 568;
    public const int CacheControl = 572;
    public const int TaskId = 576;
    public const int MmuLock = 600;
    public const int MemHandlers = 616;
    public const int MemHandler = 628;
    public const int Size = 632;
}
