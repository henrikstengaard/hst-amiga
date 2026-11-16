namespace Hst.Amiga.Vm.FileSystem;

using System;
using System.IO;

/// <summary>
/// Volume file system error with dos error code.
/// </summary>
public class VolumeException : IOException
{
    public VolumeException(int dosError, string message, Exception innerException = null)
        : base(message, innerException)
    {
        DosError = dosError;
    }

    /// <summary>
    /// Dos error code, e.g. 205 for object not found.
    /// </summary>
    public int DosError { get; }
}
