namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System.Collections.Generic;

    /// <summary>
    /// Index of entry names to entry block sectors per directory, so entries can be found without walking
    /// hash chains. Names are upper case using Amiga upper case rules of volume. Indexes are built, when
    /// directories are listed or entries are looked up, and updated when entries are created, renamed or removed.
    /// Hash tables and hash chains in directory blocks are always used as the source of truth, so indexes can be
    /// invalidated at any time.
    /// </summary>
    internal class DirectoryIndex
    {
        /// <summary>
        /// Max number of directories indexed. All indexes are cleared, when exceeded
        /// </summary>
        public const int MaxDirectories = 64;

        private readonly Dictionary<uint, Dictionary<string, uint>> indexes =
            new Dictionary<uint, Dictionary<string, uint>>();

        /// <summary>
        /// Number of directories indexed
        /// </summary>
        public int Count => indexes.Count;

        public bool TryGet(uint directorySector, out Dictionary<string, uint> index) =>
            indexes.TryGetValue(directorySector, out index);

        public void Set(uint directorySector, Dictionary<string, uint> index)
        {
            if (!indexes.ContainsKey(directorySector) && indexes.Count >= MaxDirectories)
            {
                indexes.Clear();
            }

            indexes[directorySector] = index;
        }

        /// <summary>
        /// Add entry to index of directory, if directory is indexed
        /// </summary>
        public void Add(uint directorySector, string upperName, uint entrySector)
        {
            if (indexes.TryGetValue(directorySector, out var index))
            {
                index[upperName] = entrySector;
            }
        }

        /// <summary>
        /// Remove entry from index of directory, if directory is indexed
        /// </summary>
        public void Remove(uint directorySector, string upperName)
        {
            if (indexes.TryGetValue(directorySector, out var index))
            {
                index.Remove(upperName);
            }
        }

        /// <summary>
        /// Remove index of directory
        /// </summary>
        public void Invalidate(uint directorySector) => indexes.Remove(directorySector);

        public void Clear() => indexes.Clear();
    }
}
