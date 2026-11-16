namespace Hst.Amiga.Vm.Libraries.Exec;

/// <summary>
/// Exec memory requirement flags.
/// </summary>
public static class MemoryFlags
{
    public const uint Any = 0;
    public const uint Public = 1 << 0;
    public const uint Chip = 1 << 1;
    public const uint Fast = 1 << 2;
    public const uint Local = 1 << 8;
    public const uint Kick = 1 << 10;
    public const uint Clear = 1 << 16;
    public const uint Largest = 1 << 17;
    public const uint Reverse = 1 << 18;
    public const uint Total = 1 << 19;
}
