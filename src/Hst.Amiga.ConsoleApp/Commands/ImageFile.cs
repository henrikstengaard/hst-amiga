namespace Hst.Amiga.ConsoleApp.Commands;

using System;
using System.IO;
using System.Threading.Tasks;
using Vm.FileSystem;

/// <summary>
/// Adf, CD or disk image file opened as stream. Virtual hard disk files (.vhd) are opened with their disk content
/// and other files, e.g. .adf, .iso, .hdf and .img, are opened as raw images.
/// </summary>
public class ImageFile : IDisposable
{
    private readonly IDisposable disk;

    private ImageFile(string path, Stream stream, IDisposable disk, bool writable)
    {
        Path = path;
        Stream = stream;
        Writable = writable;
        this.disk = disk;
    }

    /// <summary>
    /// Full host path to image file.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Stream with image content.
    /// </summary>
    public Stream Stream { get; }

    /// <summary>
    /// Image is opened for writing.
    /// </summary>
    public bool Writable { get; }

    /// <summary>
    /// Open image file.
    /// </summary>
    /// <param name="path">Host path to image file.</param>
    /// <param name="writable">Open image for reading and writing, otherwise read only.</param>
    /// <exception cref="FileNotFoundException">Image file doesn't exist.</exception>
    public static ImageFile Open(string path, bool writable)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Image file '{path}' not found", path);
        }

        var access = writable ? FileAccess.ReadWrite : FileAccess.Read;
        if (string.Equals(System.IO.Path.GetExtension(fullPath), ".vhd", StringComparison.OrdinalIgnoreCase))
        {
            var vhdDisk = new DiscUtils.Vhd.Disk(fullPath, access);
            var content = vhdDisk.Content;
            content.Position = 0;
            return new ImageFile(fullPath, content, vhdDisk, writable);
        }

        var stream = new FileStream(fullPath, FileMode.Open, access, writable ? FileShare.Read : FileShare.ReadWrite);
        return new ImageFile(fullPath, stream, null, writable);
    }

    /// <summary>
    /// Is image file an adf, an ISO 9660 CD image or a disk image with rigid disk block in one of the first 16
    /// sectors.
    /// </summary>
    public Task<bool> IsImage() => ImageMounter.IsImage(Stream);

    public void Dispose()
    {
        Stream.Dispose();
        disk?.Dispose();
    }
}
