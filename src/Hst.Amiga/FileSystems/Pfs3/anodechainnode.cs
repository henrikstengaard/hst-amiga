namespace Hst.Amiga.FileSystems.Pfs3
{
    internal class anodechainnode
    {
        // struct anodechainnode
        // {
        //     struct anodechainnode *next;
        //     struct canode an;
        // };
        
        public anodechainnode next;
        public canode an;

        public anodechainnode()
        {
            an = new canode();
        }
    }
}