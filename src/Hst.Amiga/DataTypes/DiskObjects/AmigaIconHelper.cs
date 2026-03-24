using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.DiskObjects.ColorIcons;
using Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons;
using Hst.Core.Extensions;

namespace Hst.Amiga.DataTypes.DiskObjects
{
    public static class AmigaIconHelper
    {
        /// <summary>
        /// Read amiga icon from stream.
        /// This is usually a disk object followed by a color icon.
        /// In case it's a PNG file, it will be converted to a disk object and a color icon.
        /// </summary>
        /// <param name="stream">Stream with Amiga icon.</param>
        /// <param name="readColorIcon">Read color icon.</param>
        /// <returns>Amiga icon.</returns>
        public static async Task<AmigaIcon> ReadAmigaIcon(Stream stream, bool readColorIcon = true)
        {
            stream.Position = 0;

            var magicBytes = await stream.ReadBytes(4);

            if (magicBytes.Length < 4)
            {
                return null;
            }

            stream.Position = 0;

            if (magicBytes.SequenceEqual(TrueColorIcons.Constants.PngMagicBytes))
            {
                var trueColorIcons = (await TrueColorIconReader.ReadTrueColorIcons(stream)).ToList();

                return new AmigaIcon
                {
                    Kind = AmigaIcon.IconKind.TrueColor,
                    DiskObject = DiskObjectHelper.GetDiskObject(trueColorIcons),
                    ColorIcon = null,
                    TrueColorIcons = trueColorIcons
                };
            }

            var diskObject = await DiskObjectReader.Read(stream);

            var colorIcon = readColorIcon && await ColorIconReader.HasColorIcon(stream)
                ? await ColorIconReader.Read(stream)
                : null;

            var tailingData = stream.Position < stream.Length
                ? await stream.ReadBytes((int)(stream.Length - stream.Position))
                : Array.Empty<byte>();

            return new AmigaIcon
            {
                Kind = AmigaIcon.IconKind.Normal,
                DiskObject = diskObject,
                ColorIcon = colorIcon,
                TrueColorIcons = null,
                TailingData = tailingData
            };
        }

        public static async Task WriteDiskObject(DiskObject diskObject, Stream stream)
        {
            // write disk object and update length
            stream.Position = 0;
            await DiskObjectWriter.Write(diskObject, stream);
            stream.SetLength(stream.Position);
        }

        public static async Task WriteColorIcon(ColorIcon colorIcon, Stream stream)
        {
            if (colorIcon == null || colorIcon.Images.Length == 0)
            {
                return;
            }
        
            await ColorIconWriter.Write(stream, colorIcon, true, true);
            stream.SetLength(stream.Position);
        }

        public static async Task WriteAmigaIcon(AmigaIcon amigaIcon, Stream stream)
        {
            stream.Position = 0;
            
            if (amigaIcon.Kind == AmigaIcon.IconKind.TrueColor)
            {
                await TrueColorIconWriter.WriteTrueColorIcons(amigaIcon.TrueColorIcons, stream);
            }
            else
            {
                await WriteDiskObject(amigaIcon.DiskObject, stream);
                
                if (amigaIcon.ColorIcon != null)
                {
                    await WriteColorIcon(amigaIcon.ColorIcon, stream);
                }
                
                if (amigaIcon.TailingData != null && amigaIcon.TailingData.Length > 0)
                {
                    await stream.WriteAsync(amigaIcon.TailingData, 0, amigaIcon.TailingData.Length);
                    stream.SetLength(stream.Position);
                }
            }
        }
    }
}