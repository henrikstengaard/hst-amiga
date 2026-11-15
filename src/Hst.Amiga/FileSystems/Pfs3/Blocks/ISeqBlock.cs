namespace Hst.Amiga.FileSystems.Pfs3.Blocks
{
    internal interface ISeqBlock : IBlock
    {
        uint seqnr { get; set; }
    }
}