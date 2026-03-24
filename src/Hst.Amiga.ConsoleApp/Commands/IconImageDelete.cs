using Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons;

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

        OnInformationMessage($"Reading icon from file '{path}'");
        
        await using var iconStream = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
        var amigaIcon = await AmigaIconHelper.ReadAmigaIcon(iconStream);

        var deleteIconImagesResult = DeleteIconImages(amigaIcon);
        if (deleteIconImagesResult.IsFaulted)
        {
            return deleteIconImagesResult;
        }
        
        DiskObjectHelper.UpdateGadgetFlags(amigaIcon.DiskObject);
        
        OnInformationMessage($"Writing icon to file '{path}'");
        
        await AmigaIconHelper.WriteAmigaIcon(amigaIcon, iconStream);

        return new Result();
    }

    private Result DeleteIconImages(AmigaIcon amigaIcon)
    {
        if (!imageType.HasValue)
        {
            OnInformationMessage("Deleting all icon images");
            DeleteAllIconImages(amigaIcon);
            return new Result();
        }
        
        switch (imageType)
        {
            case ImageType.Planar:
                OnInformationMessage("Deleting planar icon images");
                CreateDefaultPlanarImages(amigaIcon.DiskObject);
                break;
            case ImageType.NewIcon:
                OnInformationMessage("Deleting new icon images");
                CreateDefaultPlanarImages(amigaIcon.DiskObject);
                NewIconHelper.RemoveNewIconImages(amigaIcon.DiskObject);
                break;
            case ImageType.ColorIcon:
                OnInformationMessage("Deleting color icon images");
                CreateDefaultPlanarImages(amigaIcon.DiskObject);
                if (amigaIcon.ColorIcon.Images != null)
                {
                    amigaIcon.ColorIcon.Images = Array.Empty<ColorIconImage>();
                }
                break;
            case ImageType.TrueColorIcon:
                OnInformationMessage("Deleting true color icon images");
                CreateDefaultPlanarImages(amigaIcon.DiskObject);
                amigaIcon.Kind = AmigaIcon.IconKind.Normal;
                if (amigaIcon.TrueColorIcons != null)
                {
                    amigaIcon.TrueColorIcons = Array.Empty<TrueColorIcon>();
                }
                break;
            default:
                return new Result(new Error($"Image type '{imageType}' is not supported for icon image delete"));
        }

        return new Result();
    }
}