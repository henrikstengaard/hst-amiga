namespace Hst.Amiga.RigidDiskBlocks.Partitioning
{
    /// <summary>
    /// Range of cylinders from low cylinder to high cylinder, both inclusive.
    /// </summary>
    public class CylinderRange
    {
        public CylinderRange(uint lowCyl, uint highCyl)
        {
            LowCyl = lowCyl;
            HighCyl = highCyl;
        }

        /// <summary>
        /// First cylinder of range.
        /// </summary>
        public uint LowCyl { get; }

        /// <summary>
        /// Last cylinder of range.
        /// </summary>
        public uint HighCyl { get; }

        /// <summary>
        /// Number of cylinders in range.
        /// </summary>
        public uint Cylinders => HighCyl >= LowCyl ? HighCyl - LowCyl + 1 : 0;

        /// <summary>
        /// Range contains cylinders from low cylinder to high cylinder.
        /// </summary>
        public bool Contains(uint lowCyl, uint highCyl) => lowCyl >= LowCyl && highCyl <= HighCyl;

        public override string ToString() => $"{LowCyl}-{HighCyl}";
    }
}
