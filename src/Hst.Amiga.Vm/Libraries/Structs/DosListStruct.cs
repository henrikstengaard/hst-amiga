namespace Hst.Amiga.Vm.Libraries.Structs;

/// <summary>
/// Offsets of struct DosList.
/// </summary>
public static class DosListStruct
{
    public const int Next = 0;
    public const int Type = 4;
    public const int Task = 8;
    public const int Lock = 12;

    // dol_misc.dol_volume
    public const int VolumeDate = 16;
    public const int LockList = 28;
    public const int DiskType = 32;

    // dol_misc.dol_assign
    public const int AssignName = 16;
    public const int List = 20;

    public const int Name = 40;
    public const int Size = 44;
}
