namespace Hst.Amiga.Vm.Libraries.Dos;

/// <summary>
/// Names of dos.library V40 functions in jump table order starting at offset -30.
/// </summary>
public static class DosFunctionNames
{
    public static readonly string[] Names =
    {
        "Open", "Close", "Read", "Write", "Input", "Output", "Seek", "DeleteFile", "Rename", "Lock", "UnLock",
        "DupLock", "Examine", "ExNext", "Info", "CreateDir", "CurrentDir", "IoErr", "CreateProc", "Exit", "LoadSeg",
        "UnLoadSeg", "DosPrivate1", "DosPrivate2", "DeviceProc", "SetComment", "SetProtection", "DateStamp", "Delay",
        "WaitForChar", "ParentDir", "IsInteractive", "Execute", "AllocDosObject", "FreeDosObject", "DoPkt",
        "SendPkt", "WaitPkt", "ReplyPkt", "AbortPkt", "LockRecord", "LockRecords", "UnLockRecord", "UnLockRecords",
        "SelectInput", "SelectOutput", "FGetC", "FPutC", "UnGetC", "FRead", "FWrite", "FGets", "FPuts", "VFWritef",
        "VFPrintf", "Flush", "SetVBuf", "DupLockFromFH", "OpenFromLock", "ParentOfFH", "ExamineFH", "SetFileDate",
        "NameFromLock", "NameFromFH", "SplitName", "SameLock", "SetMode", "ExAll", "ReadLink", "MakeLink",
        "ChangeMode", "SetFileSize", "SetIoErr", "Fault", "PrintFault", "ErrorReport", "DosPrivate3", "Cli",
        "CreateNewProc", "RunCommand", "GetConsoleTask", "SetConsoleTask", "GetFileSysTask", "SetFileSysTask",
        "GetArgStr", "SetArgStr", "FindCliProc", "MaxCli", "SetCurrentDirName", "GetCurrentDirName",
        "SetProgramName", "GetProgramName", "SetPrompt", "GetPrompt", "SetProgramDir", "GetProgramDir",
        "SystemTagList", "AssignLock", "AssignLate", "AssignPath", "AssignAdd", "RemAssignList", "GetDeviceProc",
        "FreeDeviceProc", "LockDosList", "UnLockDosList", "AttemptLockDosList", "RemDosEntry", "AddDosEntry",
        "FindDosEntry", "NextDosEntry", "MakeDosEntry", "FreeDosEntry", "IsFileSystem", "Format", "Relabel",
        "Inhibit", "AddBuffers", "CompareDates", "DateToStr", "StrToDate", "InternalLoadSeg", "InternalUnLoadSeg",
        "NewLoadSeg", "AddSegment", "FindSegment", "RemSegment", "CheckSignal", "ReadArgs", "FindArg", "ReadItem",
        "StrToLong", "MatchFirst", "MatchNext", "MatchEnd", "ParsePattern", "MatchPattern", "DosPrivate4",
        "FreeArgs", "DosPrivate5", "FilePart", "PathPart", "AddPart", "StartNotify", "EndNotify", "SetVar", "GetVar",
        "DeleteVar", "FindVar", "DosPrivate6", "CliInitNewcli", "CliInitRun", "WriteChars", "PutStr", "VPrintf",
        "DosPrivate7", "ParsePatternNoCase", "MatchPatternNoCase", "DosPrivate8", "SameDevice", "ExAllEnd",
        "SetOwner"
    };
}
