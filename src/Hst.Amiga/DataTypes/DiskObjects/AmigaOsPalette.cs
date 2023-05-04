namespace Hst.Amiga.DataTypes.DiskObjects
{
    using Imaging;

    public static class AmigaOsPalette
    {
        /// <summary>
        /// Amiga OS 3.1 4 color setting
        /// </summary>
        public static Palette FourColors()
        {
            var palette = new Palette(4);
            
            palette.AddColor(170, 170, 170);
            palette.AddColor(0, 0, 0);
            palette.AddColor(255, 255, 255);
            palette.AddColor(102, 136, 187);
            
            return palette;
        }

        /// <summary>
        /// Amiga OS 3.1 multicolor setting
        /// </summary>
        public static Palette Multicolor()
        {
            var palette = new Palette(8);
            
            palette.AddColor(170, 170, 170);
            palette.AddColor(0, 0, 0);
            palette.AddColor(255, 255, 255);
            palette.AddColor(102, 136, 187);
            
            palette.AddColor(238, 68, 68);
            palette.AddColor(85, 221, 85);
            palette.AddColor(0, 68, 221);
            palette.AddColor(238, 153, 0);
            
            return palette;
        }
        
        /// <summary>
        /// Amiga OS 3.1 full palette
        /// </summary>
        public static Palette FullPalette()
        {
            var palette = new Palette(16);
            
            palette.AddColor(153, 153, 153);
            palette.AddColor(17, 17, 17);
            palette.AddColor(238, 238, 238);
            palette.AddColor(75, 105, 175);
            
            palette.AddColor(119, 119, 119);
            palette.AddColor(187, 187, 187);
            palette.AddColor(204, 170, 119);
            palette.AddColor(221, 102, 153);
            
            // palette.AddColor(102, 34, 0);
            // palette.AddColor(238, 85, 0);
            // palette.AddColor(153, 255, 17);
            // palette.AddColor(238, 187, 0);
            //         
            // palette.AddColor(85, 85, 255);
            // palette.AddColor(153, 34, 255);
            // palette.AddColor(0, 255, 136);
            // palette.AddColor(204, 204, 204);

            return palette;
        }
    }
}