namespace Hst.Amiga.FileSystems.Exceptions
{
    /// <summary>
    /// Thrown when an existing entry has a name longer than a requested maximum file name length.
    /// </summary>
    public class FileNameTooLongException : FileSystemException
    {
        /// <summary>
        /// Path of entry with the too long name.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Length of the entry name in characters.
        /// </summary>
        public int NameLength { get; }

        public FileNameTooLongException(string message, string path, int nameLength) : base(message)
        {
            Path = path;
            NameLength = nameLength;
        }
    }
}
