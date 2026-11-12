namespace Hst.Amiga.RigidDiskBlocks.Recovery
{
    using System.Collections.Generic;
    using System.Linq;

    public class RecoveryResult
    {
        /// <summary>
        /// Mode used for recovery.
        /// </summary>
        public RecoveryMode Mode { get; set; }

        /// <summary>
        /// Recovered rigid disk block. Null, if rigid disk block could not be recovered.
        /// </summary>
        public RigidDiskBlock RigidDiskBlock { get; set; }

        /// <summary>
        /// File systems found on disk.
        /// </summary>
        public IList<FoundFileSystem> FileSystems { get; set; } = new List<FoundFileSystem>();

        public IList<RecoveryMessage> Messages { get; } = new List<RecoveryMessage>();

        /// <summary>
        /// Recovered rigid disk block differs from rigid disk block on disk.
        /// </summary>
        public bool Modified { get; set; }

        /// <summary>
        /// Recovered rigid disk block was written to disk.
        /// </summary>
        public bool Written { get; set; }

        /// <summary>
        /// Rigid disk block was recovered without errors and can be written.
        /// </summary>
        public bool IsValid => RigidDiskBlock != null && Messages.All(x => x.Level != RecoveryMessageLevel.Error);

        internal void Info(string text) => Messages.Add(new RecoveryMessage(RecoveryMessageLevel.Info, text));
        internal void Warning(string text) => Messages.Add(new RecoveryMessage(RecoveryMessageLevel.Warning, text));
        internal void Error(string text) => Messages.Add(new RecoveryMessage(RecoveryMessageLevel.Error, text));
    }
}
