namespace Hst.Amiga.Vm;

using System;
using System.IO;

/// <summary>
/// Adf, ISO 9660 CD or disk image mounted in virtual machine, so programs run in the context of the file systems
/// in the image. Adf images are mounted as DF0:, DF1: etc., CD images as CD0:, CD1: etc. and partitions in disk
/// images with rigid disk block are mounted with their device names, e.g. DH0:.
/// </summary>
public class VmImage
{
    /// <summary>
    /// Create image mounted in virtual machine.
    /// </summary>
    /// <param name="stream">Stream with adf, CD or disk image, which must be seekable. Stream isn't disposed by
    /// virtual machine.</param>
    /// <param name="name">Name of image used in messages, e.g. path to image file.</param>
    /// <param name="writable">Image is writable. Stream must be writable. Default is false, which write
    /// protects volumes in image. CD images are always write protected.</param>
    public VmImage(Stream stream, string name = null, bool writable = false)
    {
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        Name = name ?? "image";
        Writable = writable;
    }

    /// <summary>
    /// Stream with adf, CD or disk image.
    /// </summary>
    public Stream Stream { get; }

    /// <summary>
    /// Name of image used in messages.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Image is writable, otherwise volumes in image are write protected.
    /// </summary>
    public bool Writable { get; }
}
