namespace Hst.Amiga.FileSystems.SmartFileSystem.Doctor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;

    /// <summary>
    /// Root blocks and transactions.
    /// </summary>
    public partial class SmartFileSystemDoctor
    {
        /// <summary>
        /// Check root blocks at first and last block of partition. Smart file system requires both root blocks to be
        /// valid, so an invalid root block is rebuilt from the valid root block. If both root blocks are invalid, the
        /// volume can't be checked.
        /// </summary>
        /// <returns>True, if a valid root block was found.</returns>
        private async Task<bool> CheckRootBlocks()
        {
            shiftsBlock32 = SmartFileSystemHelper.Log2(volume.BlockSize) - Constants.BlckFAccuracy;
            bitmapBlocks = Bitmap.CalculateBitmapBlocks(volume.Blocks, volume.BlockSize);

            var lastBlock = volume.Blocks - 1;
            var first = await ReadBlock(0);
            var last = await ReadBlock(lastBlock);
            var firstProblem = GetRootBlockProblem(first, 0);
            var lastProblem = GetRootBlockProblem(last, lastBlock);

            if (firstProblem != null && lastProblem != null)
            {
                var id = first == null ? 0 : GetUInt32(first, 0);
                if (id == Constants.DosTypeSfs2)
                {
                    AddIssue(IssueType.NotSfsDisk, 0, null,
                        "Smart file system dos type SFS\\2 is not supported", fixable: false);
                }
                else if (id != Constants.RootBlockId && (last == null || GetUInt32(last, 0) != Constants.RootBlockId))
                {
                    AddIssue(IssueType.NotSfsDisk, 0, null,
                        $"Volume is not a smart file system volume, block 0 has id '{BlockHelper.FormatId(id)}'",
                        fixable: false);
                }
                else
                {
                    AddIssue(IssueType.RootBlockInvalid, 0, null, $"Root block is invalid: {firstProblem}",
                        fixable: false);
                    AddIssue(IssueType.RootBlockInvalid, lastBlock, null, $"Root block is invalid: {lastProblem}",
                        fixable: false);
                }

                return false;
            }

            var firstRoot = firstProblem == null ? RootBlockParser.Parse(first) : null;
            var lastRoot = lastProblem == null ? RootBlockParser.Parse(last) : null;

            if (firstRoot != null && lastRoot != null && !RootBlocksEqual(firstRoot, lastRoot))
            {
                // use the root block with most valid structure pointers
                var firstScore = await ScoreRootBlock(firstRoot);
                var lastScore = await ScoreRootBlock(lastRoot);
                if (lastScore > firstScore)
                {
                    AddIssue(IssueType.RootBlockMismatch, 0, null,
                        $"Root block doesn't match root block at block {lastBlock}, root block is rebuilt from root block at block {lastBlock}");
                    firstRoot = null;
                }
                else
                {
                    AddIssue(IssueType.RootBlockMismatch, lastBlock, null,
                        "Root block doesn't match root block at block 0, root block is rebuilt from root block at block 0");
                    lastRoot = null;
                }
            }
            else
            {
                if (firstRoot == null)
                {
                    AddIssue(IssueType.RootBlockInvalid, 0, null,
                        $"Root block is invalid: {firstProblem}, root block is rebuilt from root block at block {lastBlock}");
                }

                if (lastRoot == null)
                {
                    AddIssue(IssueType.RootBlockInvalid, lastBlock, null,
                        $"Root block is invalid: {lastProblem}, root block is rebuilt from root block at block 0");
                }
            }

            rootBlock = firstRoot ?? lastRoot;
            caseSensitive = rootBlock.CaseSensitive;

            // reserved areas must be zero
            if (firstRoot != null && !IsRootBlockReservedZero(first))
            {
                AddIssue(IssueType.RootBlockInvalid, 0, null, "Root block reserved areas are not zero", true);
                firstRoot = null;
            }

            if (lastRoot != null && !IsRootBlockReservedZero(last))
            {
                AddIssue(IssueType.RootBlockInvalid, lastBlock, null, "Root block reserved areas are not zero", true);
                lastRoot = null;
            }

            if (firstRoot == null)
            {
                WriteRootBlock(0);
            }

            if (lastRoot == null)
            {
                WriteRootBlock(lastBlock);
            }

            if (rootBlock.FirstByte != (ulong)volume.PartitionStartOffset)
            {
                // smart file system on AROS only checks partition size, so this is not an issue
                ShowMsg($"Root block first byte {rootBlock.FirstByte} doesn't match partition start offset {volume.PartitionStartOffset}, partition was moved or is an image of a partition");
            }

            return true;
        }

        private void WriteRootBlock(uint block)
        {
            var ownBlock = rootBlock.OwnBlock;
            rootBlock.OwnBlock = block;
            ChangeBlock(block, RootBlockBuilder.Build(rootBlock, volume.BlockSize));
            rootBlock.OwnBlock = ownBlock;
        }

        /// <summary>
        /// Get problem with root block, checks done when smart file system mounts a volume. Returns null, if root
        /// block is valid.
        /// </summary>
        private string GetRootBlockProblem(byte[] data, uint block)
        {
            var problem = GetBlockProblem(data, block, Constants.RootBlockId);
            if (problem != null)
            {
                return problem;
            }

            var root = RootBlockParser.Parse(data);
            if (root.Version != Constants.StructureVersion)
            {
                return $"structure version is {root.Version}, only version {Constants.StructureVersion} is supported";
            }

            if (root.BlockSize != volume.BlockSize)
            {
                return $"block size is {root.BlockSize}, expected {volume.BlockSize}";
            }

            if (root.TotalBlocks != volume.Blocks)
            {
                return $"total blocks is {root.TotalBlocks}, expected {volume.Blocks}";
            }

            if (root.LastByte < root.FirstByte || root.LastByte - root.FirstByte != (ulong)volume.PartitionSize)
            {
                return $"partition size {root.LastByte - root.FirstByte} from first and last byte doesn't match partition size {volume.PartitionSize}";
            }

            if (root.BitmapBase == 0 || (ulong)root.BitmapBase + (ulong)bitmapBlocks > volume.Blocks)
            {
                return $"bitmap base {root.BitmapBase} is invalid";
            }

            foreach (var (name, pointer) in new[]
                     {
                         ("admin space container", root.AdminSpaceContainer),
                         ("root object container", root.RootObjectContainer),
                         ("extent B-tree root", root.ExtentBNodeRoot),
                         ("object node root", root.ObjectNodeRoot)
                     })
            {
                if (pointer == 0 || pointer >= volume.Blocks - 1)
                {
                    return $"{name} {pointer} is outside partition";
                }
            }

            if (root.RootObjectContainer + 2 >= volume.Blocks)
            {
                return $"root object container {root.RootObjectContainer} is invalid";
            }

            var structures = new[]
            {
                root.AdminSpaceContainer, root.RootObjectContainer, root.RootObjectContainer + 2, root.ExtentBNodeRoot,
                root.ObjectNodeRoot
            };
            if (structures.Distinct().Count() != structures.Length ||
                structures.Any(x => x >= root.BitmapBase && x < root.BitmapBase + bitmapBlocks))
            {
                return "admin space container, root object container, transaction block, extent B-tree root or object node root use the same block or bitmap blocks";
            }

            return null;
        }

        /// <summary>
        /// Claim blocks of structures with fixed location: root object container, transaction block, extent B-tree
        /// root and object node root. These blocks can't be used by other structures, e.g. a corrupt pointer in the
        /// object node tree.
        /// </summary>
        private void ClaimRootStructures()
        {
            ClaimAdmin(rootBlock.RootObjectContainer);
            ClaimAdmin(Transactions.GetTransactionBlock(rootBlock));
            ClaimAdmin(rootBlock.ExtentBNodeRoot);
            ClaimAdmin(rootBlock.ObjectNodeRoot);
        }

        private bool IsRootBlockReservedZero(byte[] data)
        {
            bool IsZero(int offset, int length)
            {
                for (var i = offset; i < offset + length; i++)
                {
                    if (data[i] != 0)
                    {
                        return false;
                    }
                }

                return true;
            }

            return IsZero(0x15, 0x18 - 0x15 + 8) && IsZero(0x38, 0x60 - 0x38) &&
                   IsZero(0x74, volume.BlockSize - 0x74);
        }

        private static bool RootBlocksEqual(RootBlock a, RootBlock b) =>
            a.Version == b.Version && a.SequenceNumber == b.SequenceNumber && a.DateCreated == b.DateCreated &&
            a.Bits == b.Bits && a.FirstByte == b.FirstByte && a.LastByte == b.LastByte &&
            a.TotalBlocks == b.TotalBlocks && a.BlockSize == b.BlockSize && a.BitmapBase == b.BitmapBase &&
            a.AdminSpaceContainer == b.AdminSpaceContainer && a.RootObjectContainer == b.RootObjectContainer &&
            a.ExtentBNodeRoot == b.ExtentBNodeRoot && a.ObjectNodeRoot == b.ObjectNodeRoot;

        /// <summary>
        /// Count valid structures root block points to.
        /// </summary>
        private async Task<int> ScoreRootBlock(RootBlock root)
        {
            var score = 0;
            foreach (var (block, id) in new[]
                     {
                         (root.BitmapBase, Constants.BitmapId),
                         (root.AdminSpaceContainer, Constants.AdminSpaceContainerId),
                         (root.RootObjectContainer, Constants.ObjectContainerId),
                         (root.ExtentBNodeRoot, Constants.BNodeContainerId),
                         (root.ObjectNodeRoot, Constants.NodeContainerId)
                     })
            {
                if (IsValidBlock(await ReadBlock(block), block, id))
                {
                    score++;
                }
            }

            return score;
        }

        /// <summary>
        /// Check transaction block. An unfinished transaction is replayed the same way smart file system does when
        /// it mounts the volume. The scan continues on the replayed blocks, so check mode reports the state of the
        /// volume after it's mounted.
        /// </summary>
        private async Task CheckTransaction()
        {
            var transactionBlock = Transactions.GetTransactionBlock(rootBlock);
            ClaimAdmin(transactionBlock);

            var data = await ReadBlock(transactionBlock);
            var id = GetUInt32(data, 0);
            if (!ChecksumHelper.IsValid(data) || GetUInt32(data, 8) != transactionBlock ||
                (id != Constants.TransactionOkId && id != Constants.TransactionFailureId))
            {
                AddIssue(IssueType.TransactionBlockInvalid, transactionBlock, null,
                    $"Transaction block is invalid, id '{BlockHelper.FormatId(id)}', smart file system can't mount volume. Transaction ok block is written");
                WriteTransactionOk(transactionBlock);
                return;
            }

            if (id == Constants.TransactionOkId)
            {
                return;
            }

            var firstTransaction = GetUInt32(data, 0xc);
            var (operations, problem) = await LoadTransaction(firstTransaction);
            if (problem != null)
            {
                AddIssue(IssueType.TransactionInvalid, transactionBlock, null,
                    $"Unfinished transaction can't be loaded: {problem}, smart file system can't mount volume. Transaction is discarded");
                WriteTransactionOk(transactionBlock);
                return;
            }

            AddIssue(IssueType.TransactionPending, transactionBlock, null,
                $"Unfinished transaction with {operations.Count} operations from block {firstTransaction}, transaction is replayed",
                true);

            foreach (var operation in operations)
            {
                if ((operation.Bits & Constants.OperationDelete) != 0)
                {
                    continue;
                }

                var blockBytes = (operation.Bits & Constants.OperationEmpty) != 0
                    ? new byte[volume.BlockSize]
                    : await ReadBlock(operation.Block);
                Transactions.Uncompress(blockBytes, operation.Data);

                // replayed blocks contain checksums, so they are stored as is
                changedBlocks[operation.Block] = blockBytes;
            }

            WriteTransactionOk(transactionBlock);
        }

        private void WriteTransactionOk(uint transactionBlock)
        {
            ChangeBlock(transactionBlock,
                TransactionBlockBuilder.BuildOk(new TransactionOkBlock { OwnBlock = transactionBlock },
                    volume.BlockSize));
        }

        /// <summary>
        /// Load operations of an unfinished transaction from transaction storage blocks.
        /// </summary>
        private async Task<(IList<Transactions.Operation>, string)> LoadTransaction(uint block)
        {
            var data = new List<byte>();
            var visited = new HashSet<uint>();
            while (block != 0)
            {
                if (!visited.Add(block))
                {
                    return (null, $"transaction storage block chain loops at block {block}");
                }

                var blockBytes = await ReadBlock(block);
                var problem = GetBlockProblem(blockBytes, block, Constants.TransactionStorageId);
                if (problem != null)
                {
                    return (null, $"transaction storage {problem}");
                }

                var storage = TransactionBlockParser.ParseStorage(blockBytes);
                data.AddRange(storage.Data);
                block = storage.Next;
            }

            IList<Transactions.Operation> operations;
            try
            {
                operations = Transactions.ParseOperations(data.ToArray());
            }
            catch (IOException e)
            {
                return (null, e.Message);
            }

            foreach (var operation in operations)
            {
                if (operation.Block >= volume.Blocks)
                {
                    return (null, $"operation changes block {operation.Block} outside partition");
                }
            }

            return (operations, null);
        }
    }
}
