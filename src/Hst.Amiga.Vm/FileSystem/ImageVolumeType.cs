namespace Hst.Amiga.Vm.FileSystem;

/// <summary>
/// Type of volume mounted from image.
/// </summary>
public enum ImageVolumeType
{
    /// <summary>
    /// Adf mounted as floppy disk, e.g. DF0:.
    /// </summary>
    Floppy,

    /// <summary>
    /// Partition in disk image with rigid disk block, e.g. DH0:.
    /// </summary>
    Partition,

    /// <summary>
    /// ISO 9660 CD image mounted as CD drive, e.g. CD0:.
    /// </summary>
    Cd
}
