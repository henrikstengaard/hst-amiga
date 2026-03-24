using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.DiskObjects;
using Hst.Amiga.DataTypes.DiskObjects.ColorIcons;
using Hst.Core;
using Microsoft.Extensions.Logging;

namespace Hst.Amiga.ConsoleApp.Commands;

public class IconFixCommand : IconCommandBase
{
    private readonly ILogger<IconFixCommand> logger;
    private readonly string path;

    public IconFixCommand(ILogger<IconFixCommand> logger, string path)
    {
        this.logger = logger;
        this.path = path;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Reading icon from file '{path}'");

        await using var iconStream = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
        
        var diskObject = await DiskObjectReader.Read(iconStream, true);

        ColorIcon colorIcon = null;
        do
        {
            if (await ColorIconReader.HasColorIcon(iconStream))
            {
                colorIcon = await ColorIconReader.Read(iconStream);
                break;
            }

            iconStream.ReadByte();
        } while(iconStream.Position < iconStream.Length);
        
        OnInformationMessage($"Color icon {(colorIcon != null ? "found" : "not found")} in icon file '{path}'");

        var fixedPath = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty,
            string.Concat(Path.GetFileNameWithoutExtension(path), ".fixed.info"));
        
        OnInformationMessage($"Writing icon to file '{fixedPath}'");

        await using var fixedIconStream = File.OpenWrite(fixedPath);

        await AmigaIconHelper.WriteDiskObject(diskObject, fixedIconStream);
        await AmigaIconHelper.WriteColorIcon(colorIcon, fixedIconStream);

        return new Result();
    }
}