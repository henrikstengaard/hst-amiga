namespace Hst.Amiga.Vm.Libraries.Dos;

using System.Collections.Generic;

/// <summary>
/// Dos error codes and messages.
/// </summary>
public static class DosErrors
{
    public const int NoFreeStore = 103;
    public const int TaskTableFull = 105;
    public const int BadTemplate = 114;
    public const int BadNumber = 115;
    public const int RequiredArgMissing = 116;
    public const int KeyNeedsArg = 117;
    public const int TooManyArgs = 118;
    public const int UnmatchedQuotes = 119;
    public const int LineTooLong = 120;
    public const int FileNotObject = 121;
    public const int InvalidResidentLibrary = 122;
    public const int NoDefaultDir = 201;
    public const int ObjectInUse = 202;
    public const int ObjectExists = 203;
    public const int DirNotFound = 204;
    public const int ObjectNotFound = 205;
    public const int BadStreamName = 206;
    public const int ObjectTooLarge = 207;
    public const int ActionNotKnown = 209;
    public const int InvalidComponentName = 210;
    public const int InvalidLock = 211;
    public const int ObjectWrongType = 212;
    public const int DiskNotValidated = 213;
    public const int DiskWriteProtected = 214;
    public const int RenameAcrossDevices = 215;
    public const int DirectoryNotEmpty = 216;
    public const int TooManyLevels = 217;
    public const int DeviceNotMounted = 218;
    public const int SeekError = 219;
    public const int CommentTooBig = 220;
    public const int DiskFull = 221;
    public const int DeleteProtected = 222;
    public const int WriteProtected = 223;
    public const int ReadProtected = 224;
    public const int NotADosDisk = 225;
    public const int NoDisk = 226;
    public const int NoMoreEntries = 232;
    public const int IsSoftLink = 233;
    public const int ObjectLinked = 234;
    public const int BadHunk = 235;
    public const int NotImplemented = 236;
    public const int RecordNotLocked = 240;
    public const int LockCollision = 241;
    public const int LockTimeout = 242;
    public const int UnlockError = 243;
    public const int BufferOverflow = 303;
    public const int Break = 304;
    public const int NotExecutable = 305;

    public static readonly IReadOnlyDictionary<int, string> Messages = new Dictionary<int, string>
    {
        { NoFreeStore, "not enough memory available" },
        { TaskTableFull, "process table full" },
        { BadTemplate, "bad template" },
        { BadNumber, "bad number" },
        { RequiredArgMissing, "required argument missing" },
        { KeyNeedsArg, "value after keyword missing" },
        { TooManyArgs, "wrong number of arguments" },
        { UnmatchedQuotes, "unmatched quotes" },
        { LineTooLong, "argument line invalid or too long" },
        { FileNotObject, "file is not executable" },
        { InvalidResidentLibrary, "invalid resident library" },
        { NoDefaultDir, "no default directory" },
        { ObjectInUse, "object is in use" },
        { ObjectExists, "object already exists" },
        { DirNotFound, "directory not found" },
        { ObjectNotFound, "object not found" },
        { BadStreamName, "invalid window description" },
        { ObjectTooLarge, "object too large" },
        { ActionNotKnown, "packet request type unknown" },
        { InvalidComponentName, "object name invalid" },
        { InvalidLock, "invalid object lock" },
        { ObjectWrongType, "object is not of required type" },
        { DiskNotValidated, "disk not validated" },
        { DiskWriteProtected, "disk is write-protected" },
        { RenameAcrossDevices, "rename across devices attempted" },
        { DirectoryNotEmpty, "directory not empty" },
        { TooManyLevels, "too many levels" },
        { DeviceNotMounted, "device (or volume) is not mounted" },
        { SeekError, "seek failure" },
        { CommentTooBig, "comment is too long" },
        { DiskFull, "disk is full" },
        { DeleteProtected, "object is protected from deletion" },
        { WriteProtected, "file is write protected" },
        { ReadProtected, "file is read protected" },
        { NotADosDisk, "not a valid DOS disk" },
        { NoDisk, "no disk in drive" },
        { NoMoreEntries, "no more entries in directory" },
        { IsSoftLink, "object is soft link" },
        { ObjectLinked, "object is linked" },
        { BadHunk, "bad loadfile hunk" },
        { NotImplemented, "function not implemented" },
        { RecordNotLocked, "record not locked" },
        { LockCollision, "lock collision" },
        { LockTimeout, "lock timeout" },
        { UnlockError, "unlock error" },
        { BufferOverflow, "buffer overflow" },
        { Break, "***Break" },
        { NotExecutable, "file not executable" }
    };

    public static string GetMessage(int code) =>
        Messages.TryGetValue(code, out var message) ? message : $"Error code {code}";
}
