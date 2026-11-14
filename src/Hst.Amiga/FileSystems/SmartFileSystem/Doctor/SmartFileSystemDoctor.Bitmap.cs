namespace Hst.Amiga.FileSystems.SmartFileSystem.Doctor
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;

    /// <summary>
    /// Bitmap and root info.
    /// </summary>
    public partial class SmartFileSystemDoctor
    {
        /// <summary>
        /// Check bitmap against blocks used and rebuild it, if it doesn't match. Blocks used are reserved blocks,
        /// root blocks, bitmap blocks, admin spaces and extents of files. Root info is updated with free blocks and
        /// deleted files in recycled directory.
        /// </summary>
        private async Task FinalizeBitmap()
        {
            ShowMsg("Checking bitmap");

            var used = new BitArray(metadataMap);
            used.Or(dataMap);

            var blocksInBitmap = BitmapBlock.BlocksInBitmap(volume.BlockSize);
            var longs = (volume.BlockSize - Constants.BitmapHeaderSize) / 4;
            var usedMarkedFree = new List<uint>();
            var freeMarkedUsed = new List<uint>();
            var padding = false;
            uint freeBlocks = 0;

            for (var i = 0; i < bitmapBlocks; i++)
            {
                var block = rootBlock.BitmapBase + (uint)i;
                var data = await ReadBlock(block);
                var problem = GetBlockProblem(data, block, Constants.BitmapId);
                var current = problem == null ? BitmapBlockParser.Parse(data) : null;
                if (problem != null)
                {
                    AddIssue(IssueType.BitmapBlockInvalid, block, null,
                        $"Bitmap block is invalid: {problem}, bitmap block is rebuilt");
                }

                var expected = new BitmapBlock(volume.BlockSize) { OwnBlock = block };
                for (var bit = 0; bit < blocksInBitmap; bit++)
                {
                    var blockNumber = (long)i * blocksInBitmap + bit;
                    var mask = 0x80000000U >> (bit & 31);
                    var expectedFree = blockNumber < volume.Blocks && !used[(int)blockNumber];
                    if (expectedFree)
                    {
                        expected.Map[bit >> 5] |= mask;
                        freeBlocks++;
                    }

                    if (current == null)
                    {
                        continue;
                    }

                    var free = (current.Map[bit >> 5] & mask) != 0;
                    if (free == expectedFree)
                    {
                        continue;
                    }

                    if (blockNumber >= volume.Blocks)
                    {
                        padding = true;
                    }
                    else if (free)
                    {
                        // blocks of admin spaces created by repair are free before repair
                        if (!newAdminSpaces.Any(x => blockNumber >= x && blockNumber < x + Constants.BlocksAdmin))
                        {
                            usedMarkedFree.Add((uint)blockNumber);
                        }
                    }
                    else
                    {
                        freeMarkedUsed.Add((uint)blockNumber);
                    }
                }

                if (current == null || !current.Map.Take(longs).SequenceEqual(expected.Map.Take(longs)))
                {
                    ChangeBlock(block, BitmapBlockBuilder.Build(expected, volume.BlockSize));
                }
            }

            if (usedMarkedFree.Count > 0)
            {
                AddIssue(IssueType.BlocksUsedMarkedFree, usedMarkedFree[0], null,
                    $"{usedMarkedFree.Count} blocks are used, but marked free in bitmap: {ListBlocks(usedMarkedFree)}");
            }

            if (freeMarkedUsed.Count > 0)
            {
                AddIssue(IssueType.BlocksFreeMarkedUsed, freeMarkedUsed[0], null,
                    $"{freeMarkedUsed.Count} blocks are not used, but marked used in bitmap: {ListBlocks(freeMarkedUsed)}",
                    true);
            }

            if (padding)
            {
                AddIssue(IssueType.BitmapPadding, rootBlock.BitmapBase + (uint)bitmapBlocks - 1, null,
                    "Bits after last block of partition are not cleared in last bitmap block");
            }

            stats.FreeBlocks = freeBlocks;
            stats.UsedBlocks = volume.Blocks - freeBlocks;

            var objects = AllObjects().ToList();
            stats.Directories = objects.Count(x => x.Object.IsDir && !x.IsRootObject);
            stats.Files = files.Count;
            stats.SoftLinks = objects.Count(x => x.SoftLink != null);

            // root info
            var rootInfo = rootContainer.Container.RootInfo;
            if (rootInfo.FreeBlocks != freeBlocks)
            {
                AddIssue(IssueType.RootInfoMismatch, rootContainer.Block, null,
                    $"Root info free blocks is {rootInfo.FreeBlocks}, expected {freeBlocks}", true);
                rootInfo.FreeBlocks = freeBlocks;
                rootContainer.Dirty = true;
            }

            var recycled = rootObj.Children.FirstOrDefault(x =>
                x.Object.ObjectNode == Constants.RecycledNode && x.Object.IsDir);
            var deletedFiles = recycled?.Children.Where(x => files.Contains(x)).ToList() ?? new List<ObjModel>();
            var deletedBlocks = (uint)deletedFiles.Sum(x => x.Extents.Sum(e => (long)e.Blocks));
            if (rootInfo.DeletedFiles != deletedFiles.Count || rootInfo.DeletedBlocks != deletedBlocks)
            {
                AddIssue(IssueType.RootInfoMismatch, rootContainer.Block, null,
                    $"Root info deleted files {rootInfo.DeletedFiles} and deleted blocks {rootInfo.DeletedBlocks}, expected {deletedFiles.Count} and {deletedBlocks}",
                    true);
                rootInfo.DeletedFiles = (uint)deletedFiles.Count;
                rootInfo.DeletedBlocks = deletedBlocks;
                rootContainer.Dirty = true;
            }
        }
    }
}
