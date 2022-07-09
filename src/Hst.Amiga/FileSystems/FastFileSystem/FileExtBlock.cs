namespace Hst.Amiga.FileSystems.FastFileSystem
{
    public class FileExtBlock : IHeaderBlock
    {
        // struct bFileExtBlock {
        // 000	int32_t	type;		/* == 0x10 */
        // 004	int32_t	headerKey;
        // 008	int32_t	highSeq;
        // 00c	int32_t	dataSize;	/* == 0 */
        // 010	int32_t	firstData;	/* == 0 */
        // 014	ULONG	checkSum;
        // 018	int32_t	dataBlocks[MAX_DATABLK];
        //             int32_t	r[45];
        //             int32_t	info;		/* == 0 */
        //             int32_t	nextSameHash;	/* == 0 */
        // 1f4	int32_t	parent;		/* header block */
        // 1f8	int32_t	extension;	/* next header extension block */
        // 1fc	int32_t	secType;	/* -3 */	
        // };

        public byte[] BlockBytes { get; set; }

        public int Type { get; }
        public int HeaderKey { get; set; }
        public int HighSeq { get; set; }
        public int IndexSize { get; set; }
        public int FirstData { get; set; }
        public int Checksum { get; set; }
        public int[] Index { get; set; }
        public int RealEntry { get; set; }
        public int NextLink { get; set; }
        public int Info { get; set; }
        public int NextSameHash { get; set; }
        public int Parent { get; set; }
        public int Extension { get; set; }
        public int SecType { get; }

        public FileExtBlock()
        {
            Type = Constants.T_LIST;
            IndexSize = 0;
            FirstData = 0;
            Index = new int[Constants.MAX_DATABLK];
            Info = 0;
            NextSameHash = 0;
            SecType = Constants.ST_FILE;
        }
    }
}