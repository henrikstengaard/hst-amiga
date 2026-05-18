using Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons;

namespace Hst.Amiga.ConsoleApp.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core;
using DataTypes.DiskObjects;
using DataTypes.DiskObjects.ColorIcons;
using DataTypes.DiskObjects.NewIcons;
using Imaging;
using Microsoft.Extensions.Logging;

public class IconImageConvert : IconCommandBase
{
    private readonly ILogger<IconImageConvert> logger;
    private readonly string path;
    private readonly ImageType srcType;
    private readonly ImageType destType;
    private readonly string palettePath;
    private readonly bool deleteIcons;

    public IconImageConvert(ILogger<IconImageConvert> logger, string path, ImageType srcType, ImageType destType,
        string palettePath, bool deleteIcons)
    {
        this.logger = logger;
        this.path = path;
        this.srcType = srcType;
        this.destType = destType;
        this.palettePath = palettePath;
        this.deleteIcons = deleteIcons;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Reading icon from file '{path}'");
        
        await using var iconStream = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
        var amigaIcon = await AmigaIconHelper.ReadAmigaIcon(iconStream);

        if (srcType == destType)
        {
            return new Result(new Error("Source type is the same as destination type"));
        }

        var images = DecodeIconImages(amigaIcon).ToList();

        if (!images.Any())
        {
            return new Result(new Error($"No images to convert from source type '{srcType}'"));
        }

        if (deleteIcons)
        {
            DeleteAllIconImages(amigaIcon);
        }
        
        await EncodeIconImages(amigaIcon, images);

        OnInformationMessage($"Writing icon to file '{path}'");

        await AmigaIconHelper.WriteAmigaIcon(amigaIcon, iconStream);

        return new Result();
    }

    private IEnumerable<Image> DecodeIconImages(AmigaIcon amigaIcon)
    {
        var images = new List<Image>();

        switch (srcType)
        {
            case ImageType.Planar:
                if (amigaIcon.DiskObject?.FirstImageData != null)
                {
                    OnInformationMessage("Reading planar icon image 1");
                    images.Add(ImageDataDecoder.Decode(amigaIcon.DiskObject.FirstImageData,
                        GetPalette(amigaIcon.DiskObject.FirstImageData), true));
                }
                if (amigaIcon.DiskObject?.SecondImageData != null)
                {
                    OnInformationMessage("Reading planar icon image 2");
                    images.Add(ImageDataDecoder.Decode(amigaIcon.DiskObject.SecondImageData,
                        GetPalette(amigaIcon.DiskObject.SecondImageData), true));
                }
                break;
            case ImageType.NewIcon:
                var newIcon1 = NewIconHelper.GetNewIconImage(amigaIcon.DiskObject, 1);
                if (newIcon1 != null)
                {
                    OnInformationMessage("Reading new icon image 1");
                    images.Add(NewIconConverter.ToImage(newIcon1));
                }
                var newIcon2 = NewIconHelper.GetNewIconImage(amigaIcon.DiskObject, 2);
                if (newIcon2 != null)
                {
                    OnInformationMessage("Reading new icon image 2");
                    images.Add(NewIconConverter.ToImage(newIcon2));
                }
                break;
            case ImageType.ColorIcon:
                if (amigaIcon.ColorIcon.Images.Length > 0)
                {
                    for (var i = 0; i < (amigaIcon.ColorIcon.Images.Length > 2 ? 2 : 1); i++)
                    {
                        OnInformationMessage($"Reading color icon image {i + 1}");
                    }
                    images.AddRange(amigaIcon.ColorIcon.Images.Select(x => x.Image));
                }
                break;                
            case ImageType.TrueColorIcon:
                if (amigaIcon.TrueColorIcons.Count > 0)
                {
                    for (var i = 0; i < (amigaIcon.TrueColorIcons.Count > 2 ? 2 : 1); i++)
                    {
                        OnInformationMessage($"Reading true color icon image {i + 1}");
                        
                        images.Add(amigaIcon.TrueColorIcons[i].Image);
                    }
                }
                
                break;                
        }

        return images;
    }

    private static ImageType DetectSrcImageType(AmigaIcon amigaIcon)
    {
        if (amigaIcon.Kind == AmigaIcon.IconKind.TrueColor)
        {
            return ImageType.TrueColorIcon;
        }
        
        if (amigaIcon.ColorIcon != null && amigaIcon.ColorIcon.Images.Length > 0)
        {
            return ImageType.ColorIcon;
        }

        return NewIconHelper.GetNewIconImage(amigaIcon.DiskObject, 1) != null
            ? ImageType.NewIcon : ImageType.Planar;
    }

    private async Task EncodeIconImages(AmigaIcon amigaIcon, IEnumerable<Image> images)
    {
        var imagesList = images.ToList();
        switch (destType)
        {
            case ImageType.Planar:
                amigaIcon.Kind = AmigaIcon.IconKind.Normal;
                if (imagesList.Count > 0)
                {
                    OnInformationMessage("Writing planar icon image 1");
                    DiskObjectHelper.SetFirstImage(amigaIcon.DiskObject, ImageDataEncoder.Encode(imagesList[0]));
                }
                if (imagesList.Count > 1)
                {
                    OnInformationMessage("Writing planar icon image 2");
                    DiskObjectHelper.SetSecondImage(amigaIcon.DiskObject, ImageDataEncoder.Encode(imagesList[1]));
                }
                break;
            case ImageType.NewIcon:
                amigaIcon.Kind = AmigaIcon.IconKind.Normal;
                if (imagesList.Count > 0)
                {
                    OnInformationMessage("Writing new icon image 1");
                    NewIconHelper.SetNewIconImage(amigaIcon.DiskObject, 1, NewIconConverter.ToNewIcon(imagesList[0]));
                }
                if (imagesList.Count > 1)
                {
                    OnInformationMessage("Writing new icon image 2");
                    NewIconHelper.SetNewIconImage(amigaIcon.DiskObject, 2, NewIconConverter.ToNewIcon(imagesList[1]));
                }
                break;
            case ImageType.ColorIcon:
                amigaIcon.Kind = AmigaIcon.IconKind.Normal;
                amigaIcon.ColorIcon ??= new ColorIcon();
                if (imagesList.Count > 0)
                {
                    OnInformationMessage("Writing color icon image 1");

                    var image1 = imagesList[0];
                    if (image1.BitsPerPixel > 8)
                    {
                        image1 = ImageConverter.To8Bpp(image1);
                    }
                    
                    ColorIconHelper.SetFirstImage(amigaIcon.ColorIcon, image1);
                }
                if (imagesList.Count > 1)
                {
                    OnInformationMessage("Writing color icon image 2");

                    var image2 = imagesList[1];
                    if (image2.BitsPerPixel > 8)
                    {
                        image2 = ImageConverter.To8Bpp(image2);
                    }
                    
                    ColorIconHelper.SetSecondImage(amigaIcon.ColorIcon, image2);
                }
                break;
            case ImageType.TrueColorIcon:
                amigaIcon.Kind = AmigaIcon.IconKind.TrueColor;
                if (imagesList.Count > 0)
                {
                    OnInformationMessage("Writing true color icon image 1");

                    var image1 = imagesList[0];
                    if (image1.BitsPerPixel < 24)
                    {
                        image1 = ImageConverter.ToTrueColor(image1);
                    }

                    amigaIcon.TrueColorIcons.Add(await TrueColorIconHelper.CreateTrueColorIcon(image1));
                }
                if (imagesList.Count > 1)
                {
                    OnInformationMessage("Writing true color icon image 2");

                    var image2 = imagesList[1];
                    if (image2.BitsPerPixel < 24)
                    {
                        image2 = ImageConverter.ToTrueColor(image2);
                    }

                    amigaIcon.TrueColorIcons.Add(await TrueColorIconHelper.CreateTrueColorIcon(image2));
                }

                break;
        }
    }
    
    private Palette GetPalette(ImageData imageData)
    {
        if (string.IsNullOrWhiteSpace(palettePath))
        {
            if (imageData.Depth <= 2)
            {
                OnInformationMessage($"Using Amiga OS 3.1 4 color palette");
                return AmigaOsPalette.FourColors();
            }
            OnInformationMessage($"Using Amiga OS 3.1 full color palette");
            return AmigaOsPalette.FullPalette();
        }

        OnInformationMessage($"Reading palette from JSON file '{palettePath}'");

        if (!File.Exists(palettePath))
        {
            throw new ArgumentException($"JSON Palette path '{palettePath}' doesn't exist", nameof(palettePath));
        }

        var colors = JsonSerializer.Deserialize<IEnumerable<Models.Color>>(File.OpenRead(palettePath),
            new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            });
        var palette = new Palette(colors.Select(x => new Color(x.R, x.G, x.B, x.A ?? 255)));

        OnInformationMessage(
            $"Palette has '{palette.Colors.Count}' color{(palette.Colors.Count == 1 ? string.Empty : "s")}");

        return palette;
    }
}