namespace Hst.Amiga.ConsoleApp.Commands;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Core;
using DataTypes.DiskObjects;
using DataTypes.DiskObjects.ColorIcons;
using DataTypes.DiskObjects.NewIcons;
using Microsoft.Extensions.Logging;

public class IconImageDelete : IconCommandBase
{
    private readonly ILogger<IconImageDelete> logger;
    private readonly string path;
    private readonly ImageType? imageType;

    public IconImageDelete(ILogger<IconImageDelete> logger, string path, ImageType? imageType)
    {
        this.logger = logger;
        this.path = path;
        this.imageType = imageType;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        if (imageType == ImageType.Auto)
        {
            return new Result(new Error("Auto image type is not supported for icon image import"));
        }

        OnInformationMessage($"Reading disk object from icon file '{path}'");
        
        await using var iconStream = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
        var diskObject = await DiskObjectReader.Read(iconStream);
        var colorIcon = iconStream.Position < iconStream.Length
            ? await ColorIconReader.Read(iconStream)
            : new ColorIcon();

        var deleteIconImagesResult = DeleteIconImages(diskObject, colorIcon);
        if (deleteIconImagesResult.IsFaulted)
        {
            return deleteIconImagesResult;
        }
        
        OnInformationMessage($"Writing disk object to icon file '{path}'");
        
        await WriteIcon(iconStream, diskObject);
        await WriteColorIcon(iconStream, colorIcon);

        return new Result();
    }

    private Result DeleteIconImages(DiskObject diskObject, ColorIcon colorIcon)
    {
        if (!imageType.HasValue)
        {
            OnInformationMessage($"Deleting all icon images");
            DeleteAllIconImages(diskObject, colorIcon);
            return new Result();
        }
        
        switch (imageType)
        {
            case ImageType.Planar:
                OnInformationMessage($"Deleting planar icon images");
                CreateDummyPlanarImages(diskObject);
                break;
            case ImageType.NewIcon:
                OnInformationMessage($"Deleting new icon images");
                NewIconHelper.RemoveNewIconImages(diskObject);
                break;
            case ImageType.ColorIcon:
                OnInformationMessage($"Deleting color icon images");
                colorIcon.Images = Array.Empty<ColorIconImage>();
                break;
            default:
                return new Result(new Error($"Image type '{imageType}' is not supported for icon image delete"));
        }

        return new Result();
    }
}