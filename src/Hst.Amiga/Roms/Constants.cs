using System.Collections.Generic;

namespace Hst.Amiga.Roms
{
    public static class Constants
    {
        public static class EpromSize
        {
            public const int Eprom256KbSize = 256 * 1024;
            public const int Eprom512KbSize = 512 * 1024;
            public const int Eprom1024KbSize = 1024 * 1024;
            public const int Eprom2048KbSize = 2048 * 1024;
            public const int Eprom4096KbSize = 4096 * 1024;
            
            public static int GetEpromSize(EpromType type)
            {
                if (!Sizes.TryGetValue(type, out var size))
                {
                    throw new System.ArgumentOutOfRangeException(nameof(type), type, null);
                }

                return size;
            }

            public static readonly IDictionary<EpromType, int> Sizes = new Dictionary<EpromType, int>
            {
                { EpromType.Am27C400, Eprom512KbSize }, // AMD AM27C400, 1 * 512KB, TL866 adapter bank 0
                { EpromType.Am27C800, Eprom1024KbSize }, // AMD AM27C800, 2 * 512KB, TL866 adapter bank 0-1
                { EpromType.Am27C160, Eprom2048KbSize }, // AMD AM27C160, 4 * 512KB, TL866 adapter bank 0-3 
                { EpromType.Am27C322, Eprom4096KbSize }, // AMD AM27C322, 8 * 512KB, TL866 adapter bank 0-7
                { EpromType.M27C400, Eprom512KbSize }, // ST MC27C400, 1 * 512KB, TL866 adapter bank 0
                { EpromType.Mx27C4100, Eprom512KbSize }, // MX MC27C4100, 1 * 512KB, TL866 adapter bank 0
                { EpromType.Mx29F1615, Eprom2048KbSize } // MX MX29F1615, 4 * 512KB, TL866 adapter bank 0-3
            };
        }
    }
}