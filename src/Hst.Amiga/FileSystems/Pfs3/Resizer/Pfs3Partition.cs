namespace Hst.Amiga.FileSystems.Pfs3.Resizer
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Doctor;
    using RigidDiskBlocks;
    using RigidDiskBlocks.Partitioning;

    /// <summary>
    /// PFS3 part of moving and resizing a partition.
    /// </summary>
    internal class Pfs3Partition : IPartitionFileSystem
    {
        public static readonly Pfs3Partition Instance = new Pfs3Partition();

        public string Name => "PFS3";

        public bool IsSupported(byte[] dosType) =>
            dosType.Length == 4 && dosType[0] == 'P' && (dosType[1] == 'F' || dosType[1] == 'D') &&
            dosType[2] == 'S';

        public async Task<IList<string>> Check(Stream stream, PartitionBlock partitionBlock,
            CancellationToken cancellationToken)
        {
            var messages = new List<string>();
            var line = new StringBuilder();
            var volume = Pfs3Doctor.OpenVolume(stream, partitionBlock);
            volume.ShowMsg = message =>
            {
                line.Append(message);
                var text = line.ToString();
                int index;
                while ((index = text.IndexOf('\n')) >= 0)
                {
                    messages.Add(text.Substring(0, index));
                    text = text.Substring(index + 1);
                }

                line.Clear();
                line.Append(text);
            };

            var doctor = await Pfs3Doctor.Run(volume, Mode.Check, false, cancellationToken);
            if (line.Length > 0)
            {
                messages.Add(line.ToString());
            }

            var errors = messages.Where(x => x.StartsWith("ERROR")).ToList();
            if (doctor.Result != Error.None)
            {
                errors.Add($"Doctor result {doctor.Result}");
            }
            else if (doctor.Stats.NumErrors > 0 && errors.Count == 0)
            {
                errors.Add($"Doctor found {doctor.Stats.NumErrors} errors");
            }

            return errors;
        }

        /// <summary>
        /// PFS3 blocks are relative to start of partition, nothing to update after move.
        /// </summary>
        public Task AfterMove(Stream stream, PartitionBlock partitionBlock, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ResizeInPlace(Stream stream, PartitionBlock partitionBlock, uint newCylinders, bool dryRun,
            IList<string> messages, CancellationToken cancellationToken) =>
            Pfs3Resizer.ResizeInPlace(stream, partitionBlock, newCylinders, dryRun, messages, cancellationToken);
    }
}
