namespace Hst.Amiga.Vm.Libraries.Structs;

/// <summary>
/// Offsets of struct FileInfoBlock.
/// </summary>
public static class FileInfoBlockStruct
{
    public const int DiskKey = 0;
    public const int DirEntryType = 4;
    public const int FileName = 8;
    public const int Protection = 116;
    public const int EntryType = 120;
    public const int FileSize = 124;
    public const int NumBlocks = 128;
    public const int Date = 132;
    public const int Comment = 144;
    public const int OwnerUid = 224;
    public const int OwnerGid = 226;
    public const int Reserved = 228;
    public const int Size = 260;
}
