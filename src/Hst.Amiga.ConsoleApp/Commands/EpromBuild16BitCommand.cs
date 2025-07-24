using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.ConsoleApp.Extensions;
using Hst.Amiga.Roms;
using Hst.Core;

namespace Hst.Amiga.ConsoleApp.Commands;

public class EpromBuild16BitCommand : CommandBase
{
    private readonly string amigaModel;
    private readonly string kickstartRomPath;
    private readonly string romIcName;
    private readonly EpromType? epromType;
    private readonly int? size;

    public EpromBuild16BitCommand(string amigaModel, string kickstartRomPath, string romIcName, EpromType? epromType,
        int? size)
    {
        this.amigaModel = amigaModel.ToUpperInvariant();
        this.kickstartRomPath = kickstartRomPath;
        this.romIcName = romIcName;
        this.epromType = epromType;
        this.size = size;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        if (!amigaModel.Equals("a500", StringComparison.OrdinalIgnoreCase) &&
            !amigaModel.Equals("a600", StringComparison.OrdinalIgnoreCase) &&
            !amigaModel.Equals("a2000", StringComparison.OrdinalIgnoreCase))
        {
            return new Result(new Error($"Unsupported Amiga model '{amigaModel}'. Supported models are A500, A600, and A2000."));
        }
        
        OnInformationMessage($"Reading {amigaModel} Kickstart rom '{kickstartRomPath}'");

        var kickstartRomBytes = await File.ReadAllBytesAsync(kickstartRomPath, token);

        OnInformationMessage($"Building EPROM of size {size} from {amigaModel} Kickstart rom");

        var epromResult = EpromBuilder.Build16BitEprom(kickstartRomBytes, epromType, size);
        if (epromResult.IsFaulted)
        {
            return new Result(epromResult.Error);
        }

        var epromBytes = epromResult.Value;

        var epromName = epromType?.ToString().ToLowerInvariant() ??
                        size?.ToString().ToLowerInvariant() ??
                        nameof(EpromType.Am27C400);
        var kickstartDir = Path.GetDirectoryName(kickstartRomPath) ?? string.Empty;
        var kickstartName = Path.GetFileNameWithoutExtension(kickstartRomPath);
        var epromPath = Path.Combine(kickstartDir, string.Concat(kickstartName, 
            $".{amigaModel.ToLowerInvariant()}.{romIcName}.{epromName}.bin"));

        OnInformationMessage($"Writing {amigaModel} {romIcName} EPROM to '{epromPath}'");
        await File.WriteAllBytesAsync(epromPath, epromBytes, token);

        switch (amigaModel.ToLowerInvariant())
        {
            case "a500":
            case "a2000":
                PresentMotherboardVerticalSocketOverview(epromBytes.Length);
                break;
            case "a600":
                PresentMotherboardHorizontalSocketOverview();
                break;
        }

        return new Result();
    }

    private void PresentMotherboardVerticalSocketOverview(int epromSize)
    {
        var isA500 = amigaModel.Equals("a500", StringComparison.OrdinalIgnoreCase);

        OnInformationMessage(string.Empty);
        OnInformationMessage(
            $"Burn the EPROM and install them in the {amigaModel} motherboard {romIcName.ToUpperInvariant()} socket{(isA500 ? " aligning bottom of the EPROM with the bottom of the socket" : string.Empty)}.");
        
        OnInformationMessage(
            $"Below is an overview of the {amigaModel} motherboard socket and how the EPROM is installed:");
        OnInformationMessage(string.Empty);
        
        if (isA500 && epromSize <= Constants.EpromSize.Eprom512KbSize)
        {
            OnInformationMessage("Note: Pin 1 and 42 on the top of the socket must not have any connections when using 27C400 EPROM with A500 rev 8 motherboard!");
            OnInformationMessage("1 X              X 42");
        }
        
        OnInformationMessage("    -----  -----");
        OnInformationMessage("  -|     \\/     |-");
        OnInformationMessage("  -|            |-");
        OnInformationMessage("  -|            |-");
        OnInformationMessage("  -|            |-");
        OnInformationMessage("  -|            |-");
        OnInformationMessage("  -|            |-".AddCenteredText(romIcName.ToUpperInvariant()));
        OnInformationMessage("  -|            |-");
        OnInformationMessage("  -|            |-");
        OnInformationMessage("  -|            |-");
        OnInformationMessage("  -|            |-");
        OnInformationMessage("  -|            |-");
        OnInformationMessage("    ------------");
    }

    private void PresentMotherboardHorizontalSocketOverview()
    {
        OnInformationMessage(
            $"Below is an overview of the {amigaModel} motherboard socket and how the EPROM is installed:");
        OnInformationMessage(" -|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-");
        OnInformationMessage("|                                   |");
        OnInformationMessage("|                                   |");
        OnInformationMessage(" >                                  |".AddCenteredText(romIcName.ToUpperInvariant()));
        OnInformationMessage("|                                   |");
        OnInformationMessage("|                                   |");
        OnInformationMessage(" -|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-");
    }
    
    private void PresentMotherboardHorizontalRightSocketOverview()
    {
        OnInformationMessage(
            $"Below is an overview of the {amigaModel} motherboard socket and how the EPROM is installed:");
        OnInformationMessage(" -|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-");
        OnInformationMessage("|                                   |");
        OnInformationMessage("|                                   |");
        OnInformationMessage("|                                  < ".AddCenteredText(romIcName.ToUpperInvariant()));
        OnInformationMessage("|                                   |");
        OnInformationMessage("|                                   |");
        OnInformationMessage(" -|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-");
    }
}