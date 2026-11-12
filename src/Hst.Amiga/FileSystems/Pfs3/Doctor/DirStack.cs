namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Directory stack (struct dirstack in standardscan.c) used to get name of current object.
    /// </summary>
    public class DirStack
    {
        public DirStack Parent { get; set; }
        public string Name { get; set; }
        public uint AnodeNr { get; set; }
        public string ObjectType { get; set; }
    }
}
