namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Converters;
    using Extensions;

    public static class Cache
    {
        public static int ConvertByteToSignedByte(byte[] bytes, int offset)
        {
            var value = bytes[offset];
            return value >= 128 ? value - 256 : value;
        }

        public static void ConvertSignedByteToByte(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)(value < 0 ? value + 256 : value);
        }
        
        public static async Task<IEnumerable<Entry>> AdfGetDirEntCache(Volume vol, EntryBlock parent, bool recursive = false)
        {
            // struct bEntryBlock parent;
            // struct bDirCacheBlock dirc;
            int offset, n;
            //    struct List *cell, *head;
            //    struct CacheEntry caEntry;
            //    struct Entry *entry;
            //    SECTNUM nSect;

            //var parent = await Disk.AdfReadEntryBlock(vol, dir);
            //     return NULL;
            var list = new List<Entry>();

            var nSect = parent.Extension;

            //cell = head = NULL;
            do
            {
                /* one loop per cache block */
                n = offset = 0;
                var dirC = await ReadDirCacheBlock(vol, nSect);
                while (n < dirC.RecordsNb)
                {
                    var caEntry = AdfGetCacheEntry(dirC, ref offset);

                    /* converts a cache entry into a dir entry */
                    var entry = new Entry
                    {
                        Type = caEntry.Type,
                        Name = caEntry.Name
                    };
                    entry.Parent = dirC.Parent;
                    if (entry.Name == null)
                    {
                        return null;
                    }

                    entry.Sector = caEntry.Header;
                    entry.Comment = caEntry.Comment;
                    if (entry.Comment == null)
                    {
                        return null;
                    }

                    entry.Size = caEntry.Size;
                    entry.Access = caEntry.Protect;
                    entry.Date = caEntry.Date;

                    list.Add(entry);

                    if (recursive && entry.IsDirectory())
                    {
                        var subDirParent = await Disk.ReadEntryBlock(vol, entry.Sector);
                        entry.SubDir = (await AdfGetDirEntCache(vol, subDirParent, true)).ToList();
                    }

                    n++;
                }

                nSect = dirC.NextDirC;
            } while (nSect != 0);

            return list;
        }

        public static async Task AdfAddInCache(Volume vol, EntryBlock parent, EntryBlock entry)
        {
//             // https://github.com/lclevy/ADFlib/blob/be8a6f6e8d0ca8fda963803eef77366c7584649a/src/adf_cache.c#L354

            DirCacheBlock dirc, newDirc;
            CacheEntry caEntry;
            // struct bDirCacheBlock dirc, newDirc;
            // SECTNUM nSect, nCache;
            // struct CacheEntry caEntry, newEntry;
            // int offset, n;
            // int entryLen;

            var newEntry = AdfEntry2CacheEntry(entry);
            var entryLen = newEntry.EntryLen;
/*printf("adfAddInCache--%4ld %2d %6ld %8lx %4d %2d:%02d:%02d %30s %22s\n",
    newEntry.header, newEntry.type, newEntry.size, newEntry.protect,
    newEntry.days, newEntry.mins/60, newEntry.mins%60, 
	newEntry.ticks/50,
	newEntry.name, newEntry.comm);
*/
            var offset = 0;
            var n = 0;
            var nCache = 0;
            var nSect = parent.Extension;
            do
            {
                dirc = await ReadDirCacheBlock(vol, nSect);
                offset = 0;
                n = 0;
/*printf("parent=%4ld\n",dirc.parent);*/
                while (n < dirc.RecordsNb)
                {
                    caEntry = AdfGetCacheEntry(dirc, ref offset);
/*printf("*%4ld %2d %6ld %8lx %4d %2d:%02d:%02d %30s %22s\n",
    caEntry.header, caEntry.type, caEntry.size, caEntry.protect,
    caEntry.days, caEntry.mins/60, caEntry.mins%60, 
	caEntry.ticks/50,
	caEntry.name, caEntry.comm);
*/
                    n++;
                }

/*        if (offset+entryLen<=488) {
            adfPutCacheEntry(&dirc, &offset, &newEntry);
            dirc.recordsNb++;
            adfWriteDirCBlock(vol, dirc.headerKey, &dirc);
            return rc;
        }*/
                nSect = dirc.NextDirC;
            } while (nSect != 0);

            /* in the last block */
            if (offset + entryLen <= 488)
            {
                AdfPutCacheEntry(dirc, ref offset, newEntry);
                dirc.RecordsNb++;
/*printf("entry name=%s\n",newEntry.name);*/
            }
            else
            {
                /* request one new block free */
                nCache = Bitmap.AdfGet1FreeBlock(vol);
                if (nCache == -1)
                {
                    throw new IOException("adfCreateDir : nCache==-1");
                }

                newDirc = new DirCacheBlock();
                /* create a new dircache block */
                //memset(&newDirc,0,512);
                if (parent.SecType == Constants.ST_ROOT)
                    newDirc.Parent = vol.RootBlock.HeaderKey;
                else if (parent.SecType == Constants.ST_DIR)
                    newDirc.Parent = parent.HeaderKey;
                else
                    throw new IOException("adfAddInCache : unknown secType");
                newDirc.RecordsNb = 0;
                newDirc.NextDirC = 0;

                AdfPutCacheEntry(dirc, ref offset, newEntry);
                newDirc.RecordsNb++;
                await WriteDirCacheBlock(vol, nCache, newDirc);
                dirc.NextDirC = nCache;
            }

/*printf("dirc.headerKey=%ld\n",dirc.headerKey);*/
            await WriteDirCacheBlock(vol, dirc.HeaderKey, dirc);
/*if (strcmp(entry->name,"file_5u")==0)
dumpBlock(&dirc);
*/
        }

/*
 * adfEntry2CacheEntry
 *
 * converts one dir entry into a cache entry, and return its future length in records[]
 */
        public static CacheEntry AdfEntry2CacheEntry(EntryBlock entry)
        {
            return new CacheEntry
            {
                Header = entry.HeaderKey,
                Size = entry.SecType == Constants.ST_FILE ? entry.ByteSize : 0,
                Protect = entry.Access,
                Date = entry.Date,
                Type = entry.SecType,
                Name = entry.Name,
                Comment = entry.Comment
            };

            /* new entry */
            // newEntry->header = entry->headerKey;
            // if (entry->secType==ST_FILE)
            //     newEntry->size = entry->byteSize;
            // else
            //     newEntry->size = 0L;
            // newEntry->protect = entry->access;
            // newEntry->days = (short)entry->days;
            // newEntry->mins = (short)entry->mins;
            // newEntry->ticks  = (short)entry->ticks;
            // newEntry->type = (signed char)entry->secType;
            // newEntry->nLen = entry->nameLen;
            // memcpy(newEntry->name, entry->name, newEntry->nLen);
            // newEntry->name[(int)(newEntry->nLen)] = '\0';
            // newEntry->cLen = entry->commLen;
            // if (newEntry->cLen>0)
            //     memcpy(newEntry->comm, entry->comment, newEntry->cLen);
            //
            // entryLen = 24+newEntry->nLen+1+newEntry->cLen;

/*printf("entry->name %d entry->comment %d\n",entry->nameLen,entry->commLen);
printf("newEntry->nLen %d newEntry->cLen %d\n",newEntry->nLen,newEntry->cLen);
*/
            // if ((entryLen%2)==0)
            //     return entryLen;
            // else
            //     return entryLen+1;
        }

        /*
 * adfUpdateCache
 *
 */
        public static async Task AdfUpdateCache(Volume vol, EntryBlock parent, EntryBlock entry, bool entryLenChg)
        {
            // https://github.com/lclevy/ADFlib/blob/be8a6f6e8d0ca8fda963803eef77366c7584649a/src/adf_cache.c#L441

            // struct bDirCacheBlock dirc;
            // SECTNUM nSect;
            CacheEntry caEntry;
            int offset, oldOffset, n;
            // BOOL found;
            // int i, oLen, nLen;
            // int sLen; /* shift length */
            DirCacheBlock dirc;

            var newEntry = AdfEntry2CacheEntry(entry);
            var nLen = newEntry.EntryLen;

            var nSect = parent.Extension;
            var found = false;
            do
            {
/*printf("dirc=%ld\n",nSect);*/
                dirc = await ReadDirCacheBlock(vol, nSect);
                offset = 0;
                n = 0;
                /* search entry to update with its header_key */
                while (n < dirc.RecordsNb && !found)
                {
                    oldOffset = offset;
                    /* offset is updated */
                    caEntry = AdfGetCacheEntry(dirc, ref offset);
                    var oLen = offset - oldOffset;
                    var sLen = oLen - nLen;
/*printf("olen=%d nlen=%d\n",oLen,nLen);*/
                    found = caEntry.Header == newEntry.Header;
                    if (found)
                    {
                        if (!entryLenChg || oLen == nLen)
                        {
                            /* same length : remplace the old values */
                            AdfPutCacheEntry(dirc, ref oldOffset, newEntry);
/*if (entryLenChg) puts("oLen==nLen");*/
                            await WriteDirCacheBlock(vol, dirc.HeaderKey, dirc);
                        }
                        else if (oLen > nLen)
                        {
/*puts("oLen>nLen");*/
                            /* the new record is shorter, write it, 
                             * then shift down the following records 
                             */
                            AdfPutCacheEntry(dirc, ref oldOffset, newEntry);
                            for (var i = oldOffset + nLen; i < 488 - sLen; i++)
                                dirc.Records[i] = dirc.Records[i + sLen];
                            /* then clear the following bytes */
                            for (var i = 488 - sLen; i < 488; i++)
                                dirc.Records[i] = 0;

                            await WriteDirCacheBlock(vol, dirc.HeaderKey, dirc);
                        }
                        else
                        {
                            /* the new record is larger */
/*puts("oLen<nLen");*/
                            await AdfDelFromCache(vol, parent, entry.HeaderKey);
                            await AdfAddInCache(vol, parent, entry);
/*puts("oLen<nLen end");*/
                        }
                    }

                    n++;
                }

                nSect = dirc.NextDirC;
            } while (nSect != 0 && !found);

            if (found)
            {
                await Bitmap.AdfUpdateBitmap(vol);
            }
            else
                throw new IOException("adfUpdateCache : entry not found");
        }

/*
 * adfCreateEmptyCache
 *
 */
        public static async Task AdfCreateEmptyCache(Volume vol, EntryBlock parent, int nSect)
        {
            // struct bDirCacheBlock dirc;
            int nCache;

            if (nSect == -1)
            {
                nCache = Bitmap.AdfGet1FreeBlock(vol);
                if (nCache == -1)
                {
                    throw new IOException("adfCreateDir : nCache==-1");
                }
            }
            else
                nCache = nSect;

            if (parent.Extension == 0)
                parent.Extension = nCache;

            var dirc = new DirCacheBlock();
            //memset(&dirc,0, sizeof(struct bDirCacheBlock));

            if (parent.SecType == Constants.ST_ROOT)
                dirc.Parent = vol.RootBlock.HeaderKey;
            else if (parent.SecType == Constants.ST_DIR)
                dirc.Parent = parent.HeaderKey;
            else
            {
                throw new IOException("adfCreateEmptyCache : unknown secType");
/*printf("secType=%ld\n",parent->secType);*/
            }

            dirc.RecordsNb = 0;
            dirc.NextDirC = 0;

            await WriteDirCacheBlock(vol, nCache, dirc);
        }


        public static async Task<DirCacheBlock> ReadDirCacheBlock(Volume vol, int nSect)
        {
            var blockBytes = await Disk.ReadBlock(vol, nSect);

            var dirCacheBlock = DirCacheBlockParser.Parse(blockBytes);
            if (dirCacheBlock.HeaderKey != nSect)
            {
                throw new IOException($"Invalid dir cache block header key '{dirCacheBlock.HeaderKey}' is not equal to sector {nSect}");
            }

            return dirCacheBlock;
        }

        public static async Task WriteDirCacheBlock(Volume vol, int nSect, DirCacheBlock dirCacheBlock)
        {
            dirCacheBlock.HeaderKey = nSect;

            var blockBytes = DirCacheBlockBuilder.Build(dirCacheBlock, vol.BlockSize);
            await Disk.WriteBlock(vol, nSect, blockBytes);
        }

/*
 * adfGetCacheEntry
 *
 * Returns a cache entry, starting from the offset p (the index into records[])
 * This offset is updated to the end of the returned entry.
 */
        public static CacheEntry AdfGetCacheEntry(DirCacheBlock dirc, ref int ptr)
        {
            var cEntry = new CacheEntry
            {
                Header = BigEndianConverter.ConvertBytesToInt32(dirc.Records, ptr),
                Size = BigEndianConverter.ConvertBytesToInt32(dirc.Records, ptr + 4),
                Protect = BigEndianConverter.ConvertBytesToInt32(dirc.Records, ptr + 8)
            };
            var days = BigEndianConverter.ConvertBytesToInt16(dirc.Records, ptr + 16);
            var minutes = BigEndianConverter.ConvertBytesToInt16(dirc.Records, ptr + 18);
            var ticks = BigEndianConverter.ConvertBytesToInt16(dirc.Records, ptr + 20);
            cEntry.Date = DateHelper.ConvertToDate(days, minutes, ticks);
            cEntry.Type = ConvertByteToSignedByte(dirc.Records, ptr + 22);

            var nLen = dirc.Records[ptr + 23];
            cEntry.Name = AmigaTextHelper.GetString(dirc.Records, ptr + 24, nLen);
            var cLen = dirc.Records[ptr + 24 + nLen];
            cEntry.Comment = AmigaTextHelper.GetString(dirc.Records, ptr + 24 + nLen + 1, cLen);

            var p = ptr + 24 + nLen + 1 + cLen;

            if (p % 2 != 0)
                p = p + 1;

            ptr = p;
// // #endif
//             cEntry->type =(signed char) dirc->records[ptr+22];
//
//             cEntry->nLen = dirc->records[ptr+23];
// /*    cEntry->name = (char*)malloc(sizeof(char)*(cEntry->nLen+1));
//     if (!cEntry->name)
//          return;
// */    memcpy(cEntry->name, dirc->records+ptr+24, cEntry->nLen);
//             cEntry->name[(int)(cEntry->nLen)]='\0';
//
//             cEntry->cLen = dirc->records[ptr+24+cEntry->nLen];
//             if (cEntry->cLen>0) {
// /*        cEntry->comm =(char*)malloc(sizeof(char)*(cEntry->cLen+1));
//         if (!cEntry->comm) {
//             free( cEntry->name ); cEntry->name=NULL;
//             return;
//         }
// */        memcpy(cEntry->comm,dirc->records+ptr+24+cEntry->nLen+1,cEntry->cLen);
//             }
//             cEntry->comm[(int)(cEntry->cLen)]='\0';
// /*printf("cEntry->nLen %d cEntry->cLen %d %s\n",cEntry->nLen,cEntry->cLen,cEntry->name);*/
//             *p  = ptr+24+cEntry->nLen+1+cEntry->cLen;
//
//             /* the starting offset of each record must be even (68000 constraint) */ 
//             if ((*p%2)!=0)
//                 *p=(*p)+1;
            return cEntry;
        }

        /*
 * adfPutCacheEntry
 *
 * remplaces one cache entry at the p offset, and returns its length
 */
        public static int AdfPutCacheEntry(DirCacheBlock dirc, ref int ptr, CacheEntry cEntry)
        {
//             int ptr, l;
//             ptr = *p;
//
// #ifdef LITT_ENDIAN
//             swLong(dirc->records+ptr, cEntry->header);
//             swLong(dirc->records+ptr+4, cEntry->size);
//             swLong(dirc->records+ptr+8, cEntry->protect);
//             swShort(dirc->records+ptr+16, cEntry->days);
//             swShort(dirc->records+ptr+18, cEntry->mins);
//             swShort(dirc->records+ptr+20, cEntry->ticks);
// #else
            BigEndianConverter.ConvertInt32ToBytes(cEntry.Header, dirc.Records, ptr);
            BigEndianConverter.ConvertInt32ToBytes(cEntry.Size, dirc.Records, ptr + 4);
            BigEndianConverter.ConvertInt32ToBytes(cEntry.Protect, dirc.Records, ptr + 8);
            var amigaDate = DateHelper.ConvertToAmigaDate(cEntry.Date);
            BigEndianConverter.ConvertInt16ToBytes((short)amigaDate.Days, dirc.Records, ptr + 16);
            BigEndianConverter.ConvertInt16ToBytes((short)amigaDate.Minutes, dirc.Records, ptr + 18);
            BigEndianConverter.ConvertInt16ToBytes((short)amigaDate.Ticks, dirc.Records, ptr + 20);

            ConvertSignedByteToByte(dirc.Records, ptr + 22, (sbyte)cEntry.Type);

            var nameBytes = AmigaTextHelper.GetBytes(cEntry.Name);
            dirc.Records[ptr + 23] = (byte)nameBytes.Length;
            Array.Copy(nameBytes, 0, dirc.Records, ptr + 24, nameBytes.Length);

            var commentBytes = AmigaTextHelper.GetBytes(cEntry.Comment);
            dirc.Records[ptr + 24 + nameBytes.Length] = (byte)commentBytes.Length;
            Array.Copy(commentBytes, 0, dirc.Records, ptr + 25 + nameBytes.Length, commentBytes.Length);

            var l = 25 + nameBytes.Length + commentBytes.Length;
            if (l % 2 == 0)
                return l;
            else
            {
                dirc.Records[ptr + l] = 0;
                return l + 1;
            }

            /* ptr%2 must be == 0, if l%2==0, (ptr+l)%2==0 */
        }

/*
 * adfDelFromCache
 *
 * delete one cache entry from its block. don't do 'records garbage collecting'
 */
        public static async Task AdfDelFromCache(Volume vol, EntryBlock parent, int headerKey)
        {
            // struct bDirCacheBlock dirc;
            // SECTNUM nSect, prevSect;
            // struct CacheEntry caEntry;
            int offset, oldOffset, n;
            // BOOL found;
            // int entryLen;
            // int i;
            // RETCODE rc = RC_OK;

            var prevSect = -1;
            var nSect = parent.Extension;
            var found = false;
            do
            {
                var dirc = await ReadDirCacheBlock(vol, nSect);
                offset = 0;
                n = 0;
                while (n < dirc.RecordsNb && !found)
                {
                    oldOffset = offset;
                    var caEntry = AdfGetCacheEntry(dirc, ref offset);
                    found = caEntry.Header == headerKey;
                    if (found)
                    {
                        var entryLen = offset - oldOffset;
                        if (dirc.RecordsNb > 1 || prevSect == -1)
                        {
                            if (n < dirc.RecordsNb - 1)
                            {
                                /* not the last of the block : switch the following records */
                                for (var i = oldOffset; i < (488 - entryLen); i++)
                                    dirc.Records[i] = dirc.Records[i + entryLen];
                                /* and clear the following bytes */
                                for (var i = 488 - entryLen; i < 488; i++)
                                    dirc.Records[i] = 0;
                            }
                            else
                            {
                                /* the last record of this cache block */
                                for (var i = oldOffset; i < offset; i++)
                                    dirc.Records[i] = 0;
                            }

                            dirc.RecordsNb--;
                            await WriteDirCacheBlock(vol, dirc.HeaderKey, dirc);
                        }
                        else
                        {
                            /* dirc.recordsNb ==1 or == 0 , prevSect!=-1 : 
                            * the only record in this dirc block and a previous dirc block exists 
                            */
                            Bitmap.AdfSetBlockFree(vol, dirc.HeaderKey);
                            dirc = await ReadDirCacheBlock(vol, prevSect);
                            dirc.NextDirC = 0;
                            await WriteDirCacheBlock(vol, prevSect, dirc);

                            await Bitmap.AdfUpdateBitmap(vol);
                        }
                    }

                    n++;
                }

                prevSect = nSect;
                nSect = dirc.NextDirC;
            } while (nSect != 0 && !found);

            if (!found)
                throw new IOException("adfUpdateCache : entry not found");
        }
    }
}