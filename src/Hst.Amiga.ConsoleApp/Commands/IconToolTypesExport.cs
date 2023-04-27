namespace Hst.Amiga.ConsoleApp.Commands;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Core;
using DataTypes.DiskObjects;
using Microsoft.Extensions.Logging;

public class IconToolTypesExport : IconCommandBase
{
    private readonly ILogger<IconToolTypesExport> logger;
    private readonly string iconPath;
    private readonly string toolTypesPath;
    private readonly bool excludeNewIcon;

    public IconToolTypesExport(ILogger<IconToolTypesExport> logger, string iconPath, string toolTypesPath, bool excludeNewIcon)
    {
        this.logger = logger;
        this.iconPath = iconPath;
        this.toolTypesPath = toolTypesPath;
        this.excludeNewIcon = excludeNewIcon;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return new Result(new Error("Icon path not defined"));
        }

        OnInformationMessage($"Reading disk object from icon file '{iconPath}'");
        
        await using var iconStream = File.OpenRead(iconPath);
        var diskObject = await DiskObjectReader.Read(iconStream);
        
        var toolTypesStrings = DiskObjectHelper.ConvertToolTypesToStrings(diskObject.ToolTypes).ToList();
        
        if (excludeNewIcon)
        {
            OnInformationMessage($"Excluding new icon images from tool types");
            
            var newIconHeaderStart = -1;
            for (var i = 0; i < toolTypesStrings.Count; i++)
            {
                if (toolTypesStrings[i].IndexOf(Constants.NewIcon.Header, StringComparison.InvariantCulture) < 0)
                {
                    continue;
                }
                newIconHeaderStart = i > 0 && string.IsNullOrWhiteSpace(toolTypesStrings[i - 1]) ? i - 1 : i;
                break;
            }

            if (newIconHeaderStart > -1)
            {
                toolTypesStrings = toolTypesStrings.Take(newIconHeaderStart).ToList();
            }
        }
        
        OnInformationMessage($"Writing tool types to file '{toolTypesPath}'");
        
        await File.WriteAllLinesAsync(toolTypesPath, toolTypesStrings, Encoding.UTF8, token);

        return new Result();
    }
}