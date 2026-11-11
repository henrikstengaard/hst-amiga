namespace Hst.Amiga.FileSystems.SmartFileSystem.Blocks
{
    using System;
    using Core.Converters;

    /// <summary>
    /// Transaction ok block ("TROK") stored at root object container + 2, indicates no pending transaction.
    /// </summary>
    public class TransactionOkBlock : Block
    {
        public TransactionOkBlock() : base(Constants.TransactionOkId)
        {
        }
    }

    /// <summary>
    /// Transaction failure block (struct fsTransactionFailure, "TRFA") stored at root object container + 2,
    /// indicates a transaction was interrupted and must be reapplied.
    /// </summary>
    public class TransactionFailureBlock : Block
    {
        /// <summary>
        /// First transaction storage block.
        /// </summary>
        public uint FirstTransaction { get; set; }

        public TransactionFailureBlock() : base(Constants.TransactionFailureId)
        {
        }
    }

    /// <summary>
    /// Transaction storage block (struct fsTransactionStorage, "TRST") containing transaction data.
    /// </summary>
    public class TransactionStorageBlock : Block
    {
        /// <summary>
        /// Next transaction storage block, 0 if last.
        /// </summary>
        public uint Next { get; set; }

        public byte[] Data { get; set; }

        public TransactionStorageBlock() : base(Constants.TransactionStorageId)
        {
            Data = Array.Empty<byte>();
        }
    }

    public static class TransactionBlockParser
    {
        public static TransactionFailureBlock ParseFailure(byte[] blockBytes)
        {
            var block = new TransactionFailureBlock();
            BlockHelper.ReadHeader(blockBytes, block, Constants.TransactionFailureId);
            block.FirstTransaction = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc);
            return block;
        }

        public static TransactionStorageBlock ParseStorage(byte[] blockBytes)
        {
            var block = new TransactionStorageBlock();
            BlockHelper.ReadHeader(blockBytes, block, Constants.TransactionStorageId);
            block.Next = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc);
            block.Data = new byte[blockBytes.Length - Constants.TransactionStorageHeaderSize];
            Array.Copy(blockBytes, Constants.TransactionStorageHeaderSize, block.Data, 0, block.Data.Length);
            return block;
        }
    }

    public static class TransactionBlockBuilder
    {
        public static byte[] BuildOk(TransactionOkBlock block, int blockSize)
        {
            var blockBytes = new byte[blockSize];
            BlockHelper.WriteHeader(blockBytes, block);
            return BlockHelper.UpdateChecksum(blockBytes, block);
        }

        public static byte[] BuildFailure(TransactionFailureBlock block, int blockSize)
        {
            var blockBytes = new byte[blockSize];
            BlockHelper.WriteHeader(blockBytes, block);
            BigEndianConverter.ConvertUInt32ToBytes(block.FirstTransaction, blockBytes, 0xc);
            return BlockHelper.UpdateChecksum(blockBytes, block);
        }

        public static byte[] BuildStorage(TransactionStorageBlock block, int blockSize)
        {
            var blockBytes = new byte[blockSize];
            BlockHelper.WriteHeader(blockBytes, block);
            BigEndianConverter.ConvertUInt32ToBytes(block.Next, blockBytes, 0xc);
            Array.Copy(block.Data, 0, blockBytes, Constants.TransactionStorageHeaderSize,
                Math.Min(block.Data.Length, blockSize - Constants.TransactionStorageHeaderSize));
            return BlockHelper.UpdateChecksum(blockBytes, block);
        }
    }
}
