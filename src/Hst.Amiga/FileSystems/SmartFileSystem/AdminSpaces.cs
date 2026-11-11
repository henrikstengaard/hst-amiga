namespace Hst.Amiga.FileSystems.SmartFileSystem
{
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Exceptions;

    /// <summary>
    /// Allocation of admin space blocks used for metadata blocks, see fs/adminspaces.c in smart file system source.
    /// </summary>
    public static class AdminSpaces
    {
        /// <summary>
        /// Allocate an admin space block, allocadminspace() in smart file system source.
        /// Caller is responsible for writing the allocated block.
        /// </summary>
        /// <returns>Allocated block.</returns>
        public static async Task<uint> Allocate(Volume volume)
        {
            for (;;)
            {
                var adminSpaceBlock = volume.RootBlock.AdminSpaceContainer;
                while (adminSpaceBlock != 0)
                {
                    var adminSpaceContainer = await Disk.ReadAdminSpaceContainer(volume, adminSpaceBlock);
                    foreach (var adminSpace in adminSpaceContainer.AdminSpaces)
                    {
                        if (adminSpace.Space == 0 || adminSpace.Bits == 0xffffffff)
                        {
                            continue;
                        }

                        var bitOffset = 0;
                        while ((adminSpace.Bits & (0x80000000U >> bitOffset)) != 0)
                        {
                            bitOffset++;
                        }

                        adminSpace.Bits |= 0x80000000U >> bitOffset;
                        await Disk.WriteAdminSpaceContainer(volume, adminSpaceContainer);
                        return adminSpace.Space + (uint)bitOffset;
                    }

                    adminSpaceBlock = adminSpaceContainer.Next;
                }

                await AddAdminSpace(volume);
            }
        }

        /// <summary>
        /// Find and mark 32 blocks of free space and add it as new admin space.
        /// </summary>
        private static async Task AddAdminSpace(Volume volume)
        {
            var startBlock = Bitmap.FindSpace(volume, Constants.BlocksAdmin, 0, volume.TotalBlocks);
            if (startBlock == null)
            {
                throw new DiskFullException("Disk full, no space for admin space");
            }

            await Bitmap.MarkUsed(volume, startBlock.Value, Constants.BlocksAdmin);

            var adminSpaceBlock = volume.RootBlock.AdminSpaceContainer;
            for (;;)
            {
                var adminSpaceContainer = await Disk.ReadAdminSpaceContainer(volume, adminSpaceBlock);
                foreach (var adminSpace in adminSpaceContainer.AdminSpaces)
                {
                    if (adminSpace.Space != 0)
                    {
                        continue;
                    }

                    adminSpace.Space = startBlock.Value;
                    adminSpace.Bits = 0;
                    await Disk.WriteAdminSpaceContainer(volume, adminSpaceContainer);
                    return;
                }

                if (adminSpaceContainer.Next != 0)
                {
                    adminSpaceBlock = adminSpaceContainer.Next;
                    continue;
                }

                // no unused admin space in admin space containers, create a new admin space container
                // as first block in new admin space
                adminSpaceContainer.Next = startBlock.Value;
                await Disk.WriteAdminSpaceContainer(volume, adminSpaceContainer);

                var newAdminSpaceContainer = new AdminSpaceContainer(volume.BlockSize)
                {
                    OwnBlock = startBlock.Value,
                    Previous = adminSpaceBlock,
                    Bits = Constants.BlocksAdmin
                };
                newAdminSpaceContainer.AdminSpaces[0].Space = startBlock.Value;
                newAdminSpaceContainer.AdminSpaces[0].Bits = 0x80000000;
                await Disk.WriteAdminSpaceContainer(volume, newAdminSpaceContainer);
                return;
            }
        }

        /// <summary>
        /// Free an admin space block, freeadminspace() in smart file system source.
        /// </summary>
        public static async Task Free(Volume volume, uint block)
        {
            var adminSpaceBlock = volume.RootBlock.AdminSpaceContainer;
            while (adminSpaceBlock != 0)
            {
                var adminSpaceContainer = await Disk.ReadAdminSpaceContainer(volume, adminSpaceBlock);
                foreach (var adminSpace in adminSpaceContainer.AdminSpaces)
                {
                    if (adminSpace.Space == 0 || block < adminSpace.Space || block >= adminSpace.Space + Constants.BlocksAdmin)
                    {
                        continue;
                    }

                    adminSpace.Bits &= ~(0x80000000U >> (int)(block - adminSpace.Space));
                    await Disk.WriteAdminSpaceContainer(volume, adminSpaceContainer);
                    return;
                }

                adminSpaceBlock = adminSpaceContainer.Next;
            }

            throw new IOException($"Unable to free admin block {block}, block not found in admin spaces");
        }
    }
}
