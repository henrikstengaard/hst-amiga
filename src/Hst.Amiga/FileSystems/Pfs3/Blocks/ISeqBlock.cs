namespace Hst.Amiga.FileSystems.Pfs3.Blocks
{
    public interface ISeqBlock : IBlock
    {
        uint seqnr { get; set; }
    }
}