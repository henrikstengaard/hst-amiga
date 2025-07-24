using System;
using Hst.Core;

namespace Hst.Amiga.Roms
{
    public static class EpromBuilder
    {
        public const int EvenHi = 0;
        public const int OddLo = 1;
        public const int WordSize = 2;
        
        public const string RomIcNameCdtv = "u13";
        public const string RomIcNameA500 = "u6";
        public const string RomIcNameA600 = "u6";
        public const string RomIcNameA2000 = "u500";

        public const string HiRomIcNameA1200 = "u6a";
        public const string LoRomIcNameA1200 = "u6b";
        
        public const string HiRomIcNameA3000 = "u180";
        public const string LoRomIcNameA3000 = "u181";
        
        public const string HiRomIcNameA4000 = "u175";
        public const string LoRomIcNameA4000 = "u176";

        public static Result<int> GetEpromSize(EpromType? epromType, int? size)
        {
            if (!epromType.HasValue && !size.HasValue)
            {
                return new Result<int>(Constants.EpromSize.Eprom512KbSize);
            }
            
            if (size.HasValue)
            {
                return new Result<int>(size.Value);
            }

            return !Constants.EpromSize.Sizes.TryGetValue(epromType.Value, out var epromSize)
                ? new Result<int>(new Error($"Invalid size '{size}'"))
                : new Result<int>(epromSize);
        }

        /// <summary>
        /// Build 16-bit EPROM from a kickstart rom used in Amiga 500, 600, 1000 and 2000.
        /// </summary>
        /// <param name="kickstartRomBytes">Kickstart rom bytes to build EPROMs from.</param>
        /// <param name="epromType">EPROM type to build for.</param>
        /// <param name="size">Size of EPROM in bytes to build for.</param>
        /// <returns>EPROM bytes.</returns>
        public static Result<byte[]> Build16BitEprom(byte[] kickstartRomBytes, EpromType? epromType, int? size)
        {
            if (kickstartRomBytes.Length % Constants.EpromSize.Eprom256KbSize != 0)
            {
                return new Result<byte[]>(new Error($"Kickstart rom size {kickstartRomBytes.Length} is not a multiple of {Constants.EpromSize.Eprom256KbSize} bytes."));
            }
            
            var epromSizeResult = GetEpromSize(epromType, size);
            if (epromSizeResult.IsFaulted)
            {
                return new Result<byte[]>(epromSizeResult.Error);
            }
        
            var epromSize = epromSizeResult.Value;
            if (epromSize % Constants.EpromSize.Eprom256KbSize != 0)
            {
                return new Result<byte[]>(new Error($"Eprom size {size} is not a multiple of {Constants.EpromSize.Eprom256KbSize} bytes."));
            }
            
            var byteSwappedRomBytes = ByteSwap(kickstartRomBytes);

            if (epromSize < byteSwappedRomBytes.Length)
            {
                return new Result<byte[]>(new Error($"Eprom size {size} doesn't fit Kickstart rom."));
            }

            var epromBytes = Fill(byteSwappedRomBytes, epromSize);

            return new Result<byte[]>(epromBytes);
        }

        /// <summary>
        /// Build 32-bit EPROMs from a kickstart rom used in Amiga 1200, 3000 and 4000.
        /// </summary>
        /// <param name="kickstartRomBytes">Kickstart rom bytes to build EPROMs from.</param>
        /// <param name="epromType">EPROM type to build for.</param>
        /// <param name="size">Size of EPROM in bytes to build for.</param>
        /// <returns>Hi EPROM bytes, Lo EPROM bytes.</returns>
        public static Result<Tuple<byte[], byte[]>> Build32BitEprom(byte[] kickstartRomBytes, EpromType? epromType, int? size)
        {
            if (kickstartRomBytes.Length % Constants.EpromSize.Eprom256KbSize != 0)
            {
                return new Result<Tuple<byte[], byte[]>>(new Error($"Kickstart rom size {kickstartRomBytes.Length} is not a multiple of {Constants.EpromSize.Eprom256KbSize} bytes."));
            }
            
            var epromSizeResult = GetEpromSize(epromType, size);
            if (epromSizeResult.IsFaulted)
            {
                return new Result<Tuple<byte[], byte[]>>(epromSizeResult.Error);
            }
        
            var epromSize = epromSizeResult.Value;
            if (epromSize % Constants.EpromSize.Eprom256KbSize != 0)
            {
                return new Result<Tuple<byte[], byte[]>>(new Error($"Eprom size {size} is not a multiple of {Constants.EpromSize.Eprom256KbSize} bytes."));
            }
        
            var (hiRomBytes, loRomBytes) = Split(kickstartRomBytes);
            var byteSwappedHiRomBytes = ByteSwap(hiRomBytes);
            var byteSwappedLoRomBytes = ByteSwap(loRomBytes);

            if (epromSize < hiRomBytes.Length)
            {
                return new Result<Tuple<byte[], byte[]>>(new Error($"Eprom size {size} doesn't fit Kickstart rom."));
            }

            var epromHiRomBytes = Fill(byteSwappedHiRomBytes, epromSize);
            var epromLoRomBytes = Fill(byteSwappedLoRomBytes, epromSize);
        
            return new Result<Tuple<byte[], byte[]>>(new Tuple<byte[], byte[]>(epromHiRomBytes, epromLoRomBytes));
        }
        
        /// <summary>
        /// Byte swap bytes.
        /// </summary>
        /// <param name="bytes">Bytes to byte swap.</param>
        /// <returns>Byte swapped bytes.</returns>
        private static byte[] ByteSwap(byte[] bytes)
        {
            var byteSwappedRomBytes = new byte[bytes.Length];
            
            for (var i = 0; i < bytes.Length; i += 2)
            {
                (byteSwappedRomBytes[i], byteSwappedRomBytes[i + 1]) = (bytes[i + 1], bytes[i]);
            }

            return byteSwappedRomBytes;
        }

        /// <summary>
        /// Split rom bytes into hi and lo bytes by word size.
        /// </summary>
        /// <param name="romBytes">Rom bytes to split.</param>
        /// <returns>Hi rom bytes from even rom bytes, lo rom bytes from odd rom bytes.</returns>
        /// <exception cref="ArgumentException"></exception>
        public static (byte[], byte[]) Split(byte[] romBytes)
        {
            if (romBytes.Length % WordSize != 0)
            {
                throw new ArgumentException("Invalid rom bytes", nameof(romBytes));
            }

            var splitSize = romBytes.Length / 2;
            var hiRomBytes = new byte[splitSize];
            var loRomBytes = new byte[splitSize];

            var romOffset = 0;
            var hiOffset = 0;
            var loOffset = 0;
            do
            {
                hiRomBytes[hiOffset++] = romBytes[romOffset++];
                hiRomBytes[hiOffset++] = romBytes[romOffset++];
                loRomBytes[loOffset++] = romBytes[romOffset++];
                loRomBytes[loOffset++] = romBytes[romOffset++];
            }
            while (romOffset < romBytes.Length);
            
            return (hiRomBytes, loRomBytes);
        }
        
        /// <summary>
        /// Fill rom bytes in eprom bytes duplicating it until the eprom size is reached.
        /// </summary>
        /// <param name="romBytes"></param>
        /// <param name="epromSize"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static byte[] Fill(byte[] romBytes, int epromSize)
        {
            if (epromSize < romBytes.Length)
            {
                throw new ArgumentException($"Eprom size {epromSize} is smaller than rom size {romBytes.Length}.", nameof(epromSize));
            }

            if (epromSize % romBytes.Length != 0)
            {
                throw new ArgumentException($"Eprom size {epromSize} doesn't fit rom size {romBytes.Length}.", nameof(romBytes));
            }

            var epromBytes = new byte[epromSize];

            for (var fillOffset = 0; fillOffset < epromSize; fillOffset += romBytes.Length)
            {
                Array.Copy(romBytes, 0, epromBytes, fillOffset, romBytes.Length);
            }

            return epromBytes;
        }

        /// <summary>
        /// Merge hi and lo rom bytes into rom bytes by word size.
        /// </summary>
        /// <param name="hiRomBytes">Hi rom bytes to merge.</param>
        /// <param name="loRomBytes">Lo rom bytes to merge.</param>
        /// <returns>Merged rom bytes.</returns>
        /// <exception cref="ArgumentException"></exception>
        public static byte[] Merge(byte[] hiRomBytes, byte[] loRomBytes)
        {
            if (hiRomBytes.Length != loRomBytes.Length)
            {
                throw new ArgumentException("Hi and Lo rom bytes must have the same length.");
            }

            var mergedRomBytes = new byte[hiRomBytes.Length + loRomBytes.Length];
            var offset = 0;

            for (var i = 0; i < hiRomBytes.Length; i += WordSize)
            {
                mergedRomBytes[offset++] = hiRomBytes[i];
                mergedRomBytes[offset++] = hiRomBytes[i + 1];
                mergedRomBytes[offset++] = loRomBytes[i];
                mergedRomBytes[offset++] = loRomBytes[i + 1];
            }

            return mergedRomBytes;
        }
    }
}