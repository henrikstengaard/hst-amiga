namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System.Collections.Generic;

    public static class NewIconToolTypesEncoder
    {
        public static IEnumerable<TextData> Encode(int imageNumber, NewIcon newIcon)
        {
            var encoder = new NewIconAsciiEncoder(imageNumber);
            
            // set new icon palette uses 8 bits per value
            encoder.SetBitsPerValue(8);

            // write new icon header
            encoder.Add((byte)(newIcon.Transparent ? 66 : 67));
            encoder.Add((byte)(0x21 + newIcon.Width));
            encoder.Add((byte)(0x21 + newIcon.Height));
            
            // write number of palette colors
            var colors = newIcon.Image.Palette.Colors.Count;
            encoder.Add((byte)(0x21 + (colors >> 6)));
            encoder.Add((byte)(0x21 + (colors & 0x3f)));

            // encode palette
            foreach (var color in newIcon.Image.Palette.Colors)
            {
                encoder.Encode((byte)color.R);
                encoder.Encode((byte)color.G);
                encoder.Encode((byte)color.B);
            }
            
            // flush any pending bits and prepare next text data
            encoder.Flush();
            
            // set new icon pixel data uses depth bits per value
            encoder.SetBitsPerValue(newIcon.Depth);
            
            var offset = 0;
            for (var y = 0; y < newIcon.Image.Height; y++)
            {
                for (var x = 0; x < newIcon.Image.Width; x++)
                {
                    encoder.Encode(newIcon.Image.PixelData[offset++]);
                }
            }
            
            // flush any pending bits and prepare next text data
            encoder.Flush();

            return encoder.TextDatas;
        }
    }
}