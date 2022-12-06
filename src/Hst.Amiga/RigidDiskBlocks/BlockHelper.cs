namespace Hst.Amiga.RigidDiskBlocks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Extensions;

    public static class BlockHelper
    {
        public static FileSystemHeaderBlock CreateFileSystemHeaderBlock(byte[] dosType, int version, int revision,
            string fileSystemName, byte[] fileSystemBytes)
        {
            if (dosType.Length != 4)
            {
                throw new ArgumentException("Dos type must be 4 bytes in length", nameof(dosType));
            }

            var maxSize = 512 - (5 * 4);
            var loadSegBlocks = new List<LoadSegBlock>();
            fileSystemBytes.ChunkBy(maxSize, bytes => loadSegBlocks.Add(CreateLoadSegBlock(bytes.ToArray())));

            return new FileSystemHeaderBlock
            {
                DosType = dosType,
                Version = version,
                Revision = revision,
                LoadSegBlocks = loadSegBlocks,
                FileSystemName = fileSystemName ?? string.Empty,
                FileSystemSize = loadSegBlocks.Sum(x => x.Data.Length)
            };
        }

        public static LoadSegBlock CreateLoadSegBlock(byte[] data)
        {
            return new LoadSegBlock
            {
                Data = data
            };
        }

        /// <summary>
        /// update block pointers to maintain rigid disk block structure. required when changes to rigid disk block needs block pointers updated like adding or deleting partition blocks
        /// </summary>
        /// <param name="rigidDiskBlock"></param>
        public static void UpdateBlockPointers(RigidDiskBlock rigidDiskBlock)
        {
            var highRsdkBlock = rigidDiskBlock.RdbBlockLo;

            var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();

            var partitionBlockIndex = partitionBlocks.Count > 0
                ? highRsdkBlock + 1
                : BlockIdentifiers.EndOfBlock;

            rigidDiskBlock.PartitionList = partitionBlockIndex;

            for (var p = 0; p < partitionBlocks.Count; p++)
            {
                highRsdkBlock++;
                var partitionBlock = partitionBlocks[p];

                partitionBlock.NextPartitionBlock = p < partitionBlocks.Count - 1
                    ? highRsdkBlock + 1
                    : BlockIdentifiers.EndOfBlock;
            }

            var fileSystemHeaderBlocks = rigidDiskBlock.FileSystemHeaderBlocks.ToList();
            var fileSystemHeaderBlockIndex = fileSystemHeaderBlocks.Count > 0
                ? highRsdkBlock + 1
                : BlockIdentifiers.EndOfBlock;

            rigidDiskBlock.FileSysHdrList = fileSystemHeaderBlockIndex;

            for (var f = 0; f < fileSystemHeaderBlocks.Count; f++)
            {
                highRsdkBlock++;
                var fileSystemHeaderBlock = fileSystemHeaderBlocks[f];
                var loadSegBlocks = fileSystemHeaderBlock.LoadSegBlocks.ToList();

                fileSystemHeaderBlock.NextFileSysHeaderBlock = f < fileSystemHeaderBlocks.Count - 1
                    ? (uint)(highRsdkBlock + 1 + loadSegBlocks.Count)
                    : BlockIdentifiers.EndOfBlock;
                fileSystemHeaderBlock.SegListBlocks = (int)(highRsdkBlock + 1);

                for (var l = 0; l < loadSegBlocks.Count; l++)
                {
                    highRsdkBlock++;
                    var loadSegBlock = loadSegBlocks[l];

                    loadSegBlock.NextLoadSegBlock = l < loadSegBlocks.Count - 1
                        ? (int)(highRsdkBlock + 1)
                        : -1;
                }
            }

            var badBlocks = rigidDiskBlock.BadBlocks.ToList();
            
            rigidDiskBlock.BadBlockList = badBlocks.Count > 0 ? highRsdkBlock + 1 : BlockIdentifiers.EndOfBlock;

            for (var b = 0; b < badBlocks.Count; b++)
            {
                highRsdkBlock++;
                var badBlock = badBlocks[b];

                badBlock.NextBadBlock = b < badBlocks.Count - 1
                    ? highRsdkBlock + 1
                    : BlockIdentifiers.EndOfBlock;
            }

            // set highest used rdb block
            rigidDiskBlock.HighRsdkBlock = highRsdkBlock;
        }

        public static void ResetRigidDiskBlockPointers(RigidDiskBlock rigidDiskBlock)
        {
            ResetPartitionBlockPointers(rigidDiskBlock);
            ResetFileSystemHeaderBlockPointers(rigidDiskBlock);
            ResetBadBlockPointers(rigidDiskBlock);
        }

        public static void ResetPartitionBlockPointers(RigidDiskBlock rigidDiskBlock)
        {
            rigidDiskBlock.PartitionList = 0;

            foreach (var partitionBlock in rigidDiskBlock.PartitionBlocks)
            {
                ResetPartitionBlockPointers(partitionBlock);
            }
        }

        public static void ResetPartitionBlockPointers(PartitionBlock partitionBlock)
        {
            partitionBlock.NextPartitionBlock = 0;
        }

        public static void ResetFileSystemHeaderBlockPointers(RigidDiskBlock rigidDiskBlock)
        {
            rigidDiskBlock.FileSysHdrList = 0;

            foreach (var fileSystemHeaderBlock in rigidDiskBlock.FileSystemHeaderBlocks)
            {
                ResetFileSystemHeaderBlockPointers(fileSystemHeaderBlock);
            }
        }

        public static void ResetFileSystemHeaderBlockPointers(FileSystemHeaderBlock fileSystemHeaderBlock)
        {
            fileSystemHeaderBlock.NextFileSysHeaderBlock = 0;
            fileSystemHeaderBlock.SegListBlocks = 0;

            foreach (var loadSegBlock in fileSystemHeaderBlock.LoadSegBlocks)
            {
                ResetLoadSegBlockPointers(loadSegBlock);
            }
        }

        public static void ResetLoadSegBlockPointers(LoadSegBlock loadSegBlock)
        {
            loadSegBlock.NextLoadSegBlock = 0;
        }

        public static void ResetBadBlockPointers(RigidDiskBlock rigidDiskBlock)
        {
            rigidDiskBlock.BadBlockList = 0;

            foreach (var badBlock in rigidDiskBlock.BadBlocks)
            {
                ResetBadBlockPointers(badBlock);
            }
        }

        public static void ResetBadBlockPointers(BadBlock badBlock)
        {
            badBlock.NextBadBlock = 0;
        }
    }
}