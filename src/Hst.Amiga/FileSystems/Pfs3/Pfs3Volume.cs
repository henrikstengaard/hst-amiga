namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;
    using RigidDiskBlocks;

    public class Pfs3Volume : IAsyncDisposable
    {
        public readonly globaldata g;

        public Pfs3Volume(globaldata g)
        {
            this.g = g;
        }

        public async ValueTask DisposeAsync()
        {
            await Pfs3Helper.Unmount(g);

            GC.SuppressFinalize(this);
        }

        public async Task<IEnumerable<Entry>> GetEntries()
        {
            // root dir
            var dirNodeNr = (uint)Macro.ANODE_ROOTDIR;
            
            return (await Directory.GetDirEntries(dirNodeNr, g)).Select(GetEntry)
                .OrderBy(x => x.Type).ThenBy(x => x.Name).ToList();
        }

        private Entry GetEntry(direntry dirEntry)
        {
            return new Entry
            {
                Name = dirEntry.Name,
                Type = GetEntryType(dirEntry.type),
                Size = dirEntry.fsize,
                ProtectionBits = GetProtectionBits(dirEntry.protection),
                CreationDate = dirEntry.CreationDate
            };
        }

        private EntryType GetEntryType(int type)
        {
            switch (type)
            {
                case Constants.ST_USERDIR:
                    return EntryType.Dir;
                case Constants.ST_FILE:
                    return EntryType.File;
                case Constants.ST_SOFTLINK:
                    return EntryType.SoftLink;
                case Constants.ST_LINKDIR:
                    return EntryType.DirLink;
                case Constants.ST_LINKFILE:
                    return EntryType.FileLink;
                default:
                    return EntryType.File;
            }
        }

        private ProtectionBits GetProtectionBits(int protection)
        {
            var protectionBits = ProtectionBits.Read | ProtectionBits.Write | ProtectionBits.Executable | ProtectionBits.Delete;

            // add held resident flag, if bit is present
            if ((protection & 128) == 128)
            {
                protectionBits |= ProtectionBits.HeldResident;
            }

            // add script flag, if bit is present
            if ((protection & 64) == 64)
            {
                protectionBits |= ProtectionBits.Script;
            }

            // add pure flag, if bit is present
            if ((protection & 32) == 32)
            {
                protectionBits |= ProtectionBits.Pure;
            }

            // add archive flag, if bit is present
            if ((protection & 16) == 16)
            {
                protectionBits |= ProtectionBits.Archive;
            }
            
            // remove read flag, if bit is present
            if ((protection & 8) == 8)
            {
                protectionBits &= ~ProtectionBits.Read;
            }
            
            // remove write flag, if bit is present
            if ((protection & 4) == 4)
            {
                protectionBits &= ~ProtectionBits.Write;
            }
            
            // remove executable flag, if bit is present
            if ((protection & 2) == 2)
            {
                protectionBits &= ~ProtectionBits.Executable;
            }

            // remove delete flag, if bit is present
            if ((protection & 1) == 1)
            {
                protectionBits &= ~ProtectionBits.Delete;
            }
            
            return protectionBits;
        }

        public static async Task<Pfs3Volume> Mount(Stream stream, PartitionBlock partitionBlock)
        {
            return new Pfs3Volume(await Pfs3Helper.Mount(stream, partitionBlock));
        }
    }
}