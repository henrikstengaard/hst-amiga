namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.Collections.Generic;
    using Blocks;

    public class volumedata
    {
        // struct volumedata   *next;          /* volumechain                          */
        // struct volumedata   *prev;      

//#if VERSION23
        public CachedBlock rblkextension { get; set; } /* extended rblk, NULL if disabled*/
//#endif

        public LinkedList<IEntry> fileentries { get; set; } /* all locks and open files             */
        // public LinkedList<CachedBlock>[] anblks; //[Constants.HASHM_ANODE+1];   /* anode block hash table           */
        // public LinkedList<CachedBlock>[] dirblks; //[Constants.HASHM_DIR+1];    /* dir block hash table             */
        public IDictionary<uint, CachedBlock> anblks { get; set; }
        public IDictionary<uint, CachedBlock> dirblks { get; set; }
        
        // public LinkedList<CachedBlock> indexblks; /* cached index blocks              */
        /// <summary>
        /// Cached index blocks indexed by block nr
        /// </summary>
        public IDictionary<uint, CachedBlock> indexblks { get; set; }

        /// <summary>
        /// Cached index blocks indexed by seq nr
        /// </summary>
        public IDictionary<uint, CachedBlock> indexblksBySeqNr { get; set; }

        // public LinkedList<CachedBlock> bmblks; /* cached bitmap blocks                 */
        /// <summary>
        /// Cached bitmap blocks indexed by block nr
        /// </summary>
        public IDictionary<uint, CachedBlock> bmblks { get; set; }

        /// <summary>
        /// Cached bitmap blocks indexed by seq nr
        /// </summary>
        public IDictionary<uint, CachedBlock> bmblksBySeqNr { get; set; }

        public IDictionary<uint, CachedBlock> superblks { get; set; } /* cached super blocks					*/
        public IDictionary<uint, CachedBlock> superblksBySeqNr { get; set; } /* cached super blocks					*/
        
        // public LinkedList<CachedBlock> deldirblks { get; set; } /* cached deldirblocks					*/
        public IDictionary<uint, CachedBlock> deldirblks { get; set; } /* cached deldirblocks					*/
        public IDictionary<uint, CachedBlock> deldirblksBySeqNr { get; set; } /* cached deldirblocks					*/

        // public LinkedList<CachedBlock> bmindexblks { get; set; } /* cached bitmap index blocks           */
        public IDictionary<uint, CachedBlock> bmindexblks { get; set; } /* cached bitmap index blocks           */
        public IDictionary<uint, CachedBlock> bmindexblksBySeqNr { get; set; } /* cached bitmap index blocks           */

        public LinkedList<anodechain> anodechainlist { get; set; } /* list of cached anodechains           */
        public LinkedList<string> notifylist { get; set; } /* list of notifications                */

        public bool rootblockchangeflag { get; set; } /* indicates if rootblock dirty         */
        public short numsofterrors { get; set; } /* number of soft errors on this disk   */
        public short diskstate { get; set; } /* normally ID_VALIDATED                */
        public long numblocks { get; set; } /* total number of blocks               */
        public ushort bytesperblock { get; set; } /* blok size (datablocks)               */
        public ushort rescluster { get; set; } /* reserved blocks cluster              */

        public volumedata()
        {
            fileentries = new LinkedList<IEntry>();
            // anblks = new LinkedList<CachedBlock>[Constants.HASHM_ANODE + 1];
            //dirblks = new LinkedList<CachedBlock>[Constants.HASHM_DIR + 1];
            anblks = new Dictionary<uint, CachedBlock>(Constants.HASHM_ANODE + 1);
            dirblks = new Dictionary<uint, CachedBlock>(Constants.HASHM_DIR + 1);

            // indexblks = new LinkedList<CachedBlock>();
            indexblks = new Dictionary<uint, CachedBlock>(100);
            indexblksBySeqNr = new Dictionary<uint, CachedBlock>(100);
            
            // bmblks = new LinkedList<CachedBlock>();
            bmblks = new Dictionary<uint, CachedBlock>(100);
            bmblksBySeqNr = new Dictionary<uint, CachedBlock>(100);
            
            // superblks = new LinkedList<CachedBlock>();
            superblks = new Dictionary<uint, CachedBlock>(100);
            superblksBySeqNr = new Dictionary<uint, CachedBlock>(100);
            
            // deldirblks = new LinkedList<CachedBlock>();
            deldirblks = new Dictionary<uint, CachedBlock>(100);
            deldirblksBySeqNr = new Dictionary<uint, CachedBlock>(100);

            // bmindexblks = new LinkedList<CachedBlock>();
            bmindexblks = new Dictionary<uint, CachedBlock>(100);
            bmindexblksBySeqNr = new Dictionary<uint, CachedBlock>(100);

            anodechainlist = new LinkedList<anodechain>();
            notifylist = new LinkedList<string>();

            // for (var i = 0; i < anblks.Length; i++)
            // {
            //     anblks[i] = new LinkedList<CachedBlock>();
            // }
            
            // for (var i = 0; i < dirblks.Length; i++)
            // {
            //     dirblks[i] = new LinkedList<CachedBlock>();
            // }
        }
    }
}