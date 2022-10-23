namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;
    using Extensions;

    public static class Directory
    {
        public static async Task<IEnumerable<Entry>> ReadEntries(Volume volume, int nSect, bool recursive = false)
        {
            if (volume.UsesDirCache)
            {
                return await Cache.ReadEntries(volume, nSect, recursive);
            }

            var startEntryBlock = await Disk.ReadEntryBlock(volume, nSect);

            var hashTable = startEntryBlock.HashTable.ToList();
            var entries = new List<Entry>();

            for (var i = 0; i < Constants.HT_SIZE; i++)
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

                var entry = ConvertEntryBlockToEntry(entryBlock);
                entry.Sector = hashTable[i];

                entries.Add(entry);

                if (recursive && entry.IsDirectory())
                {
                    entry.SubDir = (await ReadEntries(volume,
                        FastFileSystemHelper.GetSector(volume, entryBlock.HeaderKey), true)).ToList();
                }

                //         /* same hashcode linked list */
                //         nextSector = entryBlk.nextSameHash;
                //         while( nextSector!=0 ) {
                var nextSector = entryBlock.NextSameHash;
                while (nextSector != 0)
                {
                    entryBlock = await Disk.ReadEntryBlock(volume, nextSector);

                    entry = ConvertEntryBlockToEntry(entryBlock);
                    entry.Sector = nextSector;

                    if (recursive && entry.IsDirectory())
                    {
                        entry.SubDir = (await ReadEntries(volume,
                            FastFileSystemHelper.GetSector(volume, entryBlock.HeaderKey), true)).ToList();
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
        private static Entry ConvertEntryBlockToEntry(EntryBlock entryBlock)
        {
            var entry = new Entry
            {
                Type = entryBlock.SecType,
                Parent = entryBlock.Parent,
                Name = entryBlock.Name,
                Comment = string.Empty,
                Date = entryBlock.Date,
                Access = -1,
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

        public static int GetHashValue(string name, bool intl)
        {
            var hash = (uint)name.Length;
            foreach (var c in name)
            {
                var upper = intl ? IntlToUpper(c) : ToUpper(c);
                hash = (hash * 13 + upper) & 0x7ff;
            }

            hash %= Constants.HT_SIZE;
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

        public static async Task<NameToEntryBlockResult> GetEntryBlock(Volume volume, int[] ht, string name,
            bool nUpdSect)
        {
            var intl = Macro.isINTL(volume.DosType) || volume.UsesDirCache;
            var hashVal = GetHashValue(name, intl);
            var nameLen = Math.Min(name.Length, Constants.MAXNAMELEN);
            var upperName = MyToUpper(name, intl);

            var nSect = ht[hashVal];
            if (nSect == 0)
                return new NameToEntryBlockResult
                {
                    NSect = -1
                };

            EntryBlock entry;
            var updSect = 0;
            var found = false;
            do
            {
                entry = await Disk.ReadEntryBlock(volume, nSect);
                if (entry == null)
                {
                    return new NameToEntryBlockResult
                    {
                        NSect = -1
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
                    NSect = -1,
                };

            return new NameToEntryBlockResult
            {
                NSect = nSect,
                EntryBlock = entry,
                NUpdSect = nUpdSect ? new int?(updSect) : null
            };
        }

        public static async Task<EntryBlock> CreateFile(Volume vol, EntryBlock parent, string name)
        {
            /* -1 : do not use a specific, already allocated sector */
            var nSect = await CreateEntry(vol, parent, name, -1);
            if (nSect == -1) throw new IOException("error nSect is -1");

            if (!(parent.SecType == Constants.ST_ROOT || parent.SecType == Constants.ST_DIR))
            {
                throw new IOException($"Invalid secondary type '{parent.SecType}'");
            }

            var entryBlock = new FileHeaderBlock
            {
                HeaderKey = nSect,
                Name = name,
                Parent = parent.SecType == Constants.ST_ROOT ? (int)vol.RootBlockOffset : parent.HeaderKey,
                Date = DateTime.Now
            };

            await Disk.WriteFileHdrBlock(vol, nSect, entryBlock);

            if (vol.UsesDirCache)
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
        public static async Task<int> CreateEntry(Volume vol, EntryBlock dir, string name, int thisSect)
        {
            if (!(dir.SecType == Constants.ST_ROOT || dir.SecType == Constants.ST_DIR))
            {
                throw new IOException($"Invalid secondary type '{dir.SecType}'");
            }

            var intl = Macro.isINTL(vol.DosType) || vol.UsesDirCache;
            var len = Math.Min(name.Length, Constants.MAXNAMELEN);
            var name2 = MyToUpper(name, intl);
            var hashValue = GetHashValue(name, intl);
            var nSect = dir.HashTable[hashValue];

            if (nSect == 0)
            {
                int newSect;
                if (thisSect != -1)
                    newSect = thisSect;
                else
                {
                    newSect = Bitmap.AdfGet1FreeBlock(vol);
                    if (newSect == -1)
                    {
                        throw new IOException("nSect==-1");
                    }
                }

                dir.HashTable[hashValue] = newSect;
                dir.Date = DateTime.Now;
                await WriteEntryBlock(vol, dir.SecType == Constants.ST_ROOT ? (int)vol.RootBlockOffset : dir.HeaderKey,
                    dir);

                return newSect;
            }

            EntryBlock updEntry;
            do
            {
                updEntry = await Disk.ReadEntryBlock(vol, nSect);
                if (updEntry == null)
                    return -1;
                if (updEntry.Name.Length == len)
                {
                    var name3 = MyToUpper(updEntry.Name, intl);
                    if (name3 == name2)
                    {
                        throw new IOException("entry already exists");
                    }
                }

                nSect = updEntry.NextSameHash;
            } while (nSect != 0);

            int newSect2;
            if (thisSect != -1)
                newSect2 = thisSect;
            else
            {
                newSect2 = Bitmap.AdfGet1FreeBlock(vol);
                if (newSect2 == -1)
                {
                    throw new IOException("nSect==-1");
                }
            }

            if (!(updEntry.SecType == Constants.ST_DIR || updEntry.SecType == Constants.ST_FILE))
            {
                throw new IOException($"Invalid secondary type '{updEntry.SecType}'");
            }

            updEntry.NextSameHash = newSect2;
            await WriteEntryBlock(vol, updEntry.HeaderKey, updEntry);

            return newSect2;
        }

        /// <summary>
        /// Create directory
        /// </summary>
        /// <param name="vol"></param>
        /// <param name="parent"></param>
        /// <param name="name"></param>
        /// <exception cref="IOException"></exception>
        public static async Task CreateDirectory(Volume vol, EntryBlock parent, string name)
        {
            /* -1 : do not use a specific, already allocated sector */
            var nSect = await CreateEntry(vol, parent, name, -1);
            if (nSect == -1)
            {
                throw new IOException("no sector available");
            }

            var dirBlock = new DirBlock
            {
                HeaderKey = nSect,
                Name = name,
                Date = DateTime.Now,
                Parent = parent.SecType == Constants.ST_ROOT ? (int)vol.RootBlockOffset : parent.HeaderKey
            };

            if (vol.UsesDirCache)
            {
                await Cache.AddInCache(vol, parent, dirBlock);
                await Cache.CreateEmptyCache(vol, dirBlock, -1);
            }

            await WriteEntryBlock(vol, nSect, dirBlock);

            await Bitmap.AdfUpdateBitmap(vol);
        }

        public static async Task RenameEntry(Volume vol, int pSect, string oldName, int nPSect, string newName)
        {
            if (oldName == newName)
            {
                return;
            }

            var intl = Macro.isINTL(vol.DosType) || vol.UsesDirCache;
            var len = newName.Length;
            // myToUpper((uint8_t*)name2, (uint8_t*)newName, len, intl);
            // myToUpper((uint8_t*)name3, (uint8_t*)oldName, strlen(oldName), intl);
            var name2 = MyToUpper(newName, intl);
            var name3 = MyToUpper(oldName, intl);
            /* newName == oldName ? */

            var parent = await Disk.ReadEntryBlock(vol, pSect);

            var hashValueO = GetHashValue(oldName, intl);

            var result = await GetEntryBlock(vol, parent.HashTable, oldName, false);
            var nSect = result.NSect;
            var parentEntryBlock = result.EntryBlock;
            var prevSect = result.NUpdSect ?? 0;
            if (nSect == -1)
            {
                throw new IOException("existing entry not found");
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

            var hashValueN = GetHashValue(newName, intl);
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
                            throw new IOException("entry already exists");
                        }
                    }

                    nSect2 = previous.NextSameHash;
                } while (nSect2 != 0);

                if (!(previous.SecType == Constants.ST_DIR || previous.SecType == Constants.ST_FILE))
                {
                    throw new IOException("Invalid entry secType");
                }

                previous.NextSameHash = nSect;
                await WriteEntryBlock(vol, previous.HeaderKey, previous);
            }

            if (vol.UsesDirCache)
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

        public static async Task WriteEntryBlock(Volume vol, int nSect, EntryBlock ent)
        {
            var blockBytes = EntryBlockBuilder.Build(ent, vol.BlockSize);
            await Disk.WriteBlock(vol, nSect, blockBytes);
        }

        public static async Task RemoveEntry(Volume vol, int pSect, string name)
        {
            var parent = await Disk.ReadEntryBlock(vol, pSect);

            var result = await GetEntryBlock(vol, parent.HashTable, name, false);
            var nSect = result.NSect;
            var entryBlock = result.EntryBlock;
            var nSect2 = result.NUpdSect ?? 0;
            if (nSect == -1)
            {
                throw new IOException($"entry '{name}' not found");
            }

            /* if it is a directory, is it empty ? */
            if (entryBlock.SecType == Constants.ST_DIR && !IsEmpty(entryBlock))
            {
                throw new IOException($"directory '{name}' not empty");
            }

            /* in parent hashTable */
            if (nSect2 == 0)
            {
                var intl = Macro.isINTL(vol.DosType) || vol.UsesDirCache;
                var hashVal = GetHashValue(name, intl);
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
                var fileHeaderBlock = EntryBlockParser.Parse(entryBlock.BlockBytes);
                await File.AdfFreeFileBlocks(vol, fileHeaderBlock);
                Bitmap.AdfSetBlockFree(vol, nSect); //marks the FileHeaderBlock as free in BitmapBlock
            }
            else if (entryBlock.SecType == Constants.ST_DIR)
            {
                Bitmap.AdfSetBlockFree(vol, nSect);
                /* free dir cache block : the directory must be empty, so there's only one cache block */
                if (vol.UsesDirCache)
                    Bitmap.AdfSetBlockFree(vol, entryBlock.Extension);
            }
            else
            {
                throw new IOException($"secType {entryBlock.SecType} not supported");
            }

            if (vol.UsesDirCache)
            {
                await Cache.DeleteFromCache(vol, parent, entryBlock.HeaderKey);
            }

            await Bitmap.AdfUpdateBitmap(vol);
        }

        public static bool IsEmpty(EntryBlock dirBlock)
        {
            for (var i = 0; i < Constants.HT_SIZE; i++)
                if (dirBlock.HashTable[i] != 0)
                    return false;

            return true;
        }

        /// <summary>
        /// Set date for entry
        /// </summary>
        /// <param name="vol">Volume</param>
        /// <param name="parent"></param>
        /// <param name="name"></param>
        /// <param name="date"></param>
        /// <exception cref="IOException"></exception>
        public static async Task SetEntryDate(Volume vol, EntryBlock parent, string name, DateTime date)
        {
            var result = await GetEntryBlock(vol, parent.HashTable, name, false);
            var nSect = result.NSect;
            var entryBlock = result.EntryBlock;
            if (nSect == -1)
            {
                throw new IOException("entry not found");
            }

            if (!(entryBlock.SecType == Constants.ST_DIR || entryBlock.SecType == Constants.ST_FILE))
            {
                throw new IOException("Invalid entry secType");
            }

            entryBlock.Date = date;
            await WriteEntryBlock(vol, nSect, entryBlock);

            if (vol.UsesDirCache)
            {
                await Cache.UpdateCache(vol, parent, entryBlock, false);
            }
        }

        /// <summary>
        /// Set access for entry
        /// </summary>
        /// <param name="vol">Volume</param>
        /// <param name="parent"></param>
        /// <param name="name"></param>
        /// <param name="access"></param>
        /// <exception cref="IOException"></exception>
        public static async Task SetEntryAccess(Volume vol, EntryBlock parent, string name, int access)
        {
            var result = await GetEntryBlock(vol, parent.HashTable, name, false);
            var nSect = result.NSect;
            var entryBlock = result.EntryBlock;
            if (nSect == -1)
            {
                throw new IOException("entry not found");
            }

            if (!(entryBlock.SecType == Constants.ST_DIR || entryBlock.SecType == Constants.ST_FILE))
            {
                throw new IOException("Invalid entry secType");
            }

            entryBlock.Access = access;
            await WriteEntryBlock(vol, nSect, entryBlock);

            if (vol.UsesDirCache)
            {
                await Cache.UpdateCache(vol, parent, entryBlock, false);
            }
        }

        /// <summary>
        /// Set entry comment
        /// </summary>
        /// <param name="volume">Volume mounted</param>
        /// <param name="parent">Parent entry block</param>
        /// <param name="name">Name of entry</param>
        /// <param name="comment">Comment</param>
        /// <exception cref="IOException"></exception>
        public static async Task SetEntryComment(Volume volume, EntryBlock parent, string name, string comment)
        {
            var result = await GetEntryBlock(volume, parent.HashTable, name, false);
            var nSect = result.NSect;
            var entryBlock = result.EntryBlock;
            if (nSect == -1)
            {
                throw new IOException("entry not found");
            }

            if (!(entryBlock.SecType == Constants.ST_DIR || entryBlock.SecType == Constants.ST_FILE))
            {
                throw new IOException("Invalid entry secType");
            }

            entryBlock.Comment = comment.Length > Constants.MAXCMMTLEN
                ? comment.Substring(0, Constants.MAXCMMTLEN)
                : comment;
            await WriteEntryBlock(volume, nSect, entryBlock);

            if (volume.UsesDirCache)
            {
                await Cache.UpdateCache(volume, parent, entryBlock, true);
            }
        }

        public static async Task<FindEntryResult> FindEntry(EntryBlock currentDirectory, string path, Volume volume)
        {
            var parts = (path.StartsWith("/") ? path.Substring(1) : path).Split('/');

            if (parts.Length == 0 || string.IsNullOrEmpty(parts[0]))
            {
                return new FindEntryResult
                {
                    Name = string.Empty,
                    EntryBlock = currentDirectory,
                    PartsNotFound = Array.Empty<string>()
                };
            }

            int i;
            for (i = 0; i < parts.Length; i++)
            {
                var part = parts[i];

                var entry = (await ReadEntries(volume,
                        currentDirectory.HeaderKey == 0 ? (int)volume.RootBlockOffset : currentDirectory.HeaderKey))
                    .FirstOrDefault(x =>
                        x.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                {
                    break;
                }

                if (entry.IsDirectory())
                {
                    currentDirectory = entry.EntryBlock;
                }
            }

            return new FindEntryResult
            {
                Name = parts.Last(),
                EntryBlock = currentDirectory,
                PartsNotFound = parts.Skip(i).ToArray()
            };
        }
    }
}