using Hst.Core.Extensions;

namespace Hst.Amiga.ConsoleApp.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Core;
using DataTypes.DiskObjects;
using DataTypes.DiskObjects.NewIcons;
using Microsoft.Extensions.Logging;

public class IconToolTypesImport : IconCommandBase
{
    private readonly ILogger<IconToolTypesImport> logger;
    private readonly string iconPath;
    private readonly string toolTypesPath;
    private readonly bool preserveNewIcon;

    public IconToolTypesImport(ILogger<IconToolTypesImport> logger, string iconPath, string toolTypesPath,
        bool preserveNewIcon)
    {
        this.logger = logger;
        this.iconPath = iconPath;
        this.toolTypesPath = toolTypesPath;
        this.preserveNewIcon = preserveNewIcon;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return new Result(new Error("Icon path not defined"));
        }

        OnInformationMessage($"Reading disk object from icon file '{iconPath}'");

        await using var iconStream = File.Open(iconPath, FileMode.Open, FileAccess.ReadWrite);
        var diskObject = await DiskObjectReader.Read(iconStream);
        var colorIconData = iconStream.Position < iconStream.Length
            ? await iconStream.ReadBytes((int)(iconStream.Length - iconStream.Position))
            : Array.Empty<byte>();

        var preservedNewIconTextDatas = new List<TextData>();
        if (preserveNewIcon)
        {
            OnInformationMessage($"Preserving new icon images in tool types");

            var dataTypes = (diskObject.ToolTypes?.TextDatas ?? new List<TextData>()).ToList();
            var newIconHeaderBytes = AmigaTextHelper.GetBytes(Constants.NewIcon.Header);
            var newIconHeaderStart = -1;
            for (var i = 0; i < dataTypes.Count; i++)
            {
                if (!dataTypes[i].StartsWith(newIconHeaderBytes))
                {
                    continue;
                }

                newIconHeaderStart = i > 0 && dataTypes[i - 1].Size > 0 && dataTypes[i - 1].Data[0] == 32 ? i - 1 : i;
                break;
            }

            if (newIconHeaderStart > -1)
            {
                preservedNewIconTextDatas = new[] { " ", Constants.NewIcon.Header }
                    .Select(DiskObjectHelper.CreateTextData)
                    .Concat(dataTypes.Skip(newIconHeaderStart).Where(x => x.StartsWith(NewIconImageHeaderBytes)))
                    .ToList();
            }
        }

        OnInformationMessage($"Reading tool types from file '{toolTypesPath}'");

        var toolTypesLines = await File.ReadAllLinesAsync(toolTypesPath, Encoding.UTF8, token);

        if (preserveNewIcon)
        {
            var importedNewIconHeaderStart = -1;
            for (var i = 0; i < toolTypesLines.Length; i++)
            {
                if (toolTypesLines[i].IndexOf(Constants.NewIcon.Header, StringComparison.InvariantCulture) < 0)
                {
                    continue;
                }

                importedNewIconHeaderStart = i > 0 && toolTypesLines[i - 1] == " " ? i - 1 : i;
                break;
            }

            if (importedNewIconHeaderStart > -1)
            {
                toolTypesLines = toolTypesLines.Take(importedNewIconHeaderStart).ToArray();
            }
        }

        var textDatas = DiskObjectHelper.ConvertStringsToTextDatas(toolTypesLines)
            .Concat(preservedNewIconTextDatas).ToList();
        diskObject.ToolTypes = new ToolTypes
        {
            TextDatas = textDatas
        };
        diskObject.ToolTypesPointer = textDatas.Any() ? 1U : 0U;

        OnInformationMessage($"Writing disk object to icon file '{iconPath}'");

        await WriteIcon(iconStream, diskObject);

        if (colorIconData.Length > 0)
        {
            await iconStream.WriteAsync(colorIconData, 0, colorIconData.Length, token);
        }

        return new Result();
    }

    private static readonly byte[] NewIconImageHeaderBytes = AmigaTextHelper.GetBytes("IM");
}