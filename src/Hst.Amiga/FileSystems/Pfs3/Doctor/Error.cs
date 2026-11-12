namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Errors (error_t in doctor.h). Each member maps to the original e_ prefixed name,
    /// e.g. e_none = None, e_block_outside_partition = BlockOutsidePartition.
    /// </summary>
    public enum Error
    {
        None = 0, /* no error */
        Aborted,
        Dirty,
        Remove,
        Repartition,
        Empty,
        NotFound,
        OutOfMemory,
        FatalError,
        RblNotFound,
        MaxPassExceeded,
        ResBitmapFail,
        MainBitmapFail,
        AnodeBitmapFail,
        BlockOutsidePartition,
        BlockOutsideReserved,
        OptionsError,
        DirentryError,
        InvalidSoftlink,
        AnodeError,
        ReservedAreaError,
        OutsideBitmapError,
        DoubleAllocation,
        NumberError,
        SyntaxError,
        ReadError,
        WriteError,
        AllocFail
    }
}
