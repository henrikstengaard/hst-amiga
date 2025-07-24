using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.ConsoleApp.Extensions;
using Hst.Amiga.Roms;
using Hst.Core;

namespace Hst.Amiga.ConsoleApp.Commands;

public class EpromBuild32BitCommand : CommandBase
{
    private readonly string amigaModel;
    private readonly string kickstartRomPath;
    private readonly string hiRomIcName;
    private readonly string loRomIcName;
    private readonly EpromType? epromType;
    private readonly int? size;

    public EpromBuild32BitCommand(string amigaModel, string kickstartRomPath, string hiRomIcName, string loRomIcName,
        EpromType? epromType, int? size)
    {
        this.amigaModel = amigaModel.ToUpperInvariant();
        this.kickstartRomPath = kickstartRomPath;
        this.hiRomIcName = hiRomIcName;
        this.loRomIcName = loRomIcName;
        this.epromType = epromType;
        this.size = size;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        if (!amigaModel.Equals("a1200", StringComparison.OrdinalIgnoreCase) &&
            !amigaModel.Equals("a3000", StringComparison.OrdinalIgnoreCase) &&
            !amigaModel.Equals("a4000", StringComparison.OrdinalIgnoreCase))
        {
            return new Result(new Error($"Unsupported Amiga model '{amigaModel}'. Supported models are A1200, A3000, and A4000."));
        }
        
        OnInformationMessage($"Reading {amigaModel} Kickstart rom '{kickstartRomPath}'");

        var kickstartRomBytes = await File.ReadAllBytesAsync(kickstartRomPath, token);

        OnInformationMessage($"Building EPROMs of size {size} from {amigaModel} Kickstart rom");

        var epromResult = EpromBuilder.Build32BitEprom(kickstartRomBytes, epromType, size);
        if (epromResult.IsFaulted)
        {
            return new Result(epromResult.Error);
        }

        var (hiEpromBytes, loEpromBytes) = epromResult.Value;

        var epromName = epromType?.ToString().ToLowerInvariant() ??
                         size?.ToString().ToLowerInvariant() ??
                         nameof(EpromType.Am27C400).ToLowerInvariant();
        var kickstartDir = Path.GetDirectoryName(kickstartRomPath) ?? string.Empty;
        var kickstartName = Path.GetFileNameWithoutExtension(kickstartRomPath);
        var hiEpromPath = Path.Combine(kickstartDir, string.Concat(kickstartName, 
            $".{amigaModel.ToLowerInvariant()}.hi.{hiRomIcName.ToLowerInvariant()}.{epromName}.bin"));
        var loEpromPath = Path.Combine(kickstartDir, string.Concat(kickstartName, 
            $".{amigaModel.ToLowerInvariant()}.lo.{loRomIcName.ToLowerInvariant()}.{epromName}.bin"));

        OnInformationMessage($"Writing {amigaModel} HI {hiRomIcName} EPROM to '{hiEpromPath}'");
        await File.WriteAllBytesAsync(hiEpromPath, hiEpromBytes, token);

        OnInformationMessage($"Writing {amigaModel} LO {loRomIcName} EPROM to '{loEpromPath}'");
        await File.WriteAllBytesAsync(loEpromPath, loEpromBytes, token);

        PresentMotherboardSocketOverview(hiEpromBytes.Length);

        return new Result();
    }

    private void PresentMotherboardSocketOverview(int epromSize)
    {
        var isA1200 = amigaModel.Equals("a1200", StringComparison.OrdinalIgnoreCase);

        OnInformationMessage(string.Empty);
        OnInformationMessage(
            $"Burn the EPROMs and install them in the {amigaModel} motherboard {hiRomIcName.ToUpperInvariant()} and {loRomIcName.ToUpperInvariant()} sockets{(isA1200 ? " aligning the right side of EPROM with the right side of the socket" : string.Empty)}.");
        
        var pin42 = isA1200 && epromSize <= Constants.EpromSize.Eprom512KbSize ? "42 X" : string.Empty;
        var pin1 = isA1200 && epromSize <= Constants.EpromSize.Eprom512KbSize ?  " 1 X" : string.Empty;
        var pinPadding = isA1200 && epromSize <= Constants.EpromSize.Eprom512KbSize ?  "    " : string.Empty;

        OnInformationMessage($"Below is an overview of the {amigaModel} motherboard sockets and how the EPROMs are installed:");
        OnInformationMessage(string.Empty);
        
        if (isA1200 && epromSize <= Constants.EpromSize.Eprom512KbSize)
        {
            OnInformationMessage("Note: Pin 1 and 42 on the left side of the socket must not have any connections when using 27C400 EPROMs with A1200 motherboard!");
        }
        
        OnInformationMessage(string.Concat(pin42,
            " -|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-"));
        OnInformationMessage(string.Concat(pinPadding,
            "|                                   |"));
        OnInformationMessage(string.Concat(pinPadding,
            "|                                   |"));
        OnInformationMessage(string.Concat(pinPadding,
            " >                                  |".AddCenteredText($"HI {hiRomIcName.ToUpperInvariant()}")));
        OnInformationMessage(string.Concat(pinPadding,
            "|                                   |"));
        OnInformationMessage(string.Concat(pinPadding,
            "|                                   |"));
        OnInformationMessage(string.Concat(pin1,
            " -|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-"));
        OnInformationMessage(string.Empty);
        OnInformationMessage(string.Concat(pin42,
            " -|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-"));
        OnInformationMessage(string.Concat(pinPadding,
            "|                                   |"));
        OnInformationMessage(string.Concat(pinPadding,
            "|                                   |"));
        OnInformationMessage(string.Concat(pinPadding,
            " >                                  |".AddCenteredText($"LO {loRomIcName.ToUpperInvariant()}")));
        OnInformationMessage(string.Concat(pinPadding,
            "|                                   |"));
        OnInformationMessage(string.Concat(pinPadding,
            "|                                   |"));
        OnInformationMessage(string.Concat(pin1,
            " -|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-"));
    }
}