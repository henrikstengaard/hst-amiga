﻿namespace Hst.Amiga.ConsoleApp.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core;
using DataTypes.DiskObjects;
using Microsoft.Extensions.Logging;

public class IconInfoCommand : CommandBase
{
    private readonly ILogger<IconInfoCommand> logger;
    private readonly string path;
    private readonly bool all;

    public IconInfoCommand(ILogger<IconInfoCommand> logger, string path, bool all)
    {
        this.logger = logger;
        this.path = path;
        this.all = all;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        await using var iconStream = File.OpenRead(path);
        var diskObject = await DiskObjectReader.Read(iconStream);

        ColorIcon colorIcon = null;
        if (iconStream.Position < iconStream.Length)
        {
            colorIcon = await ColorIconReader.Read(iconStream);
        }

        OnInformationMessage("Icon:");
        OnInformationMessage($"- Type: {diskObject.Type} ({GetIconType(diskObject)})");
        OnInformationMessage($"- Position x: {diskObject.CurrentX}");
        OnInformationMessage($"- Position y: {diskObject.CurrentY}");
        OnInformationMessage($"- Stack size: {diskObject.StackSize}");

        if ((diskObject.Type == Constants.DiskObjectTypes.DISK || 
             diskObject.Type == Constants.DiskObjectTypes.DRAWER || 
             diskObject.Type == Constants.DiskObjectTypes.GARBAGE) && 
            diskObject.DrawerData != null)
        {
            OnInformationMessage($"Drawer: {diskObject.DrawerData.LeftEdge}");
            OnInformationMessage($"- Position x: {diskObject.DrawerData.LeftEdge}");
            OnInformationMessage($"- Position y: {diskObject.DrawerData.TopEdge}");
            OnInformationMessage($"- Width: {diskObject.DrawerData.Width}");
            OnInformationMessage($"- Height: {diskObject.DrawerData.Height}");
        }

        if (diskObject.FirstImageData != null)
        {
            OnInformationMessage($"Planar icon 1:");
            OnInformationMessage($"- Width: {diskObject.FirstImageData.Width}");
            OnInformationMessage($"- Height: {diskObject.FirstImageData.Height}");
            OnInformationMessage($"- Depth: {Math.Pow(2, diskObject.FirstImageData.Depth)} bpp");
        }

        if (diskObject.SecondImageData != null)
        {
            OnInformationMessage($"Planar icon 2:");
            OnInformationMessage($"- Width: {diskObject.SecondImageData.Width}");
            OnInformationMessage($"- Height: {diskObject.SecondImageData.Height}");
            OnInformationMessage($"- Depth: {Math.Pow(2, diskObject.SecondImageData.Depth)} bpp");
        }

        
        var textDatas = diskObject.ToolTypes?.TextDatas?.ToList() ?? new List<TextData>();

        if (!textDatas.Any())
        {
            return new Result();
        }

        var newIcon1 = new NewIconToolTypesDecoder(textDatas).Decode(1);
        if (newIcon1 != null)
        {
            OnInformationMessage($"New Icon 1:");
            OnInformationMessage($"- Width: {newIcon1.Width}");
            OnInformationMessage($"- Height: {newIcon1.Height}");
            OnInformationMessage($"- Depth: {newIcon1.Depth} bpp");
            OnInformationMessage($"- Transparent: {newIcon1.Transparent}");
        }
        
        var newIcon2 = new NewIconToolTypesDecoder(textDatas).Decode(2);
        if (newIcon2 != null)
        {
            OnInformationMessage($"New Icon 2:");
            OnInformationMessage($"- Width: {newIcon2.Width}");
            OnInformationMessage($"- Height: {newIcon2.Height}");
            OnInformationMessage($"- Depth: {newIcon2.Depth} bpp");
            OnInformationMessage($"- Transparent: {newIcon2.Transparent}");
        }

        if (colorIcon != null)
        {
            for (var i = 0; i < colorIcon.Images.Length; i++)
            {
                var colorIconImage = colorIcon.Images[i];
                OnInformationMessage($"Color Icon {i + 1}:");
                OnInformationMessage($"- Width: {colorIconImage.Width}");
                OnInformationMessage($"- Height: {colorIconImage.Height}");
                OnInformationMessage($"- Depth: {colorIconImage.BitsPerPixel} bpp");
                OnInformationMessage($"- Transparent: {colorIconImage.IsTransparent}");
            }
        }
        
        OnInformationMessage("Tool types:");
        var decodedTextDatas = textDatas.Select(x => AmigaTextHelper.GetString(x.Data));

        if (!all)
        {
            decodedTextDatas = decodedTextDatas.Where(x => !x.StartsWith("IM1=") && !x.StartsWith("IM2="));
        }
        
        OnInformationMessage(string.Join(Environment.NewLine, decodedTextDatas));

        return new Result();
    }

    private static string GetIconType(DiskObject diskObject)
    {
        return diskObject.Type switch
        {
            Constants.DiskObjectTypes.DISK => "Disk",
            Constants.DiskObjectTypes.DRAWER => "Drawer",
            Constants.DiskObjectTypes.TOOL => "Tool",
            Constants.DiskObjectTypes.PROJECT => "Project",
            Constants.DiskObjectTypes.GARBAGE => "Garbage",
            Constants.DiskObjectTypes.DEVICE => "Device",
            Constants.DiskObjectTypes.KICK => "Kick",
            Constants.DiskObjectTypes.APP_ICON => "AppIcon",
            _ => "Unknown"
        };
    }
}