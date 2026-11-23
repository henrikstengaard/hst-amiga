namespace Hst.Amiga.Vm.FileSystem;

/// <summary>
/// Type of assign like the dos list entry types DLT_DIRECTORY, DLT_LATE and DLT_NONBINDING.
/// </summary>
public enum AssignType
{
    /// <summary>
    /// Assign to one or more directories like AssignLock() and AssignAdd().
    /// </summary>
    Lock,

    /// <summary>
    /// Late binding assign to path like AssignLate().
    /// </summary>
    Late,

    /// <summary>
    /// Non-binding assign to path like AssignPath(), which is resolved each time it's used.
    /// </summary>
    NonBinding
}
