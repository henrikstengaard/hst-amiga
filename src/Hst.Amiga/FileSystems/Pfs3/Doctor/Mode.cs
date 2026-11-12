namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Mode (enum mode in doctor.h).
    /// Mode for cached block can be:
    /// 'Check' -- loaded in cache voor check etc --> indirect (by value) edit
    /// 'Build' -- loaded in buildblock cache. --> direct edit (by reference)
    /// </summary>
    public enum Mode
    {
        Check = 0,
        Info,
        Build,
        Repair,
        Search,
        Unformat,
        Done
    }
}
