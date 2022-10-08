namespace Hst.Amiga.FileSystems
{
    using System.Linq;

    public static class EntryFormatter
    {
        private static readonly ProtectionBits[] ProtectionBitsOrder = new[]
        {
            ProtectionBits.HeldResident, ProtectionBits.Script, ProtectionBits.Pure, ProtectionBits.Archive,
            ProtectionBits.Read, ProtectionBits.Write, ProtectionBits.Executable, ProtectionBits.Delete
        };

        public static string FormatProtectionBits(ProtectionBits protectionBits)
        {
            return new string(ProtectionBitsOrder.Select(x => protectionBits.HasFlag(x) ? x.ToString()[0] : '-')
                .ToArray());
        }
    }
}