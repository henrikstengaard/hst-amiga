namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;
    using Exceptions;
    using Extensions;

    public static class Directory
    {
        public static async Task<IEnumerable<Entry>> ReadEntries(Volume volume, uint nSect, bool recursive = false)
        {
            if (volume.UseDirCache)
            {
                return await Cache.ReadEntries(volume, nSect, recursive);
            }

            var startEntryBlock = await Disk.ReadEntryBlock(volume, nSect);

            var hashTable = startEntryBlock.HashTable.ToList();
            var entries = new List<Entry>();

            for (var i = 0; i < hashTable.Count; i++)
            {
                if (hashTable[i] == 0)
                {
                    continue;
                }

                if (hashTable[i] < volume.FirstBlock || hashTable[i] > volume.LastBlock)
                {
                    continue;
                }

                var entryBlock = await Disk.ReadEntryBlock(volume, hashTable[i]);

                // convert entry block to entry and add to list
                var entry = ConvertEntryBlockToEntry(entryBlock);
                entry.Sector = hashTable[i];
                entries.Add(entry);

                if (recursive && entry.IsDirectory())
                {
                    entry.SubDir = (await ReadEntries(volume,
                        FastFileSystemHelper.GetSector(volume, entryBlock), true)).ToList();
                }

                //         /* same hashcode linked list */
                //         nextSector = entryBlk.nextSameHash;
                //         while( nextSector!=0 ) {
                var nextSector = entryBlock.NextSameHash;
                while (nextSector != 0)
                {
                    entryBlock = await Disk.ReadEntryBlock(volume, nextSector);

                    // convert entry block to entry and add to list
                    entry = ConvertEntryBlockToEntry(entryBlock);
                    entry.Sector = nextSector;
                    entries.Add(entry);

                    if (recursive && entry.IsDirectory())
                    {
                        entry.SubDir = (await ReadEntries(volume,
                            FastFileSystemHelper.GetSector(volume, entryBlock), true)).ToList();
                    }

                    nextSector = entryBlock.NextSameHash;
                }
            }

            return entries;
        }

        /// <summary>
        /// Convert entry block to entry
        /// </summary>
        /// <param name="entryBlock"></param>
        /// <returns></returns>
        /// <exception cref="IOException"></exception>
        public static Entry ConvertEntryBlockToEntry(EntryBlock entryBlock)
        {
            var entry = new Entry
            {
                Type = entryBlock.SecType,
                Parent = entryBlock.Parent,
                Name = entryBlock.Name,
                Comment = string.Empty,
                Date = entryBlock.Date,
                Access = uint.MaxValue, //-1
                Size = 0,
                Real = 0,
                EntryBlock = entryBlock
            };

            switch (entryBlock.SecType)
            {
                case Constants.ST_DIR:
                    entry.Access = entryBlock.Access;
                    entry.Comment = entryBlock.Comment;
                    break;
                case Constants.ST_FILE:
                    entry.Access = entryBlock.Access;
                    entry.Size = entryBlock.ByteSize;
                    entry.Comment = entryBlock.Comment;
                    break;
                case Constants.ST_LFILE:
                    entry.Real = entryBlock.RealEntry;
                    break;
                case Constants.ST_LDIR:
                    entry.Real = entryBlock.RealEntry;
                    break;
                case Constants.ST_LSOFT:
                    break;
            }

            return entry;
        }

        public static char ToUpper(char c)
        {
            return (char)(c >= 'a' && c <= 'z' ? c - ('a' - 'A') : c);
        }

        public static char IntlToUpper(char c)
        {
            return (char)((c >= 'a' && c <= 'z') || (c >= 224 && c <= 254 && c != 247) ? c - ('a' - 'A') : c);
        }

        public static int GetHashValue(int hashTableSize, string name, bool intl)
        {
            var hash = (uint)name.Length;
            foreach (var c in name)
            {
                var upper = intl ? IntlToUpper(c) : ToUpper(c);
                hash = (hash * 13 + upper) & 0x7ff;
            }

            hash %= (uint)hashTableSize;
            return (int)hash;
        }

        public static string MyToUpper(string str, bool intl)
        {
            var nstr = str.ToCharArray();
            for (var i = 0; i < str.Length; i++)
            {
                nstr[i] = intl ? IntlToUpper(str[i]) : ToUpper(str[i]);
            }

            return new string(nstr);
        }

        public static async Task<NameToEntryBlockResult> GetEntryBlock(Volume volume, uint[] ht, string name,
            bool nUpdSect)
        {
            var intl = volume.UseIntl || volume.UseDirCache;
            var hashVal = GetHashValue(ht.Length, name, intl);
            var nameLen = Math.Min(name.Length, volume.UseLnfs ? Constants.LNFSMAXNAMELEN : Constants.MAXNAMELEN);
            var upperName = MyToUpper(name, intl);

            var nSect = ht[hashVal];
            if (nSect == 0)
                return new NameToEntryBlockResult
                {
                    NSect = uint.MaxValue //-1
                };

            EntryBlock entry;
            var updSect = 0U;
            var found = false;
            do
            {
                entry = await Disk.ReadEntryBlock(volume, nSect);
                if (entry == null)
                {
                    return new NameToEntryBlockResult
                    {
                        NSect = uint.MaxValue //-1
                    };
                }

                if (nameLen == entry.Name.Length)
                {
                    var upperName2 = MyToUpper(entry.Name, intl);
                    found = upperName == upperName2;
                }

                if (!found)
                {
                    updSect = nSect;
                    nSect = entry.NextSameHash;
                }
            } while (!found && nSect != 0);

            if (nSect == 0 && !found)
                return new NameToEntryBlockResult
                {
                    NSect = uint.MaxValue, // -1
                };

            return new NameToEntryBlockResult
            {
                NSect = nSect,
                EntryBlock = entry,
                NUpdSect = nUpdSect ? new uint?(updSect) : null
            };
        }

        public static async Task<EntryBlock> CreateFile(Volume vol, EntryBlock parent, string name)
        {
            /* -1 : do not use a specific, already allocated sector */
            var nSect = await CreateEntry(vol, parent, name, uint.MaxValue);
            if (nSect == uint.MaxValue)
            {
                throw new DiskFullException("No sector available");
            }

            if (!(parent.SecType == Constants.ST_ROOT || parent.SecType == Constants.ST_DIR))
            {
                throw new FileSystemException($"Invalid secondary type '{parent.SecType}'");
            }

            var entryBlock = new FileHeaderBlock(vol.FileSystemBlockSize)
            {
                HeaderKey = nSect,
                Name = name,
                Parent = parent.SecType == Constants.ST_ROOT ? vol.RootBlockOffset : parent.HeaderKey,
                Date = DateTime.Now
            };

            await Disk.WriteFileHdrBlock(vol, nSect, entryBlock);

            if (vol.UseDirCache)
            {
                await Cache.AddInCache(vol, parent, entryBlock);
            }

            await Bitmap.AdfUpdateBitmap(vol);

            return entryBlock;
        }

        /// <summary>
        /// Create entry in entry block
        /// </summary>
        /// <param name="vol">Volume</param>
        /// <param name="dir">Entry block to insert entry into it's hashTable</param>
        /// <param name="name">Name of entry</param>
        /// <param name="thisSect">insert this sector pointer into the hashTable (here 'thisSect' must be allocated before in the bitmap). if 'thisSect'==-1, allocate a sector</param>
        /// <returns></returns>
        /// <exception cref="IOException"></exception>
        public static async Task<uint> CreateEntry(Volume vol, EntryBlock dir, string name, uint thisSect)
        {
            if (!(dir.SecType == Constants.ST_ROOT || dir.SecType == Constants.ST_DIR))
            {
                throw new FileSystemException($"Invalid secondary type '{dir.SecType}'");
            }

            var intl = vol.UseIntl || vol.UseDirCache;
            var len = Math.Min(name.Length, Constants.MAXNAMELEN);
            var name2 = MyToUpper(name, intl);
            var hashValue = GetHashValue(dir.HashTable.Length, name, intl);
            var nSect = dir.HashTable[hashValue];

            if (nSect == 0)
            {
                uint newSect;
                if (thisSect != uint.MaxValue)
                    newSect = thisSect;
                else
                {
                    newSect = Bitmap.AdfGet1FreeBlock(vol);
                    if (newSect == uint.MaxValue)
                    {
                        throw new FileSystemException("No sector available");
                    }
                }

                dir.HashTable[hashValue] = newSect;
                dir.Date = DateTime.Now;
                await WriteEntryBlock(vol, dir.SecType == Constants.ST_ROOT ? vol.RootBlockOffset : dir.HeaderKey,
                    dir);

                return newSect;
            }

            EntryBlock updEntry;
            do
            {
                updEntry = await Disk.ReadEntryBlock(vol, nSect);
                if (updEntry == null)
                {
                    return uint.MaxValue;
                }

                if (updEntry.Name.Length == len)
                {
                    var name3 = MyToUpper(updEntry.Name, intl);
                    if (name3 == name2)
                    {
                        throw new PathAlreadyExistsException($"Path '{updEntry.Name}' already exists");
                    }
                }

                nSect = updEntry.NextSameHash;
            } while (nSect != 0);

            uint newSect2;
            if (thisSect != uint.MaxValue)
                newSect2 = thisSect;
            else
            {
                newSect2 = Bitmap.AdfGet1FreeBlock(vol);
                if (newSect2 == uint.MaxValue)
                {
                    throw new FileSystemException("No sector available");
                }
            }

            if (!(updEntry.SecType == Constants.ST_DIR || updEntry.SecType == Constants.ST_FILE))
            {
                throw new FileSystemException($"Invalid secondary type '{updEntry.SecType}'");
            }

            updEntry.NextSameHash = newSect2;
            await WriteEntryBlock(vol, updEntry.HeaderKey, updEntry);

            return newSect2;
        }

        /// <summary>
        /// Create directory
        /// </summary>
        /// <param name="vol"></param>
        /// <param name="parentSector"></param>
        /// <param name="name"></param>
        /// <exception cref="IOException"></exception>
        public static async Task CreateDirectory(Volume vol, uint parentSector, string name)
        {
            var parent = await Disk.ReadEntryBlock(vol, parentSector);

            /* -1 : do not use a specific, already allocated sector */
            var nSect = await CreateEntry(vol, parent, name, uint.MaxValue);
            if (nSect == uint.MaxValue)
            {
                throw new FileSystemException("No sector available");
            }

            var dirBlock = new DirBlock(vol.FileSystemBlockSize)
            {
                HeaderKey = nSect,
                Name = name,
                Date = DateTime.Now,
                Parent = parent.SecType == Constants.ST_ROOT ? vol.RootBlockOffset : parent.HeaderKey
            };

            if (vol.UseDirCache)
            {
                await Cache.AddInCache(vol, parent, dirBlock);
                await Cache.CreateEmptyCache(vol, dirBlock, uint.MaxValue);
            }

            await WriteEntryBlock(vol, nSect, dirBlock);

            await Bitmap.AdfUpdateBitmap(vol);
        }

        public static async Task RenameEntry(Volume vol, uint pSect, string oldName, uint nPSect, string newName)
        {
            if (oldName == newName)
            {
                return;
            }

            var intl = vol.UseIntl || vol.UseDirCache;
            var len = newName.Length;
            // myToUpper((uint8_t*)name2, (uint8_t*)newName, len, intl);
            // myToUpper((uint8_t*)name3, (uint8_t*)oldName, strlen(oldName), intl);
            var name2 = MyToUpper(newName, intl);
            var name3 = MyToUpper(oldName, intl);
            /* newName == oldName ? */

            var parent = await Disk.ReadEntryBlock(vol, pSect);

            var hashValueO = GetHashValue(parent.HashTable.Length, oldName, intl);

            var result = await GetEntryBlock(vol, parent.HashTable, oldName, false);
            var nSect = result.NSect;
            var parentEntryBlock = result.EntryBlock;
            var prevSect = result.NUpdSect ?? 0;
            if (nSect == uint.MaxValue)
            {
                throw new PathNotFoundException($"Path '{oldName}' not found");
            }

            /* change name and parent dir */
            parentEntryBlock.Name = newName;
            parentEntryBlock.Parent = nPSect;
            var tmpSect = parentEntryBlock.NextSameHash;

            parentEntryBlock.NextSameHash = 0;
            await WriteEntryBlock(vol, nSect, parentEntryBlock);

            /* del from the oldname list */

            /* in hashTable */
            if (prevSect == 0)
            {
                parent.HashTable[hashValueO] = tmpSect;
                await WriteEntryBlock(vol, pSect, parent);
            }
            else
            {
                /* in linked list */
                var previous = await Disk.ReadEntryBlock(vol, prevSect);
                /* entry.nextSameHash (tmpSect) could be == 0 */
                previous.NextSameHash = tmpSect;
                await WriteEntryBlock(vol, prevSect, previous);
            }

            var nParent = await Disk.ReadEntryBlock(vol, nPSect);

            var hashValueN = GetHashValue(nParent.HashTable.Length, newName, intl);
            var nSect2 = nParent.HashTable[hashValueN];
            /* no list */
            if (nSect2 == 0)
            {
                nParent.HashTable[hashValueN] = nSect;
                await WriteEntryBlock(vol, nPSect, nParent);
            }
            else
            {
                /* a list exists : addition at the end */
                EntryBlock previous;
                do
                {
                    previous = await Disk.ReadEntryBlock(vol, nSect2);

                    if (previous.Name.Length == len)
                    {
                        name3 = MyToUpper(previous.Name, intl);
                        if (name3 == name2)
                        {
                            throw new PathAlreadyExistsException($"Path '{previous.Name}' already exists");
                        }
                    }

                    nSect2 = previous.NextSameHash;
                } while (nSect2 != 0);

                if (!(previous.SecType == Constants.ST_DIR || previous.SecType == Constants.ST_FILE))
                {
                    throw new FileSystemException($"Invalid entry secType {previous.SecType}");
                }

                previous.NextSameHash = nSect;
                await WriteEntryBlock(vol, previous.HeaderKey, previous);
            }

            if (vol.UseDirCache)
            {
                if (pSect != nPSect)
                {
                    await Cache.UpdateCache(vol, parent, parentEntryBlock, true);
                }
                else
                {
                    await Cache.DeleteFromCache(vol, parent, parentEntryBlock.HeaderKey);
                    await Cache.AddInCache(vol, nParent, parentEntryBlock);
                }
            }
        }

        public static async Task WriteEntryBlock(Volume vol, uint nSect, EntryBlock ent)
        {
            if (vol.UseLnfs)
            {
                // check if comment fits in lnfs dir block,
                // if yes: if comment block is present, move comment to entry block and free comment block 
                // if no: create comment block, move comment, allocate block and write block

                var nameAndCommendSpaceLeft = Constants.LNFSNAMECMMTLEN - ent.Name.Length + 1;
                var useCommentBlock = nameAndCommendSpaceLeft < ent.Comment.Length + 1;

                if (useCommentBlock)
                {
                    // get free block for comment block, if use comment block and no block is allocated
                    if (ent.CommentBlock == 0)
                    {
                        ent.CommentBlock = Bitmap.AdfGet1FreeBlock(vol);
                    }

                    // create comment block
                    var commentBlock = new LongNameFileSystemCommentBlock
                    {
                        OwnKey = ent.CommentBlock,
                        HeaderKey = nSect,
                        Comment = ent.Comment
                    };

                    // remove comment from entry block
                    ent.Comment = string.Empty;

                    // write comment block to disk
                    var commentBlockBytes = LongNameFileSystemCommentBlockWriter.Build(commentBlock, vol.FileSystemBlockSize);
                    await Disk.WriteBlock(vol, ent.CommentBlock, commentBlockBytes);
                }
                else
                {
                    // free comment block, if not using comment block and block is allocated
                    if (ent.CommentBlock != 0)
                    {
                        Bitmap.AdfSetBlockFree(vol, ent.CommentBlock);
                        await Bitmap.AdfUpdateBitmap(vol);
                    }
                }
            }

            var blockBytes = EntryBlockBuilder.Build(ent, vol.FileSystemBlockSize, vol.UseLnfs);
            await Disk.WriteBlock(vol, nSect, blockBytes);
        }

        public static async Task RemoveEntry(Volume vol, uint pSect, string name, bool ignoreProtectionBits)
        {
            var parent = await Disk.ReadEntryBlock(vol, pSect);

            var result = await GetEntryBlock(vol, parent.HashTable, name, false);
            var nSect = result.NSect;
            var entryBlock = result.EntryBlock;
            var nSect2 = result.NUpdSect ?? 0;
            if (nSect == uint.MaxValue)
            {
                throw new PathNotFoundException($"Path '{name}' not found");
            }

            if (!ignoreProtectionBits && Macro.hasD(result.EntryBlock.Access))
            {
                throw new FileSystemException($"File '{name}' does not have delete protection bits set");
            }
            
            /* if it is a directory, is it empty ? */
            if (entryBlock.SecType == Constants.ST_DIR && !IsEmpty(entryBlock))
            {
                throw new DirectoryNotEmptyException($"Directory '{name}' is not empty");
            }

            /* in parent hashTable */
            if (nSect2 == 0)
            {
                var intl = vol.UseIntl || vol.UseDirCache;
                var hashVal = GetHashValue(entryBlock.HashTable.Length, name, intl);
                parent.HashTable[hashVal] = entryBlock.NextSameHash;
                await WriteEntryBlock(vol, pSect, parent);
            }
            /* in linked list */
            else
            {
                var previous = await Disk.ReadEntryBlock(vol, nSect2);
                previous.NextSameHash = entryBlock.NextSameHash;
                await WriteEntryBlock(vol, nSect2, previous);
            }

            if (entryBlock.SecType == Constants.ST_FILE)
            {
                var fileHeaderBlock = EntryBlockParser.Parse(entryBlock.BlockBytes, vol.UseLnfs);
                await File.AdfFreeFileBlocks(vol, fileHeaderBlock);
                Bitmap.AdfSetBlockFree(vol, nSect); //marks the FileHeaderBlock as free in BitmapBlock
                if (vol.UseLnfs && fileHeaderBlock.CommentBlock != 0)
                {
                    Bitmap.AdfSetBlockFree(vol,
                        fileHeaderBlock.CommentBlock); //marks the comment block as free in BitmapBlock
                }
            }
            else if (entryBlock.SecType == Constants.ST_DIR)
            {
                Bitmap.AdfSetBlockFree(vol, nSect);
                /* free dir cache block : the directory must be empty, so there's only one cache block */
                if (vol.UseDirCache)
                    Bitmap.AdfSetBlockFree(vol, entryBlock.Extension);
            }
            else
            {
                throw new FileSystemException($"SecType {entryBlock.SecType} not supported");
            }

            if (vol.UseDirCache)
            {
                await Cache.DeleteFromCache(vol, parent, entryBlock.HeaderKey);
            }

            await Bitmap.AdfUpdateBitmap(vol);
        }

        public static bool IsEmpty(EntryBlock dirBlock)
        {
            for (var i = 0; i < dirBlock.HashTable.Length; i++)
                if (dirBlock.HashTable[i] != 0)
                    return false;

            return true;
        }

        /// <summary>
        /// Set date for entry
        /// </summary>
        /// <param name="volume">Volume</param>
        /// <param name="parentSector"></param>
        /// <param name="name"></param>
        /// <param name="date"></param>
        /// <exception cref="IOException"></exception>
        public static async Task SetEntryDate(Volume volume, uint parentSector, string name, DateTime date)
        {
            var parent = await Disk.ReadEntryBlock(volume, parentSector);

            var result = await GetEntryBlock(volume, parent.HashTable, name, false);
            var nSect = result.NSect;
            var entryBlock = result.EntryBlock;
            if (nSect == uint.MaxValue)
            {
                throw new PathNotFoundException($"Path '{name}' not found");
            }

            if (!(entryBlock.SecType == Constants.ST_DIR || entryBlock.SecType == Constants.ST_FILE))
            {
                throw new FileSystemException($"Invalid entry secType '{entryBlock.SecType}'");
            }

            entryBlock.Date = date;
            await WriteEntryBlock(volume, nSect, entryBlock);

            if (volume.UseDirCache)
            {
                await Cache.UpdateCache(volume, parent, entryBlock, false);
            }
        }

        /// <summary>
        /// Set access for entry
        /// </summary>
        /// <param name="volume">Volume</param>
        /// <param name="parentSector"></param>
        /// <param name="name"></param>
        /// <param name="access"></param>
        /// <exception cref="IOException"></exception>
        public static async Task SetEntryAccess(Volume volume, uint parentSector, string name, uint access)
        {
            var parent = await Disk.ReadEntryBlock(volume, parentSector);

            var result = await GetEntryBlock(volume, parent.HashTable, name, false);
            var nSect = result.NSect;
            var entryBlock = result.EntryBlock;
            if (nSect == uint.MaxValue)
            {
                throw new PathNotFoundException($"Path '{name}' not found");
            }

            if (!(entryBlock.SecType == Constants.ST_DIR || entryBlock.SecType == Constants.ST_FILE))
            {
                throw new FileSystemException($"Invalid entry secType '{entryBlock.SecType}'");
            }

            entryBlock.Access = access;
            await WriteEntryBlock(volume, nSect, entryBlock);

            if (volume.UseDirCache)
            {
                await Cache.UpdateCache(volume, parent, entryBlock, false);
            }
        }

        /// <summary>
        /// Set entry comment
        /// </summary>
        /// <param name="volume">Volume mounted</param>
        /// <param name="parentSector">Parent sector</param>
        /// <param name="name">Name of entry</param>
        /// <param name="comment">Comment</param>
        /// <exception cref="IOException"></exception>
        public static async Task SetEntryComment(Volume volume, uint parentSector, string name, string comment)
        {
            var parent = await Disk.ReadEntryBlock(volume, parentSector);

            var result = await GetEntryBlock(volume, parent.HashTable, name, false);
            var nSect = result.NSect;
            var entryBlock = result.EntryBlock;
            if (nSect == uint.MaxValue)
            {
                throw new DiskFullException("No sector available");
            }

            if (!(entryBlock.SecType == Constants.ST_DIR || entryBlock.SecType == Constants.ST_FILE))
            {
                throw new FileSystemException($"Invalid entry secType '{entryBlock.SecType}'");
            }

            entryBlock.Comment = comment.Length > Constants.MAXCMMTLEN
                ? comment.Substring(0, Constants.MAXCMMTLEN)
                : comment;
            await WriteEntryBlock(volume, nSect, entryBlock);

            if (volume.UseDirCache)
            {
                await Cache.UpdateCache(volume, parent, entryBlock, true);
            }
        }

        public static async Task<FindEntryResult> FindEntry(uint sector, string path, Volume volume)
        {
            var parts = (path.StartsWith("/") ? path.Substring(1) : path).Split('/');

            if (parts.Length == 0 || string.IsNullOrEmpty(parts[0]))
            {
                return new FindEntryResult
                {
                    Name = string.Empty,
                    Sector = sector,
                    PartsNotFound = Array.Empty<string>()
                };
            }

            var entryBlock = await Disk.ReadEntryBlock(volume, sector);
            sector = FastFileSystemHelper.GetSector(volume, entryBlock);
            
            int i;
            for (i = 0; i < parts.Length; i++)
            {
                var part = parts[i];

                var entry = (await ReadEntries(volume, sector)).FirstOrDefault(x =>
                    x.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                {
                    break;
                }

                if (entry.IsDirectory())
                {
                    sector = FastFileSystemHelper.GetSector(volume, entry.EntryBlock);
                }
            }

            return new FindEntryResult
            {
                Name = parts.Last(),
                Sector = sector,
                PartsNotFound = parts.Skip(i).ToArray()
            };
        }
    }
}