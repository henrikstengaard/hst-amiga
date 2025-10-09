using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.Roms;
using Hst.Core;

namespace Hst.Amiga.ConsoleApp.Commands;

public class EpromFillCommand : CommandBase
{
    private readonly string kickstartRomPath;
    private readonly EpromType? epromType;
    private readonly int? size;
    private readonly bool? zeroFill;

    public EpromFillCommand(string kickstartRomPath, EpromType? epromType, int? size, bool? zeroFill)
    {
        this.kickstartRomPath = kickstartRomPath;
        this.epromType = epromType;
        this.size = size;
        this.zeroFill = zeroFill;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Reading Kickstart rom '{kickstartRomPath}'");

        var kickstartRomBytes = await File.ReadAllBytesAsync(kickstartRomPath, token);

        OnInformationMessage($"Filling {(zeroFill ?? false ? "by concatenating from Kickstart rom" : "with Kickstart rom and zeroes")} until to EPROM {(epromType.HasValue ? epromType : $"size {size}")}");

        var epromResult = EpromBuilder.FillEprom(kickstartRomBytes, epromType, size, zeroFill ?? false);
        if (epromResult.IsFaulted)
        {
            return new Result(epromResult.Error);
        }

        var epromBytes = epromResult.Value;

        OnInformationMessage($"Filled EPROM is {epromBytes.Length} bytes");

        var epromName = epromType?.ToString().ToLowerInvariant() ??
                        size?.ToString().ToLowerInvariant() ??
                        nameof(EpromType.Am27C400).ToLowerInvariant();
        var kickstartDir = Path.GetDirectoryName(kickstartRomPath) ?? string.Empty;
        var kickstartName = Path.GetFileNameWithoutExtension(kickstartRomPath);
        var epromPath = Path.Combine(kickstartDir, string.Concat(kickstartName, 
            $".filled.{epromName}.bin"));

        OnInformationMessage($"Writing filled EPROM to '{epromPath}'");
        await File.WriteAllBytesAsync(epromPath, epromBytes, token);

        return new Result();
    }
}