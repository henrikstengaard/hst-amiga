public class stats 
{
    public uint blocknr;
    public uint prevblknr;
    public int pass;
    public int blockschecked;
    public int numerrors;
    public int errorsfixed;
    public int	numfiles;
    public int numdirs;
    public int	numsoftlink;
    public int numhardlink;
    public int	numrollover;
    public int fragmentedfiles;
    public int anodesused;
	
    public static void enterblock(stats stats, uint blknr)
    {
        if (blknr != stats.blocknr)
        {
            if (stats.blocknr != 0)
            {
                stats.prevblknr = stats.blocknr;
            }
            stats.blocknr = blknr;
            //error = fixed = FALSE;
            stats.blockschecked++;
            //volume.updatestats();			/* optimise !! */
        }
    }	
	
}