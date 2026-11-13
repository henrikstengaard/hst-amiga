namespace Hst.Amiga.FileSystems.Salvage
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Target salvaged directories and files are re-created in. Paths are lists of Amiga names from the root of
    /// the target, an empty list is the root.
    /// </summary>
    public interface ISalvageTarget
    {
        /// <summary>
        /// Create directory in parent directory.
        /// </summary>
        /// <param name="parentPath">Amiga names of parent directories.</param>
        /// <param name="entry">Directory to create.</param>
        Task CreateDirectory(IReadOnlyList<string> parentPath, SalvageEntry entry);

        /// <summary>
        /// Set protection bits, comment and date of directory. Called after all directories and files in the
        /// directory are created, so creating them doesn't change the date of the directory.
        /// </summary>
        /// <param name="parentPath">Amiga names of parent directories.</param>
        /// <param name="entry">Directory to set metadata for.</param>
        Task SetDirectoryMetadata(IReadOnlyList<string> parentPath, SalvageEntry entry);

        /// <summary>
        /// Create file in parent directory with data, protection bits, comment and date.
        /// </summary>
        /// <param name="parentPath">Amiga names of parent directories.</param>
        /// <param name="entry">File to create.</param>
        /// <param name="data">Stream with file data.</param>
        Task WriteFile(IReadOnlyList<string> parentPath, SalvageEntry entry, Stream data);

        /// <summary>
        /// Flush changes.
        /// </summary>
        Task Flush();
    }
}
