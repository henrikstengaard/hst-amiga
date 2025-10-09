using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.Roms;
using Hst.Core;

namespace Hst.Amiga.ConsoleApp.Commands;

public class EpromByteSwapCommand : CommandBase
{
    private readonly string kickstartRomPath;

    public EpromByteSwapCommand(string kickstartRomPath)
    {
        this.kickstartRomPath = kickstartRomPath;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Reading Kickstart rom '{kickstartRomPath}'");

        var kickstartRomBytes = await File.ReadAllBytesAsync(kickstartRomPath, token);

        OnInformationMessage($"Byte swapping to EPROM size {kickstartRomBytes.Length} from Kickstart rom");

        var epromResult = EpromBuilder.ByteSwapEprom(kickstartRomBytes);
        if (epromResult.IsFaulted)
        {
            return new Result(epromResult.Error);
        }

        var epromBytes = epromResult.Value;
    
        OnInformationMessage($"Byte swapped EPROM is {epromBytes.Length} bytes");

        var kickstartDir = Path.GetDirectoryName(kickstartRomPath) ?? string.Empty;
        var kickstartName = Path.GetFileNameWithoutExtension(kickstartRomPath);
        var epromPath = Path.Combine(kickstartDir, string.Concat(kickstartName, 
            $".byteswapped.bin"));

        OnInformationMessage($"Writing byte swapped EPROM to '{epromPath}'");
        await File.WriteAllBytesAsync(epromPath, epromBytes, token);

        return new Result();
    }
}