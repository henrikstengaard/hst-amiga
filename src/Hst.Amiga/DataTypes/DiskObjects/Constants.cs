using System;

namespace Hst.Amiga.DataTypes.DiskObjects
{
    public static class Constants
    {
        public static class DiskObjectTypes
        {
            public const int DISK = 1;                 // a disk
            public const int DRAWER = 2;               // a directory
            public const int TOOL = 3;                  // a program
            public const int PROJECT = 4;               // a project file with defined program to start
            public const int GARBAGE = 5;               // the trashcan
            public const int DEVICE = 6;                // should never appear
            public const int KICK = 7;                  // a kickstart disk
            public const int APP_ICON = 8; // should never appear
        }

        public const int BITS_PER_BYTE = 8;

        public static class NewIcon
        {
            public const int MaxNewIconColors = 255;
            public const int MAX_TEXTDATA_LENGTH = 127;
            public const string Header = "*** DON'T EDIT THE FOLLOWING LINES!! ***";
        }

        /// <summary>
        /// Gadget activation flags, found in inituition.h.
        /// </summary>
        [Flags]
        public enum GadgetActivationFlags
        {
            /// <summary>
            /// GACT_RELVERIFY if you want to verify that the pointer was still over the gadget when the select button was released.
            /// Will cause an IDCMP_GADGETUP message to be sent if so.
            /// </summary>
            GactRelverify = 0x1,

            /// <summary>
            /// GACT_IMMEDIATE, when set, informs the caller that the gadget was activated when it was activated.
            /// This flag works in conjunction with the GACT_RELVERIFY flag.
            /// </summary>
            GactImmediate = 0x2
        }

        /// <summary>
        /// Gadget flags, found in inituition.h.
        /// </summary>
        [Flags]
        public enum GadgetFlags
        {
            /// <summary>
            /// Complement the select box
            /// </summary>
            GflgGadghcomp = 0x0,

            /// <summary>
            /// Draw a box around the image
            /// </summary>
            GflgGadghbox = 0x1,
            
            /// <summary>
            /// Blast in this alternate image
            /// </summary>
            GflgGadghimage = 0x2,
            
            /// <summary>
            /// Don't highlight
            /// </summary>
            GflgGadghnone = 0x3,
            
            /// <summary>
            /// set if GadgetRender and SelectRender point to an Image structure, clear if they point to Border structures
            /// </summary>
            GflgGadgimage = 0x4
        }
    }
}