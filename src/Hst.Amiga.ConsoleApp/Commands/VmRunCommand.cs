namespace Hst.Amiga.ConsoleApp.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RigidDiskBlocks;
using Roms;
using Vm;
using Vm.FileSystem;
using Vm.Processor;

/// <summary>
/// Run Amiga CLI executable in virtual machine.
/// </summary>
public class VmRunCommand
{
    private readonly string executablePath;
    private readonly IEnumerable<string> arguments;
    private readonly VmOptions options;
    private readonly VmRunMode mode;
    private readonly IEnumerable<string> images;
    private readonly bool imageContext;
    private readonly bool writeImage;
    private readonly string kickstartPath;
    private readonly string romKeyPath;

    /// <summary>
    /// Create command running Amiga executable, script or interactive shell in virtual machine.
    /// </summary>
    /// <param name="executablePath">Host path to executable, host or Amiga path to script for execute mode and
    /// ignored for shell mode.</param>
    /// <param name="arguments">Arguments passed to executable or script.</param>
    /// <param name="options">Virtual machine options.</param>
    /// <param name="mode">Run executable, execute script or run interactive shell.</param>
    /// <param name="images">Host paths to adf and disk image files mounted as volumes.</param>
    /// <param name="imageContext">Use image context for executable in adf or disk image, e.g.
    /// "workbench.adf\c\list". True resolves paths in the Amiga file systems of the image by mounting it as
    /// volumes, false reads executable from image and resolves host paths.</param>
    /// <param name="writeImage">Open images for writing, otherwise volumes in images are write protected.</param>
    /// <param name="kickstartPath">Host path to Kickstart ROM mapped read-only in virtual machine, if any.</param>
    /// <param name="romKeyPath">Host path to Amiga Forever rom.key for encrypted Kickstart ROM. Default is
    /// rom.key in directory of Kickstart ROM, if it exists.</param>
    public VmRunCommand(string executablePath, IEnumerable<string> arguments, VmOptions options,
        VmRunMode mode = VmRunMode.Run, IEnumerable<string> images = null, bool imageContext = false,
        bool writeImage = false, string kickstartPath = null, string romKeyPath = null)
    {
        this.executablePath = executablePath;
        this.arguments = arguments ?? Enumerable.Empty<string>();
        this.options = options;
        this.mode = mode;
        this.images = images ?? Enumerable.Empty<string>();
        this.imageContext = imageContext;
        this.writeImage = writeImage;
        this.kickstartPath = kickstartPath;
        this.romKeyPath = romKeyPath;
    }

    /// <summary>
    /// Create virtual machine options from command line values.
    /// </summary>
    /// <exception cref="ArgumentException">Invalid volume or assign.</exception>
    public static VmOptions CreateOptions(string cpu, int? memory, int? stack, IEnumerable<string> volumes,
        IEnumerable<string> assigns, IEnumerable<string> stubLibraries, string currentDirectory, long? maxInstructions,
        bool trace, bool traceLibraries, string uaeMetadata = null, bool writeUaeMetadata = false)
    {
        var options = new VmOptions
        {
            CpuType = cpu == "68000" ? CpuType.M68000 : CpuType.M68020,
            CurrentDirectory = currentDirectory,
            MaxInstructions = maxInstructions ?? 0,
            TraceInstructions = trace,
            TraceLibraryCalls = traceLibraries,
            UaeMetadata = ParseUaeMetadata(uaeMetadata),
            WriteUaeMetadata = writeUaeMetadata
        };

        if (memory.HasValue)
        {
            options.MemorySize = (uint)memory.Value * 1024 * 1024;
        }

        if (stack.HasValue)
        {
            options.StackSize = (uint)stack.Value;
        }

        foreach (var volume in volumes ?? Enumerable.Empty<string>())
        {
            var (name, path) = ParseMapping(volume, "volume");
            options.Volumes[name] = path;
        }

        foreach (var stubLibrary in stubLibraries ?? Enumerable.Empty<string>())
        {
            options.StubLibraries.Add(stubLibrary);
        }

        foreach (var assign in assigns ?? Enumerable.Empty<string>())
        {
            var (name, path) = ParseMapping(assign, "assign");
            if (path == "+")
            {
                throw new ArgumentException($"Invalid assign '{assign}', expected name=+path");
            }

            // paths starting with + are added to assign given earlier or default assign
            options.Assigns[name] = path.StartsWith("+") && options.Assigns.TryGetValue(name, out var existing)
                ? $"{existing};{path.Substring(1)}"
                : path;
        }

        return options;
    }

    private static UaeMetadataMode ParseUaeMetadata(string value) =>
        (value ?? "auto").ToLowerInvariant() switch
        {
            "auto" => UaeMetadataMode.Auto,
            "uaefsdb" => UaeMetadataMode.UaeFsDb,
            "uaemetafile" => UaeMetadataMode.UaeMetafile,
            "none" => UaeMetadataMode.None,
            _ => throw new ArgumentException(
                $"Invalid UAE metadata '{value}', expected auto, uaefsdb, uaemetafile or none")
        };

    private static (string Name, string Path) ParseMapping(string value, string type)
    {
        var separator = value.IndexOf('=');
        if (separator <= 0 || separator == value.Length - 1)
        {
            throw new ArgumentException($"Invalid {type} '{value}', expected name=path");
        }

        return (value.Substring(0, separator).TrimEnd(':'), value.Substring(separator + 1));
    }

    /// <summary>
    /// Join arguments into Amiga argument string, quoting arguments with spaces or quotes.
    /// </summary>
    public static string JoinArguments(IEnumerable<string> arguments) =>
        string.Join(" ", arguments.Select(QuoteArgument));

    private static string QuoteArgument(string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
        {
            return argument;
        }

        var builder = new StringBuilder("\"");
        foreach (var c in argument)
        {
            switch (c)
            {
                case '"':
                    builder.Append("*\"");
                    break;
                case '*':
                    builder.Append("**");
                    break;
                case '\n':
                    builder.Append("*N");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.Append('"').ToString();
    }

    public async Task<VmResult> Execute(Action<AmigaVm> started, CancellationToken token)
    {
        var imageFiles = new List<ImageFile>();
        try
        {
            if (!string.IsNullOrEmpty(kickstartPath))
            {
                options.KickstartRom = await ReadKickstartRom(kickstartPath, romKeyPath, token);
            }

            foreach (var image in images)
            {
                await AddImage(imageFiles, image);
            }

            var argumentString = JoinArguments(arguments);
            Func<AmigaVm, Task<VmResult>> run = mode switch
            {
                VmRunMode.Execute => vm => vm.Execute(executablePath, argumentString, token),
                VmRunMode.Shell => vm => vm.Shell(token),
                _ => await PrepareRun(imageFiles, argumentString, token)
            };

            var vm = new AmigaVm(options);
            started?.Invoke(vm);
            return await run(vm);
        }
        finally
        {
            foreach (var imageFile in imageFiles)
            {
                imageFile.Dispose();
            }
        }
    }

    /// <summary>
    /// Read raw or Amiga Forever encrypted Kickstart ROM.
    /// </summary>
    /// <exception cref="VmException">Kickstart ROM is invalid or unsupported.</exception>
    private static async Task<KickstartRom> ReadKickstartRom(string path, string keyPath, CancellationToken token)
    {
        var romBytes = await File.ReadAllBytesAsync(path, token);
        var keyBytes = await KickstartCommand.ReadKey(path, keyPath, token);
        try
        {
            return KickstartRomReader.Read(romBytes, keyBytes);
        }
        catch (InvalidDataException e)
        {
            throw new VmException($"Invalid Kickstart ROM '{path}': {e.Message}");
        }
    }

    /// <summary>
    /// Prepare running executable from host file, from adf or disk image file or from Amiga path in mounted
    /// images.
    /// </summary>
    private async Task<Func<AmigaVm, Task<VmResult>>> PrepareRun(List<ImageFile> imageFiles, string argumentString,
        CancellationToken token)
    {
        if (File.Exists(executablePath))
        {
            return vm => vm.Run(executablePath, argumentString, token);
        }

        if (ImagePath.TryParse(executablePath, out var imagePath))
        {
            if (imageContext)
            {
                var imageFile = await AddImage(imageFiles, imagePath.ImageFile);
                var amigaPath = await GetDevicePath(imageFile, imagePath);
                return vm => vm.RunFromVolume(amigaPath, argumentString, token);
            }

            var existing = FindImage(imageFiles, imagePath.ImageFile);
            using var opened = existing == null ? await OpenImage(imagePath.ImageFile, false) : null;
            var executable = await ReadExecutable((existing ?? opened).Stream, imagePath);
            var programName = imagePath.Components[imagePath.Components.Count - 1];
            return vm => vm.Run(executable, programName, argumentString, null, token);
        }

        if (options.Images.Count > 0 && executablePath.IndexOf(':') > 0)
        {
            return vm => vm.RunFromVolume(executablePath, argumentString, token);
        }

        throw new FileNotFoundException($"Executable '{executablePath}' not found", executablePath);
    }

    private static ImageFile FindImage(IEnumerable<ImageFile> imageFiles, string path)
    {
        var fullPath = Path.GetFullPath(path);
        return imageFiles.FirstOrDefault(x => string.Equals(x.Path, fullPath, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Open image file and check it's an adf or a disk image with rigid disk block.
    /// </summary>
    /// <exception cref="IOException">File is not an adf or disk image.</exception>
    private static async Task<ImageFile> OpenImage(string path, bool writable)
    {
        var imageFile = ImageFile.Open(path, writable);
        try
        {
            if (!await imageFile.IsImage())
            {
                throw new IOException(
                    $"'{path}' is not an adf or disk image with rigid disk block in the first {ImageMounter.RigidDiskBlockLocationLimit} sectors");
            }

            return imageFile;
        }
        catch
        {
            imageFile.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Open image file and mount it in virtual machine, if it's not already mounted.
    /// </summary>
    private async Task<ImageFile> AddImage(List<ImageFile> imageFiles, string path)
    {
        var existing = FindImage(imageFiles, path);
        if (existing != null)
        {
            return existing;
        }

        var imageFile = await OpenImage(path, writeImage);
        imageFiles.Add(imageFile);
        options.Images.Add(new VmImage(imageFile.Stream, imageFile.Path, writeImage));
        return imageFile;
    }

    /// <summary>
    /// Get Amiga path with device name of file in mounted image, e.g. "DF0:c/list" or "DH0:c/list".
    /// </summary>
    private async Task<string> GetDevicePath(ImageFile imageFile, ImagePath imagePath)
    {
        if (ImageMounter.IsAdf(imageFile.Stream))
        {
            // adf images are mounted as DF0:, DF1: etc. in order
            var floppyNumber = options.Images.TakeWhile(x => x.Stream != imageFile.Stream)
                .Count(x => ImageMounter.IsAdf(x.Stream));
            return $"DF{floppyNumber}:{imagePath.GetAmigaPath(0)}";
        }

        var (_, partitionBlock, amigaPath) = await FindPartition(imageFile.Stream, imagePath);
        return $"{partitionBlock.DriveName}:{amigaPath}";
    }

    /// <summary>
    /// Find partition in rigid disk block and Amiga path of file in partition. Path starting with rdb and
    /// partition number starting from 1 or device name, e.g. "rdb\1\c\list" or "rdb\dh0\c\list", is in that
    /// partition. Otherwise path, e.g. "c\list", is in boot partition, which is the bootable partition with
    /// highest boot priority.
    /// </summary>
    private static async Task<(int Number, PartitionBlock PartitionBlock, string AmigaPath)> FindPartition(
        Stream stream, ImagePath imagePath)
    {
        stream.Position = 0;
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        if (rigidDiskBlock == null)
        {
            throw new IOException($"Rigid disk block not found in disk image '{imagePath.ImageFile}'");
        }

        var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
        if (imagePath.IsRigidDiskBlockPath && imagePath.Components.Count >= 3)
        {
            var partition = imagePath.Components[1];
            if (int.TryParse(partition, out var number))
            {
                if (number < 1 || number > partitionBlocks.Count)
                {
                    throw new ArgumentException(
                        $"Partition number {number} not found in disk image '{imagePath.ImageFile}', which has {partitionBlocks.Count} partitions");
                }

                return (number, partitionBlocks[number - 1], imagePath.GetAmigaPath(2));
            }

            var index = partitionBlocks.FindIndex(x =>
                string.Equals(x.DriveName, partition.TrimEnd(':'), StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                return (index + 1, partitionBlocks[index], imagePath.GetAmigaPath(2));
            }
        }

        var (bootNumber, bootPartitionBlock) = ImageMounter.FindBootPartition(partitionBlocks);
        return (bootNumber, bootPartitionBlock, imagePath.GetAmigaPath(0));
    }

    /// <summary>
    /// Read executable from adf or partition in disk image.
    /// </summary>
    private async Task<byte[]> ReadExecutable(Stream stream, ImagePath imagePath)
    {
        string amigaPath;
        IList<ImageVolume> mounted;
        if (ImageMounter.IsAdf(stream))
        {
            amigaPath = imagePath.GetAmigaPath(0);
            mounted = await ImageMounter.Mount(stream, false, "DF0", options.Log);
        }
        else
        {
            (var number, _, amigaPath) = await FindPartition(stream, imagePath);
            mounted = await ImageMounter.Mount(stream, false, "DF0", options.Log, (x, _) => x == number);
        }

        try
        {
            if (mounted.Count == 0)
            {
                throw new IOException($"Unable to mount volume in image '{imagePath.ImageFile}'");
            }

            var volumes = new VolumeManager();
            volumes.AddVolume("IMAGE", mounted[0].FileSystem);
            var path = volumes.Resolve($"IMAGE:{amigaPath}", null);
            if (!volumes.IsFile(path))
            {
                throw new FileNotFoundException(
                    $"Executable '{amigaPath}' not found in image '{imagePath.ImageFile}'", executablePath);
            }

            return volumes.ReadAllBytes(path);
        }
        finally
        {
            foreach (var volume in mounted)
            {
                volume.Dispose();
            }
        }
    }
}
