namespace Hst.Amiga.Vm.Libraries.Exec;

/// <summary>
/// Names of exec.library V40 functions in jump table order starting at offset -30.
/// </summary>
public static class ExecFunctionNames
{
    public static readonly string[] Names =
    {
        "Supervisor", "ExitIntr", "Schedule", "Reschedule", "Switch", "Dispatch", "Exception", "InitCode",
        "InitStruct", "MakeLibrary", "MakeFunctions", "FindResident", "InitResident", "Alert", "Debug", "Disable",
        "Enable", "Forbid", "Permit", "SetSR", "SuperState", "UserState", "SetIntVector", "AddIntServer",
        "RemIntServer", "Cause", "Allocate", "Deallocate", "AllocMem", "AllocAbs", "FreeMem", "AvailMem",
        "AllocEntry", "FreeEntry", "Insert", "AddHead", "AddTail", "Remove", "RemHead", "RemTail", "Enqueue",
        "FindName", "AddTask", "RemTask", "FindTask", "SetTaskPri", "SetSignal", "SetExcept", "Wait", "Signal",
        "AllocSignal", "FreeSignal", "AllocTrap", "FreeTrap", "AddPort", "RemPort", "PutMsg", "GetMsg", "ReplyMsg",
        "WaitPort", "FindPort", "AddLibrary", "RemLibrary", "OldOpenLibrary", "CloseLibrary", "SetFunction",
        "SumLibrary", "AddDevice", "RemDevice", "OpenDevice", "CloseDevice", "DoIO", "SendIO", "CheckIO", "WaitIO",
        "AbortIO", "AddResource", "RemResource", "OpenResource", "RawIOInit", "RawMayGetChar", "RawPutChar",
        "RawDoFmt", "GetCC", "TypeOfMem", "Procure", "Vacate", "OpenLibrary", "InitSemaphore", "ObtainSemaphore",
        "ReleaseSemaphore", "AttemptSemaphore", "ObtainSemaphoreList", "ReleaseSemaphoreList", "FindSemaphore",
        "AddSemaphore", "RemSemaphore", "SumKickData", "AddMemList", "CopyMem", "CopyMemQuick", "CacheClearU",
        "CacheClearE", "CacheControl", "CreateIORequest", "DeleteIORequest", "CreateMsgPort", "DeleteMsgPort",
        "ObtainSemaphoreShared", "AllocVec", "FreeVec", "CreatePool", "DeletePool", "AllocPooled", "FreePooled",
        "AttemptSemaphoreShared", "ColdReboot", "StackSwap", "ChildFree", "ChildOrphan", "ChildStatus", "ChildWait",
        "CachePreDMA", "CachePostDMA", "AddMemHandler", "RemMemHandler", "ObtainQuickVector"
    };
}
