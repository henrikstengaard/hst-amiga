namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System;

    public class DrawerData2
    {
        public uint Flags { get; set; }
        public ushort ViewModes { get; set; }

        [Flags]
        public enum FlagEnum : uint
        {
            /// <summary>
            /// View icons with view mode from parent drawer (OS1.x compatibility)
            /// </summary>
            ViewIconsOs1X = 0,
            /// <summary>
            /// View icons in drawer
            /// </summary>
            ViewIcons = 1,
            /// <summary>
            /// View all files in drawer
            /// </summary>
            AllFiles = 2
        }
        
        [Flags]
        public enum ViewModesEnum : uint
        {
            /// <summary>
            /// Show icons (OS1.x compatibility)
            /// </summary>
            ShowIconsOs1X = 0,
            /// <summary>
            /// Show icons
            /// </summary>
            ShowIcons = 1,
            /// <summary>
            /// Show in list sorted by name
            /// </summary>
            ShowSortedByName = 2,
            /// <summary>
            /// Show in list sorted by date
            /// </summary>
            ShowSortedByDate = 3,
            /// <summary>
            /// Show in list sorted by size
            /// </summary>
            ShowSortedBySize = 4,
            /// <summary>
            /// Show in list sorted by type
            /// </summary>
            ShowSortedByType = 5
        }
    }
}