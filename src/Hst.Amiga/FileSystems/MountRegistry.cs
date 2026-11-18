namespace Hst.Amiga.FileSystems
{
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using Exceptions;

    /// <summary>
    /// Registry of partitions mounted by file system volumes per stream. Volumes cache and defer writing blocks,
    /// so two volumes mounted on the same partition in the same stream can overwrite each others changes and
    /// read stale blocks. Mounting a partition overlapping a partition already mounted on the same writable stream
    /// throws an exception, unless forced. A mounted partition is released when its volume is disposed.
    /// </summary>
    internal static class MountRegistry
    {
        private static readonly object LockObject = new object();

        private static readonly ConditionalWeakTable<Stream, List<MountRegistration>> Mounts =
            new ConditionalWeakTable<Stream, List<MountRegistration>>();

        /// <summary>
        /// Register mounted partition in stream.
        /// </summary>
        /// <param name="stream">Stream partition is mounted from.</param>
        /// <param name="offset">Byte offset of partition start in stream.</param>
        /// <param name="size">Size of partition in bytes.</param>
        /// <param name="force">Skip check for partitions already mounted.</param>
        /// <returns>Registration, which must be released when volume is disposed.</returns>
        /// <exception cref="PartitionAlreadyMountedException">Partition overlaps partition already mounted.</exception>
        public static MountRegistration Register(Stream stream, long offset, long size, bool force)
        {
            // volumes mounted from read only streams can't write blocks and are not registered
            if (!stream.CanWrite)
            {
                return MountRegistration.None;
            }

            var end = offset + size;

            lock (LockObject)
            {
                var registrations = Mounts.GetOrCreateValue(stream);

                if (!force)
                {
                    foreach (var registration in registrations)
                    {
                        if (offset < registration.End && registration.Offset < end)
                        {
                            throw new PartitionAlreadyMountedException(
                                $"Partition at byte offset {offset} to {end - 1} overlaps partition at byte offset {registration.Offset} to {registration.End - 1} already mounted on the same stream. Dispose the mounted volume or mount with force");
                        }
                    }
                }

                var newRegistration = new MountRegistration(stream, offset, end);
                registrations.Add(newRegistration);
                return newRegistration;
            }
        }

        /// <summary>
        /// Number of partitions mounted in stream.
        /// </summary>
        public static int Count(Stream stream)
        {
            lock (LockObject)
            {
                return Mounts.TryGetValue(stream, out var registrations) ? registrations.Count : 0;
            }
        }

        internal static void Release(MountRegistration registration)
        {
            if (registration.Stream == null)
            {
                return;
            }

            lock (LockObject)
            {
                if (Mounts.TryGetValue(registration.Stream, out var registrations))
                {
                    registrations.Remove(registration);
                }
            }
        }
    }

    /// <summary>
    /// Partition mounted in stream by a volume.
    /// </summary>
    internal sealed class MountRegistration
    {
        public static readonly MountRegistration None = new MountRegistration(null, 0, 0);

        private bool released;

        public MountRegistration(Stream stream, long offset, long end)
        {
            Stream = stream;
            Offset = offset;
            End = end;
        }

        public Stream Stream { get; }
        public long Offset { get; }
        public long End { get; }

        /// <summary>
        /// Release mounted partition, so partition can be mounted again.
        /// </summary>
        public void Release()
        {
            if (released)
            {
                return;
            }

            released = true;
            MountRegistry.Release(this);
        }
    }
}
