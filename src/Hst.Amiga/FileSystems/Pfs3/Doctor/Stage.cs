namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Standard scan stage (ss.stage in standardscan.c).
    /// Order is significant, stages are compared with less than.
    /// </summary>
    public enum Stage
    {
        Syntax,
        ResBitmap,
        MainBitmap,
        AnodeBitmap,
        Finished
    }
}
