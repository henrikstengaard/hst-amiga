namespace Hst.Amiga.FileSystems
{
    using System;

    [Flags]
    public enum ProtectionBits : int
    {
        None = 0,
        /// <summary>
        /// The file is a script.
        /// </summary>
        Script = 1,
        /// <summary>
        /// The file is a pure command and can be made resident.
        /// </summary>
        Pure = 2,
        /// <summary>
        /// The file has been archived.
        /// </summary>
        Archive = 4,
        /// <summary>
        /// The command file should be held resident in memory after it has been used (requires that the 'p' bit is set, too)
        /// </summary>
        HeldResident = 8,
        /// <summary>
        /// The file can be read.
        /// </summary>
        Read = 16,
        /// <summary>
        /// The file can be written to (altered).
        /// </summary>
        Write = 32,
        /// <summary>
        /// The file is executable (a program).
        /// </summary>
        Executable = 64,
        /// <summary>
        /// The file can be deleted.
        /// </summary>
        Delete = 128
    }
}