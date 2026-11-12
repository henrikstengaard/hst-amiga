namespace Hst.Amiga.RigidDiskBlocks.Recovery
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Resolves rigid disk block geometry (heads and sectors per track) where all file systems found on disk
    /// start and end at cylinder boundaries.
    /// </summary>
    public static class GeometryResolver
    {
        /// <summary>
        /// Default geometry used by rigid disk block.
        /// </summary>
        public static readonly (uint Heads, uint Sectors) DefaultGeometry = (16, 63);

        /// <summary>
        /// Commonly used geometries, tried in order when no preferred geometry fits.
        /// </summary>
        private static readonly (uint Heads, uint Sectors)[] KnownGeometries =
        {
            DefaultGeometry,
            (255, 63),
            (15, 63),
            (16, 32),
            (8, 32),
            (4, 32),
            (2, 32),
            (1, 63),
            (1, 32)
        };

        public static bool IsSane(uint heads, uint sectors) =>
            heads > 0 && sectors > 0 && heads <= 65535 && sectors <= 65535 && (ulong)heads * sectors <= 1048576;

        /// <summary>
        /// Resolve geometry.
        /// </summary>
        /// <param name="fileSystems">File systems found on disk.</param>
        /// <param name="preferredGeometries">Preferred geometries tried first, e.g. from existing rigid disk block
        /// and partition blocks.</param>
        /// <param name="result">Result to add messages to.</param>
        public static (uint Heads, uint Sectors) Resolve(IList<FoundFileSystem> fileSystems,
            IEnumerable<(uint Heads, uint Sectors)> preferredGeometries, RecoveryResult result)
        {
            var preferred = preferredGeometries.Where(x => IsSane(x.Heads, x.Sectors)).Distinct().ToList();

            // use preferred geometry fitting most file systems. file systems not fitting preferred geometry, e.g.
            // a partition shifted from cylinder boundary, gets partition geometry of its own
            var bestPreferred = preferred
                .Select(x => new { Geometry = x, Count = fileSystems.Count(f => Fits(new[] { f }, x.Heads * x.Sectors)) })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();

            if (bestPreferred != null && (bestPreferred.Count > 0 || fileSystems.Count == 0))
            {
                if (bestPreferred.Count < fileSystems.Count)
                {
                    result.Warning(
                        $"{fileSystems.Count - bestPreferred.Count} file system(s) don't fit geometry with {bestPreferred.Geometry.Heads} heads and {bestPreferred.Geometry.Sectors} sectors from existing blocks");
                }

                return bestPreferred.Geometry;
            }

            foreach (var geometry in KnownGeometries)
            {
                if (Fits(fileSystems, geometry.Heads * geometry.Sectors))
                {
                    return geometry;
                }
            }

            // calculate greatest common divisor of start and exact sizes of file systems
            ulong gcd = 0;
            foreach (var fileSystem in fileSystems)
            {
                gcd = Gcd(gcd, (ulong)fileSystem.StartSector);
                if (fileSystem.IsExactSize)
                {
                    gcd = Gcd(gcd, (ulong)fileSystem.Sectors);
                }
            }

            if (gcd > 0)
            {
                for (uint heads = 16; heads >= 1; heads--)
                {
                    if (gcd % heads != 0 || gcd / heads > 65535)
                    {
                        continue;
                    }

                    var geometry = (heads, (uint)(gcd / heads));
                    result.Warning(
                        $"No known geometry fits file systems found, using geometry with {geometry.Item1} heads and {geometry.Item2} sectors calculated from file system offsets");
                    return geometry;
                }
            }

            var fallback = preferred.Count > 0 ? preferred[0] : DefaultGeometry;
            if (fileSystems.Count > 0)
            {
                result.Warning(
                    $"No geometry fits file systems found, using geometry with {fallback.Heads} heads and {fallback.Sectors} sectors");
            }

            return fallback;
        }

        /// <summary>
        /// Geometry fits file systems, if they all start at a cylinder boundary and their size can be a whole
        /// number of cylinders.
        /// </summary>
        public static bool Fits(IEnumerable<FoundFileSystem> fileSystems, long cylinderBlocks)
        {
            return cylinderBlocks > 0 && fileSystems.All(x =>
                x.StartSector % cylinderBlocks == 0 && SnapSize(x, cylinderBlocks) > 0);
        }

        /// <summary>
        /// Snap size of file systems to whole cylinders.
        /// </summary>
        public static void Snap(IEnumerable<FoundFileSystem> fileSystems, long cylinderBlocks, RecoveryResult result)
        {
            foreach (var fileSystem in fileSystems)
            {
                var sectors = SnapSize(fileSystem, cylinderBlocks);
                if (sectors > 0)
                {
                    fileSystem.Sectors = sectors;
                    fileSystem.MaxSectors = sectors;
                    continue;
                }

                if (fileSystem.StartSector % cylinderBlocks != 0 || fileSystem.Sectors % cylinderBlocks != 0)
                {
                    result.Warning($"{fileSystem} doesn't start or end at a cylinder boundary");
                }
            }
        }

        /// <summary>
        /// Smallest size within file system size range, which is a whole number of cylinders. Returns 0, if no
        /// size in range is a whole number of cylinders.
        /// </summary>
        private static long SnapSize(FoundFileSystem fileSystem, long cylinderBlocks)
        {
            var sectors = (fileSystem.Sectors + cylinderBlocks - 1) / cylinderBlocks * cylinderBlocks;
            return sectors > 0 && sectors <= fileSystem.MaxSectors ? sectors : 0;
        }

        private static ulong Gcd(ulong a, ulong b)
        {
            while (b != 0)
            {
                var t = a % b;
                a = b;
                b = t;
            }

            return a;
        }
    }
}
