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

    public IconImageConvert(ILogger<IconImageConvert> logger, string path, ImageType srcType, ImageType destType,
        string palettePath)
    {
        this.logger = logger;
        this.path = path;
        this.srcType = srcType;
        this.destType = destType;
        this.palettePath = palettePath;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        OnInformationMessage($"Reading disk object from icon file '{path}'");
        
        await using var iconStream = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
        var diskObject = await DiskObjectReader.Read(iconStream);
        var colorIcon = iconStream.Position < iconStream.Length
            ? await ColorIconReader.Read(iconStream)
            : new ColorIcon();

        if (srcType == destType)
        {
            return new Result(new Error("Source type is the same as destination type"));
        }

        var images = DecodeIconImages(diskObject, colorIcon).ToList();

        if (!images.Any())
        {
            return new Result(new Error($"No images to convert from source type '{srcType}'"));
        }
        
        RemoveIconImages(diskObject, colorIcon);
        
        EncodeIconImages(diskObject, colorIcon, images);

        OnInformationMessage($"Writing disk object to icon file '{path}'");
        
        await WriteIcon(iconStream, diskObject, colorIcon);

        return new Result();
    }

    private IEnumerable<Image> DecodeIconImages(DiskObject diskObject, ColorIcon colorIcon)
    {
        var images = new List<Image>();

        var imageType = DetectSrcImageType(diskObject, colorIcon);
        
        switch (imageType)
        {
            case ImageType.Planar:
                if (diskObject.FirstImageData != null)
                {
                    OnInformationMessage("Reading planar icon image 1");
                    images.Add(ImageDataDecoder.Decode(diskObject.FirstImageData,
                        GetPalette(diskObject.FirstImageData), true));
                }
                if (diskObject.SecondImageData != null)
                {
                    OnInformationMessage("Reading planar icon image 2");
                    images.Add(ImageDataDecoder.Decode(diskObject.SecondImageData,
                        GetPalette(diskObject.SecondImageData), true));
                }
                break;
            case ImageType.NewIcon:
                var newIcon1 = NewIconHelper.GetNewIconImage(diskObject, 1);
                if (newIcon1 != null)
                {
                    OnInformationMessage("Reading new icon image 1");
                    images.Add(NewIconConverter.ToImage(newIcon1));
                }
                var newIcon2 = NewIconHelper.GetNewIconImage(diskObject, 2);
                if (newIcon2 != null)
                {
                    OnInformationMessage("Reading new icon image 2");
                    images.Add(NewIconConverter.ToImage(newIcon2));
                }
                break;
            case ImageType.ColorIcon:
                if (colorIcon.Images.Length > 0)
                {
                    for (var i = 0; i < (colorIcon.Images.Length > 2 ? 2 : 1); i++)
                    {
                        OnInformationMessage($"Reading color icon image {i + 1}");
                    }
                    images.AddRange(colorIcon.Images.Select(x => x.Image));
                }
                break;                
        }

        return images;
    }

    private static ImageType DetectSrcImageType(DiskObject diskObject, ColorIcon colorIcon)
    {
        if (colorIcon != null && colorIcon.Images.Length > 0)
        {
            return ImageType.ColorIcon;
        }

        return NewIconHelper.GetNewIconImage(diskObject, 1) != null ? ImageType.NewIcon : ImageType.Planar;
    }

    private static void RemoveIconImages(DiskObject diskObject, ColorIcon colorIcon)
    {
        CreateDummyPlanarImages(diskObject);
        NewIconHelper.RemoveNewIconImages(diskObject);
        colorIcon.Images = Array.Empty<ColorIconImage>();
    }

    private void EncodeIconImages(DiskObject diskObject, ColorIcon colorIcon, IEnumerable<Image> images)
    {
        var imagesList = images.ToList();
        switch (destType)
        {
            case ImageType.Planar:
                if (imagesList.Count > 0)
                {
                    OnInformationMessage("Writing planar icon image 1");
                    DiskObjectHelper.SetFirstImage(diskObject, ImageDataEncoder.Encode(imagesList[0]));
                }
                if (imagesList.Count > 1)
                {
                    OnInformationMessage("Writing planar icon image 2");
                    DiskObjectHelper.SetSecondImage(diskObject, ImageDataEncoder.Encode(imagesList[1]));
                }
                break;
            case ImageType.NewIcon:
                if (imagesList.Count > 0)
                {
                    OnInformationMessage("Writing new icon image 1");
                    NewIconHelper.SetNewIconImage(diskObject, 1, NewIconConverter.ToNewIcon(imagesList[0]));
                }
                if (imagesList.Count > 1)
                {
                    OnInformationMessage("Writing new icon image 2");
                    NewIconHelper.SetNewIconImage(diskObject, 2, NewIconConverter.ToNewIcon(imagesList[1]));
                }
                break;
            case ImageType.ColorIcon:
                if (imagesList.Count > 0)
                {
                    OnInformationMessage("Writing color icon image 1");
                    ColorIconHelper.SetFirstImage(colorIcon, imagesList[0]);
                }
                if (imagesList.Count > 1)
                {
                    OnInformationMessage("Writing color icon image 2");
                    ColorIconHelper.SetSecondImage(colorIcon, imagesList[1]);
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