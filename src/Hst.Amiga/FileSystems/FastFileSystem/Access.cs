namespace Hst.Amiga.FileSystems.FastFileSystem
{
    public enum Access
    {
        Delete = 1,
        Execute = 2,
        Write = 4,
        Read = 8,
        Archive = 16,
        Pure = 32,
        Script = 64,
        Hold = 128 // requires p bit set
        
        /*The Hold interpretation is the correct one for AmigaOS. In the OFS and FFS file systems,
         if the H,P, E, and R bits are all set then a reentrant command will automatically become 
         resident the first time its executed./**/
    }
}