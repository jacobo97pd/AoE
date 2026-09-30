using System;

namespace Emberfield.Simulation
{
    /// <summary>Authoritative planar position in integer millimetres.</summary>
    [Serializable]
    public struct SimPoint : IEquatable<SimPoint>
    {
        public int X;
        public int Z;
        public SimPoint(int x, int z) { X = x; Z = z; }
        public bool Equals(SimPoint other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is SimPoint other && Equals(other);
        public override int GetHashCode() { unchecked { return (X * 397) ^ Z; } }
        public override string ToString() => $"({X}, {Z}) mm";
        public static bool operator ==(SimPoint a, SimPoint b) => a.Equals(b);
        public static bool operator !=(SimPoint a, SimPoint b) => !a.Equals(b);

        internal static int DistanceCeiling(SimPoint a, SimPoint b)
        {
            long dx = (long)b.X - a.X;
            long dz = (long)b.Z - a.Z;
            ulong value = (ulong)(dx * dx + dz * dz);
            ulong remaining = value;
            ulong root = 0;
            ulong bit = 1UL << 62;
            while (bit > remaining) bit >>= 2;
            while (bit != 0)
            {
                if (remaining >= root + bit)
                {
                    remaining -= root + bit;
                    root = (root >> 1) + bit;
                }
                else root >>= 1;
                bit >>= 2;
            }
            return (int)(root * root == value ? root : root + 1);
        }
    }
}
