// namespace Hst.Amiga.FileSystems.Pfs3.Doctor
// {
//     using Blocks;
//
//     public static class Access
//     {
// /* get reserved block
//  * anything, except deldir, root, boot, dir and rext
//  */
// public static Error GetResBlock(CachedBlock blok, ushort bloktype, uint seqnr, bool fix)
// {
// 	CachedBlock blk, t;
// 	uint blknr;//, *bp = NULL, index, offset;
// 	var error = Error.e_none;
//
// 	// controleer build block lijst
// 	t = GetBuildBlock(bloktype, seqnr);
// 	if (t)
// 	{
// 		blok->blocknr = t->blocknr;
// 		//blok->mode = t->mode;
// 		blok->data = *t->data;
// 		return e_none;
// 	}
//
// 	//blk.data = pfscalloc(1, SIZEOF_RESBLOCK);
// 	var index = seqnr / INDEX_PER_BLOCK;
// 	var offset = seqnr % INDEX_PER_BLOCK;
// 	switch(bloktype)
// 	{
// 		case SBLKID:
//
// 			if (seqnr > MAXSUPER)
// 			{
// 				pfsfree (blk.data);
// 				return e_number_error;
// 			}
// 			
// 			bp = &rext.data->superindex[seqnr];
// 			if (!*bp)
// 			{
// 				if (fix)
// 				{
// 					adderror("superindex block not found");
// 					adderror("seq = %lu index = %lu offset = %lu %08lx", seqnr, index, offset, bloktype);
// 					error = RepairSuperIndex(bp, seqnr);
// 					if (error)
// 						*bp = 0;
// 					else
// 					{
// 						volume.writeblock((cachedblock_t *)&rext);
// 						KillAnodeBitmap();
// 					}
// 				}
// 			}
// 			break;
//
// 		case BMIBLKID:
//
// 			bp = &rbl->idx.large.bitmapindex[seqnr];
// 			if (!*bp)
// 			{
// 				if (fix)
// 				{
// 					adderror("bitmapindex block not found");
// 					adderror("seq = %lu index = %lu offset = %lu %08lx", seqnr, index, offset, bloktype);
// 					error = RepairBitmapIndex(bp, seqnr);
// 					if (error)
// 						*bp = 0;
// 					else
// 						c_WriteBlock((uint8 *)rbl, ROOTBLOCK + volume.firstblock, volume.blocksize);
// 				}
// 			}
// 			break;
//
// 		case IBLKID:
//
// 			if (rbl->options & MODE_SUPERINDEX)
// 			{
// 				error = GetResBlock(&blk, SBLKID, index, fix);
// 				if (error)
// 				{
// 					pfsfree (blk.data);
// 					return error;
// 				}
//
// 				bp = &blk.data->indexblock.index[offset];
// 			}
// 			else
// 			{
// 				bp = &rbl->idx.small.indexblocks[seqnr];
// 			}
// 			if (!*bp)
// 			{
// 				if (fix)
// 				{
// 					adderror("anodeindex block not found");
// 					adderror("seq = %lu index = %lu offset = %lu %08lx", seqnr, index, offset, bloktype);
// 					error = RepairAnodeIndex(bp, seqnr);
// 					if (error)
// 						*bp = 0;
// 					else
// 					{
// 						/* the anodebitmap, which is made per aib, 
// 						 * could be too small
// 						 */
// 						KillAnodeBitmap();
// 						if (rbl->options & MODE_SUPERINDEX)
// 							volume.writeblock((cachedblock_t *)&blk);
// 						else
// 							c_WriteBlock((uint8 *)rbl, ROOTBLOCK + volume.firstblock, volume.blocksize);
// 					}
// 				}
// 			}
// 			break;
// 			
// 		case ABLKID:
// 		
// 			error = GetResBlock(&blk, IBLKID, index, fix);
// 			if (error)
// 			{
// 				pfsfree (blk.data);
// 				return error;
// 			}
// 			
// 			bp = &blk.data->indexblock.index[offset];
// 			if (!*bp)
// 			{
// 				if (fix)
// 				{
// 					adderror("anode block not found");
// 					adderror("seq = %lu index = %lu offset = %lu %08lx", seqnr, index, offset, bloktype);
// 					// RepairAnodeBlock already called from RepairAnodeTree
// 					// Pointless to call it again here.
// 					//if (error = RepairAnodeBlock(bp, seqnr))
// 					//	*bp = 0;
// 					//else
// 					//	volume.writeblock((cachedblock_t *)&blk);
// 				}
// 			}
// 			break;
//
// 		case BMBLKID:
//
// 			error = GetResBlock(&blk, BMIBLKID, index, fix);
// 			if (error)
// 			{
// 				pfsfree (blk.data);
// 				return error;
// 			}
//
// 			bp = &blk.data->indexblock.index[offset];
// 			if (!*bp)
// 			{
// 				if (fix)
// 				{
// 					adderror("bitmap block not found");
// 					adderror("seq = %lu index = %lu offset = %lu %08lx", seqnr, index, offset, bloktype);
// 					error = RepairBitmapBlock(bp, seqnr);
// 					if (error)
// 						*bp = 0;
// 					else
// 						volume.writeblock((cachedblock_t *)&blk);
// 				}
// 			}
// 			break;
// 	}
//
// 	blknr = *bp;
//
// 	pfsfree (blk.data);
// 	if (!blknr)
// 		return e_not_found;
//
// 	error = volume.getblock(blok, blknr);
// 	if (error)
// 		return error;
//
// 	return e_none;
// }
//         
//         
//         private static int ANODES_PER_BLOCK(globaldata g)
//         {
//             return g.glob_anodedata.anodesperblock;
//         }        
//         
//         public static CachedBlock tablk = null;
//         //static ULONG tanodedata[MAXRESBLOCKSIZE / 4];
//         public static anodeblock tanodeblk;
//
//         public static bool GetAnode(anode anode, uint anodenr, bool fix, globaldata g)
//         {
//             // anodenr_t *split = (anodenr_t *)&anodenr;
//             var split = Macro.SplitAnodenr(anodenr);
//
//             if (!(tablk != null && tablk.blk != null && tanodeblk.seqnr == split.seqnr))
//             {
//                 tablk.data = tanodeblk;
//                 if (GetResBlock((cachedblock_t *)&tablk, ABLKID, split->seqnr, fix))
//                 {
//                     tablk.data = NULL;
//                     return false;
//                 }
//             }
//
//             if (split.offset > ANODES_PER_BLOCK(g))
//                 return false;
//
//             anode.nr = anodenr;
//             anode.clustersize = tablk.ANodeBlock.nodes[split.offset].clustersize;
//             anode.blocknr     = tablk.ANodeBlock.nodes[split.offset].blocknr;
//             anode.next        = tablk.ANodeBlock.nodes[split.offset].next;
//             return true;
//         }
//     }
// }