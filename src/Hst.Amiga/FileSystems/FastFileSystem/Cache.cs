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
        public static async Task<IEnumerable<Entry>> ReadEntries(Volume vol, EntryBlock parent, bool recursive = false)
        {
            var list = new List<Entry>();

            var nSect = parent.Extension;

            do
            {
                /* one loop per cache block */
                int offset;
                var n = offset = 0;
                var dirC = await Disk.ReadDirCacheBlock(vol, nSect);
                while (n < dirC.RecordsNb)
                {
                    var cacheEntry = GetCacheEntry(dirC, ref offset);

                    /* converts a cache entry into a dir entry */
                    var entry = new Entry
                    {
                        Type = cacheEntry.Type,
                        Name = cacheEntry.Name,
                        Parent = dirC.Parent
                    };
                    if (entry.Name == null)
                    {
                        return null;
                    }

                    entry.Sector = cacheEntry.Header;
                    entry.Comment = cacheEntry.Comment;
                    if (entry.Comment == null)
                    {
                        return null;
                    }

                    entry.Size = cacheEntry.Size;
                    entry.Access = cacheEntry.Protect;
                    entry.Date = cacheEntry.Date;

                    list.Add(entry);

                    if (recursive && entry.IsDirectory())
                    {
                        var subDirParent = await Disk.ReadEntryBlock(vol, entry.Sector);
                        entry.SubDir = (await ReadEntries(vol, subDirParent, true)).ToList();
                    }

                    n++;
                }

                nSect = dirC.NextDirC;
            } while (nSect != 0);

            return list;
        }

        public static async Task AddInCache(Volume vol, EntryBlock parent, EntryBlock entry)
        {
            DirCacheBlock dirCacheBlock;

            var newCacheEntry = ConvertEntryBlockToCacheEntry(entry);
            var entryLen = newCacheEntry.EntryLen;

            int offset;
            var nSect = parent.Extension;
            do
            {
                dirCacheBlock = await Disk.ReadDirCacheBlock(vol, nSect);
                offset = 0;
                var n = 0;

                while (n < dirCacheBlock.RecordsNb)
                {
                    GetCacheEntry(dirCacheBlock, ref offset);
                    n++;
                }

                nSect = dirCacheBlock.NextDirC;
            } while (nSect != 0);

            /* in the last block */
            if (offset + entryLen <= 488)
            {
                PutCacheEntry(dirCacheBlock, ref offset, newCacheEntry);
                dirCacheBlock.RecordsNb++;
            }
            else
            {
                /* request one new block free */
                var nCache = Bitmap.AdfGet1FreeBlock(vol);
                if (nCache == -1)
                {
                    throw new IOException("nCache==-1");
                }

                if (!(parent.SecType == Constants.ST_ROOT || parent.SecType == Constants.ST_DIR))
                {
                    throw new IOException("Invalid sec type for parent block");
                }
                
                /* create a new dir cache block */
                var newDirCacheBlock = new DirCacheBlock
                {
                    Parent = parent.SecType == Constants.ST_ROOT ? (int)vol.RootBlockOffset : parent.HeaderKey,
                    RecordsNb = 0,
                    NextDirC = 0
                };

                PutCacheEntry(dirCacheBlock, ref offset, newCacheEntry);
                newDirCacheBlock.RecordsNb++;
                await Disk.WriteDirCacheBlock(vol, nCache, newDirCacheBlock);
                dirCacheBlock.NextDirC = nCache;
            }

            await Disk.WriteDirCacheBlock(vol, dirCacheBlock.HeaderKey, dirCacheBlock);
        }

        public static CacheEntry ConvertEntryBlockToCacheEntry(EntryBlock entry)
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
        }

        public static async Task UpdateCache(Volume vol, EntryBlock parent, EntryBlock entry, bool entryLenChg)
        {
            var newCacheEntry = ConvertEntryBlockToCacheEntry(entry);
            var nLen = newCacheEntry.EntryLen;

            var nSect = parent.Extension;
            var found = false;
            do
            {
                var dirCacheBlock = await Disk.ReadDirCacheBlock(vol, nSect);
                var offset = 0;
                var n = 0;
                /* search entry to update with its header_key */
                while (n < dirCacheBlock.RecordsNb && !found)
                {
                    var oldOffset = offset;
                    /* offset is updated */
                    var caEntry = GetCacheEntry(dirCacheBlock, ref offset);
                    var oLen = offset - oldOffset;
                    var sLen = oLen - nLen;

                    found = caEntry.Header == newCacheEntry.Header;
                    if (found)
                    {
                        if (!entryLenChg || oLen == nLen)
                        {
                            /* same length : replace the old values */
                            PutCacheEntry(dirCacheBlock, ref oldOffset, newCacheEntry);
                            await Disk.WriteDirCacheBlock(vol, dirCacheBlock.HeaderKey, dirCacheBlock);
                        }
                        else if (oLen > nLen)
                        {
                            /* the new record is shorter, write it, 
                             * then shift down the following records 
                             */
                            PutCacheEntry(dirCacheBlock, ref oldOffset, newCacheEntry);
                            for (var i = oldOffset + nLen; i < 488 - sLen; i++)
                                dirCacheBlock.Records[i] = dirCacheBlock.Records[i + sLen];
                            /* then clear the following bytes */
                            for (var i = 488 - sLen; i < 488; i++)
                                dirCacheBlock.Records[i] = 0;

                            await Disk.WriteDirCacheBlock(vol, dirCacheBlock.HeaderKey, dirCacheBlock);
                        }
                        else
                        {
                            /* the new record is larger */
                            await DeleteFromCache(vol, parent, entry.HeaderKey);
                            await AddInCache(vol, parent, entry);
                        }
                    }

                    n++;
                }

                nSect = dirCacheBlock.NextDirC;
            } while (nSect != 0 && !found);

            if (found)
            {
                await Bitmap.AdfUpdateBitmap(vol);
            }
            else
            {
                throw new IOException("Entry not found");
            }
        }

        public static async Task CreateEmptyCache(Volume vol, EntryBlock parent, int nSect)
        {
            if (!(parent.SecType == Constants.ST_ROOT || parent.SecType == Constants.ST_DIR))
            {
                throw new IOException("Invalid sec type for new dir cache block");
            }
            
            int nCache;

            if (nSect == -1)
            {
                nCache = Bitmap.AdfGet1FreeBlock(vol);
                if (nCache == -1)
                {
                    throw new IOException("nCache==-1");
                }
            }
            else
            {
                nCache = nSect;
            }

            if (parent.Extension == 0)
            {
                parent.Extension = nCache;
            }

            var dirCacheBlock = new DirCacheBlock
            {
                Parent = parent.SecType == Constants.ST_ROOT ? (int)vol.RootBlockOffset :parent.HeaderKey,
                RecordsNb = 0,
                NextDirC = 0
            };

            await Disk.WriteDirCacheBlock(vol, nCache, dirCacheBlock);
        }

        /// <summary>
        /// Get cache entry from dir cache block at offset ptr (index into records[]). Offset is updated, then entry is returned
        /// </summary>
        /// <param name="dirCacheBlock"></param>
        /// <param name="ptr"></param>
        /// <returns></returns>
        public static CacheEntry GetCacheEntry(DirCacheBlock dirCacheBlock, ref int ptr)
        {
            var cacheEntry = new CacheEntry
            {
                Header = BigEndianConverter.ConvertBytesToInt32(dirCacheBlock.Records, ptr),
                Size = BigEndianConverter.ConvertBytesToInt32(dirCacheBlock.Records, ptr + 4),
                Protect = BigEndianConverter.ConvertBytesToInt32(dirCacheBlock.Records, ptr + 8)
            };
            var days = BigEndianConverter.ConvertBytesToInt16(dirCacheBlock.Records, ptr + 16);
            var minutes = BigEndianConverter.ConvertBytesToInt16(dirCacheBlock.Records, ptr + 18);
            var ticks = BigEndianConverter.ConvertBytesToInt16(dirCacheBlock.Records, ptr + 20);
            cacheEntry.Date = DateHelper.ConvertToDate(days, minutes, ticks);
            cacheEntry.Type = SignedByteConverter.ConvertByteToSignedByte(dirCacheBlock.Records, ptr + 22);

            var nLen = dirCacheBlock.Records[ptr + 23];
            cacheEntry.Name = AmigaTextHelper.GetString(dirCacheBlock.Records, ptr + 24, nLen);
            var cLen = dirCacheBlock.Records[ptr + 24 + nLen];
            cacheEntry.Comment = AmigaTextHelper.GetString(dirCacheBlock.Records, ptr + 24 + nLen + 1, cLen);

            var p = ptr + 24 + nLen + 1 + cLen;
            if (p % 2 != 0)
            {
                p += 1;
            }

            ptr = p;
            return cacheEntry;
        }

        public static int PutCacheEntry(DirCacheBlock dirCacheBlock, ref int ptr, CacheEntry cacheEntry)
        {
            BigEndianConverter.ConvertInt32ToBytes(cacheEntry.Header, dirCacheBlock.Records, ptr);
            BigEndianConverter.ConvertInt32ToBytes(cacheEntry.Size, dirCacheBlock.Records, ptr + 4);
            BigEndianConverter.ConvertInt32ToBytes(cacheEntry.Protect, dirCacheBlock.Records, ptr + 8);
            var amigaDate = DateHelper.ConvertToAmigaDate(cacheEntry.Date);
            BigEndianConverter.ConvertInt16ToBytes((short)amigaDate.Days, dirCacheBlock.Records, ptr + 16);
            BigEndianConverter.ConvertInt16ToBytes((short)amigaDate.Minutes, dirCacheBlock.Records, ptr + 18);
            BigEndianConverter.ConvertInt16ToBytes((short)amigaDate.Ticks, dirCacheBlock.Records, ptr + 20);

            SignedByteConverter.ConvertSignedByteToByte(dirCacheBlock.Records, ptr + 22, (sbyte)cacheEntry.Type);

            var nameBytes = AmigaTextHelper.GetBytes(cacheEntry.Name);
            dirCacheBlock.Records[ptr + 23] = (byte)nameBytes.Length;
            Array.Copy(nameBytes, 0, dirCacheBlock.Records, ptr + 24, nameBytes.Length);

            var commentBytes = AmigaTextHelper.GetBytes(cacheEntry.Comment);
            dirCacheBlock.Records[ptr + 24 + nameBytes.Length] = (byte)commentBytes.Length;
            Array.Copy(commentBytes, 0, dirCacheBlock.Records, ptr + 25 + nameBytes.Length, commentBytes.Length);

            var l = 25 + nameBytes.Length + commentBytes.Length;
            if (l % 2 == 0)
            {
                return l;
            }
            dirCacheBlock.Records[ptr + l] = 0;
            return l + 1;
        }

        /// <summary>
        /// Delete cache entry from block (doesn't handle garbage collection) 
        /// </summary>
        /// <param name="vol"></param>
        /// <param name="parent"></param>
        /// <param name="headerKey"></param>
        /// <exception cref="IOException"></exception>
        public static async Task DeleteFromCache(Volume vol, EntryBlock parent, int headerKey)
        {
            var prevSect = -1;
            var nSect = parent.Extension;
            var found = false;
            do
            {
                var dirCacheBlock = await Disk.ReadDirCacheBlock(vol, nSect);
                var offset = 0;
                var n = 0;
                while (n < dirCacheBlock.RecordsNb && !found)
                {
                    var oldOffset = offset;
                    var caEntry = GetCacheEntry(dirCacheBlock, ref offset);
                    found = caEntry.Header == headerKey;
                    if (found)
                    {
                        var entryLen = offset - oldOffset;
                        if (dirCacheBlock.RecordsNb > 1 || prevSect == -1)
                        {
                            if (n < dirCacheBlock.RecordsNb - 1)
                            {
                                /* not the last of the block : switch the following records */
                                for (var i = oldOffset; i < (488 - entryLen); i++)
                                    dirCacheBlock.Records[i] = dirCacheBlock.Records[i + entryLen];
                                /* and clear the following bytes */
                                for (var i = 488 - entryLen; i < 488; i++)
                                    dirCacheBlock.Records[i] = 0;
                            }
                            else
                            {
                                /* the last record of this cache block */
                                for (var i = oldOffset; i < offset; i++)
                                    dirCacheBlock.Records[i] = 0;
                            }

                            dirCacheBlock.RecordsNb--;
                            await Disk.WriteDirCacheBlock(vol, dirCacheBlock.HeaderKey, dirCacheBlock);
                        }
                        else
                        {
                            /* dirCacheBlock.recordsNb ==1 or == 0 , prevSect!=-1 : 
                            * the only record in this dir cache block and a previous dir cache block exists 
                            */
                            Bitmap.AdfSetBlockFree(vol, dirCacheBlock.HeaderKey);
                            dirCacheBlock = await Disk.ReadDirCacheBlock(vol, prevSect);
                            dirCacheBlock.NextDirC = 0;
                            await Disk.WriteDirCacheBlock(vol, prevSect, dirCacheBlock);

                            await Bitmap.AdfUpdateBitmap(vol);
                        }
                    }

                    n++;
                }

                prevSect = nSect;
                nSect = dirCacheBlock.NextDirC;
            } while (nSect != 0 && !found);

            if (!found)
            {
                throw new IOException("entry not found");
            }
        }
    }
}