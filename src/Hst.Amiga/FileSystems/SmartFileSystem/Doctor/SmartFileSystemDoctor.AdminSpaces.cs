namespace Hst.Amiga.FileSystems.SmartFileSystem.Doctor
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;

    /// <summary>
    /// Admin space containers and allocation of administration blocks.
    /// </summary>
    public partial class SmartFileSystemDoctor
    {
        /// <summary>
        /// Admin space container in admin space container chain.
        /// </summary>
        private class AdminContainerModel
        {
            public uint Block;
            public AdminSpaceContainer Container;
            public bool Dirty;
        }

        private readonly List<AdminContainerModel> adminContainers;

        /// <summary>
        /// Admin spaces created by repair.
        /// </summary>
        private readonly HashSet<uint> newAdminSpaces = new HashSet<uint>();

        /// <summary>
        /// Administration blocks allocated by repair.
        /// </summary>
        private readonly HashSet<uint> allocatedAdmin = new HashSet<uint>();

        /// <summary>
        /// Blocks used by reserved blocks, root blocks, bitmap blocks and admin spaces.
        /// </summary>
        private BitArray metadataMap;

        /// <summary>
        /// Blocks used by extents of files.
        /// </summary>
        private BitArray dataMap;

        private IEnumerable<AdminSpace> AdminSpaces =>
            adminContainers.SelectMany(x => x.Container.AdminSpaces).Where(x => x.Space != 0);

        /// <summary>
        /// Read admin space container chain and admin spaces.
        /// </summary>
        private async Task ReadAdminSpaces()
        {
            ShowMsg("Checking admin space containers");

            var block = rootBlock.AdminSpaceContainer;
            var previous = 0U;
            var visited = new HashSet<uint>();
            while (block != 0)
            {
                var data = await ReadBlock(block);
                var problem = GetBlockProblem(data, block, Constants.AdminSpaceContainerId);
                if (problem == null && !visited.Add(block))
                {
                    problem = $"chain loops at block {block}";
                }

                if (problem == null && !ClaimAdmin(block))
                {
                    problem = $"block {block} is used by another structure";
                }

                if (problem != null)
                {
                    if (adminContainers.Count == 0)
                    {
                        AddIssue(IssueType.AdminSpaceContainerInvalid, block, null,
                            $"Admin space container is invalid: {problem}, empty admin space container is created");
                        ClaimAdmin(block);
                        adminContainers.Add(new AdminContainerModel
                        {
                            Block = block,
                            Container = new AdminSpaceContainer(volume.BlockSize) { OwnBlock = block },
                            Dirty = true
                        });
                    }
                    else
                    {
                        AddIssue(IssueType.AdminSpaceContainerInvalid, previous, null,
                            $"Admin space container chain is broken: {problem}, chain is truncated");
                    }

                    break;
                }

                var container = AdminSpaceContainerParser.Parse(data);
                var model = new AdminContainerModel { Block = block, Container = container };
                adminContainers.Add(model);

                if (container.Previous != previous)
                {
                    AddIssue(IssueType.AdminSpaceContainerInvalid, block, null,
                        $"Admin space container has previous {container.Previous}, expected {previous}");
                }

                if (container.Bits != Constants.BlocksAdmin)
                {
                    AddIssue(IssueType.AdminSpaceContainerInvalid, block, null,
                        $"Admin space container has {container.Bits} blocks per admin space, expected {Constants.BlocksAdmin}");
                    container.Bits = Constants.BlocksAdmin;
                    model.Dirty = true;
                }

                previous = block;
                block = container.Next;
            }

            // blocks used by reserved blocks, root blocks and bitmap blocks
            metadataMap = CreateBlockMap();
            MarkRange(metadataMap, 0, volume.Reserved);
            MarkRange(metadataMap, volume.Blocks - volume.PreAlloc, volume.PreAlloc);
            MarkRange(metadataMap, rootBlock.BitmapBase, (uint)bitmapBlocks);

            foreach (var model in adminContainers)
            {
                foreach (var adminSpace in model.Container.AdminSpaces.Where(x => x.Space != 0))
                {
                    string problem = null;
                    if ((ulong)adminSpace.Space + Constants.BlocksAdmin > volume.Blocks)
                    {
                        problem = "is outside partition";
                    }
                    else if (AnyInRange(metadataMap, adminSpace.Space, Constants.BlocksAdmin))
                    {
                        problem = "overlaps reserved blocks, root blocks, bitmap or another admin space";
                    }

                    if (problem != null)
                    {
                        AddIssue(IssueType.AdminSpaceInvalid, model.Block, null,
                            $"Admin space at block {adminSpace.Space} {problem}, admin space is removed");
                        adminSpace.Space = 0;
                        adminSpace.Bits = 0;
                        model.Dirty = true;
                        continue;
                    }

                    MarkRange(metadataMap, adminSpace.Space, Constants.BlocksAdmin);
                }
            }
        }

        /// <summary>
        /// Block is inside an admin space.
        /// </summary>
        private bool IsInAdminSpace(uint block) =>
            AdminSpaces.Any(x => block >= x.Space && block < x.Space + Constants.BlocksAdmin);

        /// <summary>
        /// Allocate an administration block. Blocks from pool are used first, then free blocks in admin spaces. If
        /// all admin spaces are full, a new admin space is created.
        /// </summary>
        /// <returns>Block allocated or 0, if there is no free space.</returns>
        private uint AllocateAdminBlock(Queue<uint> pool = null)
        {
            while (pool != null && pool.Count > 0)
            {
                var block = pool.Dequeue();
                if (ClaimAdmin(block))
                {
                    allocatedAdmin.Add(block);
                    return block;
                }
            }

            foreach (var adminSpace in AdminSpaces.ToList())
            {
                for (uint i = 0; i < Constants.BlocksAdmin; i++)
                {
                    if (ClaimAdmin(adminSpace.Space + i))
                    {
                        allocatedAdmin.Add(adminSpace.Space + i);
                        return adminSpace.Space + i;
                    }
                }
            }

            var space = CreateAdminSpace(null);
            if (space == 0)
            {
                AddIssue(IssueType.DiskFull, 0, null, "No free space to allocate administration block",
                    fixable: false);
                return 0;
            }

            for (uint i = 0; i < Constants.BlocksAdmin; i++)
            {
                if (ClaimAdmin(space + i))
                {
                    allocatedAdmin.Add(space + i);
                    return space + i;
                }
            }

            return 0;
        }

        /// <summary>
        /// Create admin space of 32 free blocks. If cover is set, admin space is created so it contains the block.
        /// Admin space is added to an admin space container with a free admin space or the first block of the admin
        /// space becomes a new admin space container.
        /// </summary>
        /// <returns>First block of admin space or 0, if no space was found.</returns>
        private uint CreateAdminSpace(uint? cover)
        {
            var firstStart = cover.HasValue ? (cover.Value >= Constants.BlocksAdmin - 1 ? cover.Value - (Constants.BlocksAdmin - 1) : 0) : 0;
            var lastStart = cover ?? volume.Blocks;
            var freeSlot = adminContainers
                .SelectMany(x => x.Container.AdminSpaces.Select(s => (Model: x, Space: s)))
                .FirstOrDefault(x => x.Space.Space == 0);

            for (var start = firstStart; start <= lastStart && (ulong)start + Constants.BlocksAdmin <= volume.Blocks; start++)
            {
                if (AnyInRange(metadataMap, start, Constants.BlocksAdmin) ||
                    (dataMap != null && AnyInRange(dataMap, start, Constants.BlocksAdmin)))
                {
                    continue;
                }

                if (!cover.HasValue && Enumerable.Range(0, Constants.BlocksAdmin).Any(i => adminUsed.Contains(start + (uint)i)))
                {
                    continue;
                }

                if (freeSlot.Model != null)
                {
                    freeSlot.Space.Space = start;
                    freeSlot.Space.Bits = 0;
                    freeSlot.Model.Dirty = true;
                }
                else
                {
                    // first block of admin space becomes a new admin space container linked last in chain
                    if (adminUsed.Contains(start) || adminContainers.Count == 0)
                    {
                        continue;
                    }

                    ClaimAdmin(start);
                    var container = new AdminSpaceContainer(volume.BlockSize)
                    {
                        OwnBlock = start,
                        Previous = adminContainers[adminContainers.Count - 1].Block
                    };
                    container.AdminSpaces[0].Space = start;
                    adminContainers[adminContainers.Count - 1].Container.Next = start;
                    adminContainers[adminContainers.Count - 1].Dirty = true;
                    adminContainers.Add(new AdminContainerModel { Block = start, Container = container, Dirty = true });
                }

                newAdminSpaces.Add(start);
                MarkRange(metadataMap, start, Constants.BlocksAdmin);
                ShowMsg($"Created admin space at block {start}");
                return start;
            }

            return 0;
        }

        /// <summary>
        /// Update admin space bits to match administration blocks used and fix admin space container chain.
        /// Administration blocks not inside any admin space get a new admin space.
        /// </summary>
        private Task FinalizeAdminSpaces()
        {
            ShowMsg("Checking admin spaces");

            foreach (var block in adminUsed.OrderBy(x => x).ToList())
            {
                if (IsInAdminSpace(block))
                {
                    continue;
                }

                var space = CreateAdminSpace(block);
                if (space == 0)
                {
                    AddIssue(IssueType.AdminBlockNotMarked, block, null,
                        "Administration block is not inside any admin space", fixable: false);
                }
                else
                {
                    AddIssue(IssueType.AdminBlockNotMarked, block, null,
                        $"Administration block is not inside any admin space, admin space is created at block {space}");
                }
            }

            var notMarked = new List<uint>();
            var notUsed = new List<uint>();
            foreach (var model in adminContainers)
            {
                foreach (var adminSpace in model.Container.AdminSpaces.Where(x => x.Space != 0))
                {
                    uint bits = 0;
                    for (var i = 0; i < Constants.BlocksAdmin; i++)
                    {
                        if (adminUsed.Contains(adminSpace.Space + (uint)i))
                        {
                            bits |= 0x80000000U >> i;
                        }
                    }

                    if (bits == adminSpace.Bits)
                    {
                        continue;
                    }

                    if (!newAdminSpaces.Contains(adminSpace.Space))
                    {
                        for (var i = 0; i < Constants.BlocksAdmin; i++)
                        {
                            var mask = 0x80000000U >> i;
                            if ((bits & mask) != 0 && (adminSpace.Bits & mask) == 0 &&
                                !allocatedAdmin.Contains(adminSpace.Space + (uint)i))
                            {
                                notMarked.Add(adminSpace.Space + (uint)i);
                            }
                            else if ((bits & mask) == 0 && (adminSpace.Bits & mask) != 0 &&
                                     !allocatedAdmin.Contains(adminSpace.Space + (uint)i))
                            {
                                notUsed.Add(adminSpace.Space + (uint)i);
                            }
                        }
                    }

                    adminSpace.Bits = bits;
                    model.Dirty = true;
                }
            }

            if (notMarked.Count > 0)
            {
                AddIssue(IssueType.AdminBlockNotMarked, notMarked[0], null,
                    $"{notMarked.Count} administration blocks are used, but not marked used in admin space: {ListBlocks(notMarked)}");
            }

            if (notUsed.Count > 0)
            {
                AddIssue(IssueType.AdminBlockNotUsed, notUsed[0], null,
                    $"{notUsed.Count} administration blocks are marked used in admin space, but not used: {ListBlocks(notUsed)}",
                    true);
            }

            for (var i = 0; i < adminContainers.Count; i++)
            {
                var model = adminContainers[i];
                var previous = i > 0 ? adminContainers[i - 1].Block : 0;
                var next = i < adminContainers.Count - 1 ? adminContainers[i + 1].Block : 0;
                if (model.Container.Previous != previous || model.Container.Next != next)
                {
                    model.Container.Previous = previous;
                    model.Container.Next = next;
                    model.Dirty = true;
                }

                if (model.Dirty)
                {
                    model.Container.OwnBlock = model.Block;
                    ChangeBlock(model.Block, AdminSpaceContainerBuilder.Build(model.Container, volume.BlockSize));
                }
            }

            return Task.CompletedTask;
        }
    }
}
