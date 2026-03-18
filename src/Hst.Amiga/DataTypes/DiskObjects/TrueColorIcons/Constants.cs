using System;

namespace Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons
{
    public static class Constants
    {
        public static class PngChunkTypes
        {
            public static readonly byte[] Ihdr = { 0x49, 0x48, 0x44, 0x52 };
            public static readonly byte[] Idat = { 0x49, 0x44, 0x41, 0x54 };
            public static readonly byte[] Iend = { 0x49, 0x45, 0x4e, 0x44 };
            public static readonly byte[] Icon = { 0x69, 0x63, 0x4f, 0x6e };
        }

        [Flags]
        public enum IconAttributeTags : uint
        {
            ATTR_ICONX = 0x80001001,
            ATTR_ICONY = 0x80001002,
            ATTR_DRAWERX = 0x80001003,
            ATTR_DRAWERY = 0x80001004,
            ATTR_DRAWERWIDTH = 0x80001005,
            ATTR_DRAWERHEIGHT = 0x80001006,
            ATTR_DRAWERFLAGS = 0x80001007,
            
            /// <summary>
            /// Tool window string. OS4 STRPTR, tool window string, length including the tag, multiple of 8.
            /// </summary>
            ATTR_TOOLWINDOW = 0x80001008,
            ATTR_STACKSIZE = 0x80001009,
            ATTR_DEFAULTTOOL = 0x8000100a,
            ATTR_TOOLTYPE = 0x8000100b,
            
            /// <summary>
            /// View modes. OS4 PNG.
            /// </summary>
            ATTR_VIEWMODES = 0x8000100c,
            
            /// <summary>
            /// Drawer data x offset. OS4 ULONG.
            /// </summary>
            ATTR_DD_CURRENTX = 0x8000100d,

            /// <summary>
            /// Drawer data y offset. OS4 ULONG.
            /// </summary>
            ATTR_DD_CURRENTY = 0x8000100e,
            
            /// <summary>
            /// Icon type. OS4 ULONG, icon type (WBDISK...WBKICK).
            /// </summary>
            ATTR_TYPE = 0x8000100f,

            /// <summary>
            /// Frameless property. OS4 ULONG.
            /// </summary>
            ATTR_FRAMELESS = 0x80001010,
            
            /// <summary>
            /// Drawer flags. OS4 ULONG, drawer flags.
            /// </summary>
            ATTR_DRAWERFLAGS3 = 0x80001011,
            
            /// <summary>
            /// Drawer view modes. OS4 ULONG.
            /// </summary>
            ATTR_VIEWMODES2 = 0x80001012,  //OS4 ULONG, drawer view modes

            /// <summary>
            /// Drawer flags 2 . written from AFA to store needed dopus Magellan settings.
            /// </summary>
            ATTR_DRAWERFLAGS2 = 0x80001107
        }
    }
}