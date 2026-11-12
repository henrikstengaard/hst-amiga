namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Scan element (scanelement_t in doctor.h).
    /// </summary>
    public class ScanElement
    {
        public uint BlockNr { get; set; }
        public uint Datestamp { get; set; }
    }
}
