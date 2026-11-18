using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.Roms;
using Hst.Core;

namespace Hst.Amiga.ConsoleApp.Commands;

public class KickstartCommand : CommandBase
{
    private readonly string romPath;
    private readonly string outputDirectory;
    private readonly string keyFilePath;
    private readonly bool extract;
    private readonly bool hunkOutput;
    private readonly string remusSplitDataPath;

    public KickstartCommand(string romPath, string outputDirectory, string keyFilePath, bool extract,
        bool hunkOutput = false, string remusSplitDataPath = null)
    {
        this.romPath = romPath;
        this.outputDirectory = outputDirectory;
        this.keyFilePath = keyFilePath;
        this.extract = extract;
        this.hunkOutput = hunkOutput;
        this.remusSplitDataPath = remusSplitDataPath;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        try
        {
            if (remusSplitDataPath != null && extract && !hunkOutput)
            {
                return new Result(new Error("--remus-split-data requires kickstart extract --hunk."));
            }

            OnInformationMessage($"Reading Kickstart ROM '{romPath}'");
            var romBytes = await File.ReadAllBytesAsync(romPath, token);
            var keyBytes = await ReadKey(romPath, keyFilePath, token);

            var directory = outputDirectory ?? Path.Combine(
                Path.GetDirectoryName(romPath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(romPath) + ".modules");

            if (remusSplitDataPath != null)
            {
                OnInformationMessage($"Reading Remus split data '{remusSplitDataPath}'");
                var splitData = await File.ReadAllBytesAsync(remusSplitDataPath, token);
                if (!extract)
                {
                    foreach (var (offset, size, name) in RemusRomSplitter.List(romBytes, splitData, keyBytes))
                    {
                        OnInformationMessage($"0x{offset:X6} size={size} {name}");
                    }

                    return new Result();
                }

                var modules = RemusRomSplitter.Extract(romBytes, splitData, keyBytes);
                Directory.CreateDirectory(directory);
                foreach (var (offset, name, bytes) in modules)
                {
                    var path = await WriteModule(directory, offset, name, bytes, token);
                    OnInformationMessage($"Extracted catalog module '{name}' to '{path}'");
                }

                return new Result();
            }

            var rom = KickstartRomReader.Read(romBytes, keyBytes);
            OnInformationMessage(
                $"Found {rom.Modules.Count} modules in {rom.Bytes.Length} byte{(rom.IsEncrypted ? " encrypted" : string.Empty)} ROM (base 0x{rom.BaseAddress:X6})");

            if (!extract)
            {
                foreach (var module in rom.Modules)
                {
                    OnInformationMessage(FormatModule(module));
                }

                return new Result();
            }

            Directory.CreateDirectory(directory);

            foreach (var module in rom.Modules)
            {
                var moduleBytes = hunkOutput
                    ? rom.GetModuleHunkBytes(module)
                    : rom.GetModuleBytes(module);
                var path = await WriteModule(directory, module.Offset, module.Name, moduleBytes, token);
                OnInformationMessage($"Extracted {FormatModule(module)} to '{path}'");
            }

            return new Result();
        }
        catch (InvalidDataException e)
        {
            return new Result(new Error(e.Message));
        }
    }

    internal static async Task<byte[]> ReadKey(string romPath, string keyFilePath, CancellationToken token)
    {
        var path = keyFilePath ?? Path.Combine(Path.GetDirectoryName(romPath) ?? string.Empty, "rom.key");
        return keyFilePath != null || File.Exists(path) ? await File.ReadAllBytesAsync(path, token) : null;
    }

    private async Task<string> WriteModule(string directory, int offset, string name, byte[] bytes,
        CancellationToken token)
    {
        name = string.IsNullOrWhiteSpace(name) ? "module" : name;
        var safeName = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var extension = hunkOutput ? ".hunk" : ".bin";
        var path = Path.Combine(directory, $"{offset:X6}-{safeName}{extension}");
        await File.WriteAllBytesAsync(path, bytes, token);
        return path;
    }

    private static string FormatModule(KickstartRomModule module)
    {
        var description = string.IsNullOrWhiteSpace(module.Name) ? "(unnamed)" : module.Name;
        if (!string.IsNullOrWhiteSpace(module.IdString))
        {
            description += $" - {module.IdString}";
        }

        return $"0x{module.Offset:X6} size={module.Size} v{module.Version}.{module.Revision} {description}";
    }
}
