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

    public KickstartCommand(string romPath, string outputDirectory, string keyFilePath, bool extract,
        bool hunkOutput = false)
    {
        this.romPath = romPath;
        this.outputDirectory = outputDirectory;
        this.keyFilePath = keyFilePath;
        this.extract = extract;
        this.hunkOutput = hunkOutput;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        try
        {
            OnInformationMessage($"Reading Kickstart ROM '{romPath}'");
            var romBytes = await File.ReadAllBytesAsync(romPath, token);
            var effectiveKeyPath = keyFilePath ?? Path.Combine(Path.GetDirectoryName(romPath) ?? string.Empty, "rom.key");
            byte[] keyBytes = null;
            if (File.Exists(effectiveKeyPath))
            {
                keyBytes = await File.ReadAllBytesAsync(effectiveKeyPath, token);
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

            var directory = outputDirectory ?? Path.Combine(
                Path.GetDirectoryName(romPath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(romPath) + ".modules");
            Directory.CreateDirectory(directory);

            foreach (var module in rom.Modules)
            {
                var name = string.IsNullOrWhiteSpace(module.Name) ? "module" : module.Name;
                var safeName = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                var extension = hunkOutput ? ".hunk" : ".bin";
                var fileName = $"{module.Offset:X6}-{safeName}{extension}";
                var path = Path.Combine(directory, fileName);
                var moduleBytes = hunkOutput
                    ? rom.GetModuleHunkBytes(module)
                    : rom.GetModuleBytes(module);
                await File.WriteAllBytesAsync(path, moduleBytes, token);
                OnInformationMessage($"Extracted {FormatModule(module)} to '{path}'");
            }

            return new Result();
        }
        catch (InvalidDataException e)
        {
            return new Result(new Error(e.Message));
        }
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
