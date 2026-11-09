namespace Hst.Amiga.FileSystems.SmartFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Converters;

    /// <summary>
    /// Transactions, see fs/transactions.c in smart file system source.
    /// Smart file system writes changes to metadata blocks as a transaction stored in transaction storage blocks
    /// before writing them in place. If writing is interrupted, the transaction failure block points to the
    /// transaction storage blocks and the transaction must be reapplied when the volume is mounted.
    /// This implementation writes blocks directly and only replays pending transactions.
    /// </summary>
    public static class Transactions
    {
        public class Operation
        {
            public uint Block { get; set; }
            public byte Bits { get; set; }
            public byte[] Data { get; set; }
        }

        /// <summary>
        /// Block number of transaction ok or failure block.
        /// </summary>
        public static uint GetTransactionBlock(RootBlock rootBlock) => rootBlock.RootObjectContainer + 2;

        /// <summary>
        /// Check for and replay pending transaction, checkfortransaction() in smart file system source.
        /// If stream is not writable, replayed blocks are kept in volume overlay.
        /// </summary>
        /// <returns>True if a pending transaction was replayed.</returns>
        public static async Task<bool> CheckForTransaction(Volume volume)
        {
            var transactionBlock = GetTransactionBlock(volume.RootBlock);
            var blockBytes = await Disk.ReadBlock(volume, transactionBlock);

            var ownBlock = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 8);
            if (!ChecksumHelper.IsValid(blockBytes) || ownBlock != transactionBlock)
            {
                throw new IOException($"Invalid checksum or own block for transaction block {transactionBlock}");
            }

            var id = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0);
            if (id != Constants.TransactionFailureId)
            {
                return false;
            }

            var transactionFailureBlock = TransactionBlockParser.ParseFailure(blockBytes);
            var operations = await LoadTransaction(volume, transactionFailureBlock.FirstTransaction);

            foreach (var operation in operations)
            {
                if ((operation.Bits & Constants.OperationDelete) != 0)
                {
                    continue;
                }

                var baseBytes = (operation.Bits & Constants.OperationEmpty) != 0
                    ? new byte[volume.BlockSize]
                    : await Disk.ReadBlock(volume, operation.Block);
                Uncompress(baseBytes, operation.Data);
                await WriteOrOverlay(volume, operation.Block, baseBytes);
            }

            var transactionOkBlock = new TransactionOkBlock { OwnBlock = transactionBlock };
            await WriteOrOverlay(volume, transactionBlock,
                TransactionBlockBuilder.BuildOk(transactionOkBlock, volume.BlockSize));

            volume.Logs.Add($"Pending transaction from block {transactionFailureBlock.FirstTransaction} with {operations.Count} operations was reapplied");
            return true;
        }

        private static async Task WriteOrOverlay(Volume volume, uint block, byte[] blockBytes)
        {
            if (volume.IsReadOnly)
            {
                volume.Overlay[block] = blockBytes;
                return;
            }

            await Disk.WriteBlock(volume, block, blockBytes);
        }

        /// <summary>
        /// Load operations from transaction storage blocks, loadtransaction() in smart file system source.
        /// </summary>
        public static async Task<IList<Operation>> LoadTransaction(Volume volume, uint block)
        {
            // concatenate data from transaction storage blocks, operations can span blocks
            var data = new List<byte>();
            var visited = new HashSet<uint>();
            while (block != 0 && visited.Add(block))
            {
                var storageBlock = TransactionBlockParser.ParseStorage(
                    await Disk.ReadCheckedBlock(volume, block, Constants.TransactionStorageId));
                data.AddRange(storageBlock.Data);
                block = storageBlock.Next;
            }

            return ParseOperations(data.ToArray());
        }

        /// <summary>
        /// Parse operations (struct OperationInformation) from transaction data.
        /// Each operation has length (UWORD), block (ULONG), bits (UBYTE) and compressed data of length bytes
        /// padded so operations starts at even offsets. Length of 0 indicates end of operations.
        /// </summary>
        public static IList<Operation> ParseOperations(byte[] data)
        {
            var operations = new List<Operation>();
            var offset = 0;
            while (offset + 7 <= data.Length)
            {
                var length = BigEndianConverter.ConvertBytesToUInt16(data, offset);
                if (length == 0)
                {
                    break;
                }

                if (offset + 7 + length > data.Length)
                {
                    throw new IOException("Transaction data is truncated");
                }

                var operation = new Operation
                {
                    Block = BigEndianConverter.ConvertBytesToUInt32(data, offset + 2),
                    Bits = data[offset + 6],
                    Data = new byte[length]
                };
                Array.Copy(data, offset + 7, operation.Data, 0, length);
                operations.Add(operation);

                offset += (length | 1) + 7;
            }

            return operations;
        }

        /// <summary>
        /// Uncompress transaction operation data into block, uncompress() with LONGCOMPRESSION in smart file system source.
        /// Each code byte is followed by n = (code &amp; 0x3f) + 1 longs:
        /// 00xxxxxx = clear n longs, 01xxxxxx = skip n longs, 10xxxxxx = set n longs to 0xffffffff,
        /// 11xxxxxx = copy n longs following code byte.
        /// </summary>
        public static void Uncompress(byte[] blockBytes, byte[] data)
        {
            var dest = 0;
            var src = 0;
            while (src < data.Length)
            {
                var code = data[src++];
                var count = ((code & 0x3f) + 1) * 4;

                switch (code & 0xc0)
                {
                    case 0x40:
                        dest += count;
                        break;
                    case 0x80:
                        for (var i = 0; i < count && dest < blockBytes.Length; i++)
                        {
                            blockBytes[dest++] = 0xff;
                        }

                        break;
                    case 0x00:
                        for (var i = 0; i < count && dest < blockBytes.Length; i++)
                        {
                            blockBytes[dest++] = 0;
                        }

                        break;
                    default:
                        for (var i = 0; i < count && dest < blockBytes.Length && src < data.Length; i++)
                        {
                            blockBytes[dest++] = data[src++];
                        }

                        break;
                }
            }
        }
    }
}
