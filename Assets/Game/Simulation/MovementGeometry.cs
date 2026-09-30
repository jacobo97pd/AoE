using System;

namespace Emberfield.Simulation
{
    internal static class MovementGeometry
    {
        internal static bool DiscsOverlap(SimPoint a, int ar, SimPoint b, int br)
        {
            long dx = (long)a.X - b.X, dz = (long)a.Z - b.Z, radius = (long)ar + br;
            return dx * dx + dz * dz < radius * radius;
        }

        internal static bool SweptDiscsOverlap(SimPoint a0, SimPoint a1, int ar, SimPoint b0, SimPoint b1, int br)
        {
            long dx = (long)a0.X - b0.X, dz = (long)a0.Z - b0.Z;
            long vx = (long)a1.X - a0.X - b1.X + b0.X, vz = (long)a1.Z - a0.Z - b1.Z + b0.Z;
            return SegmentNearOrigin(dx, dz, vx, vz, (long)ar + br);
        }

        private static bool SegmentNearOrigin(long dx, long dz, long vx, long vz, long radius)
        {
            long distance = dx * dx + dz * dz, radiusSquared = radius * radius;
            if (distance < radiusSquared) return true;
            long velocity = vx * vx + vz * vz;
            if (velocity == 0) return false;
            long projection = dx * vx + dz * vz;
            if (projection >= 0) return false;
            if (-projection >= velocity)
            { dx += vx; dz += vz; return dx * dx + dz * dz < radiusSquared; }
            if (distance <= long.MaxValue / velocity && radiusSquared <= long.MaxValue / velocity && Math.Abs(projection) <= 3037000499L)
                return distance * velocity - projection * projection < radiusSquared * velocity;
            return (decimal)distance * velocity - (decimal)projection * projection < (decimal)radiusSquared * velocity;
        }

        internal static bool SweptDiscRectangle(SimPoint from, SimPoint to, int radius, int left, int bottom, int right, int top)
        {
            if (PointRectangle(from, radius, left, bottom, right, top) || PointRectangle(to, radius, left, bottom, right, top)) return true;
            var a = new SimPoint(left, bottom); var b = new SimPoint(right, bottom);
            var c = new SimPoint(right, top); var d = new SimPoint(left, top);
            if (Intersect(from, to, a, b) || Intersect(from, to, b, c) || Intersect(from, to, c, d) || Intersect(from, to, d, a)) return true;
            long vx = (long)to.X - from.X, vz = (long)to.Z - from.Z;
            return SegmentNearOrigin((long)from.X - left, (long)from.Z - bottom, vx, vz, radius) ||
                SegmentNearOrigin((long)from.X - right, (long)from.Z - bottom, vx, vz, radius) ||
                SegmentNearOrigin((long)from.X - right, (long)from.Z - top, vx, vz, radius) ||
                SegmentNearOrigin((long)from.X - left, (long)from.Z - top, vx, vz, radius);
        }

        private static bool PointRectangle(SimPoint point, int radius, int left, int bottom, int right, int top)
        {
            long dx = point.X - Math.Max(left, Math.Min(point.X, right));
            long dz = point.Z - Math.Max(bottom, Math.Min(point.Z, top));
            return dx * dx + dz * dz < (long)radius * radius;
        }

        private static long Cross(SimPoint a, SimPoint b, SimPoint c) =>
            ((long)b.X - a.X) * ((long)c.Z - a.Z) - ((long)b.Z - a.Z) * ((long)c.X - a.X);

        private static bool Intersect(SimPoint a, SimPoint b, SimPoint c, SimPoint d)
        {
            if (Math.Max(a.X, b.X) < Math.Min(c.X, d.X) || Math.Min(a.X, b.X) > Math.Max(c.X, d.X) ||
                Math.Max(a.Z, b.Z) < Math.Min(c.Z, d.Z) || Math.Min(a.Z, b.Z) > Math.Max(c.Z, d.Z)) return false;
            long abC = Cross(a, b, c), abD = Cross(a, b, d), cdA = Cross(c, d, a), cdB = Cross(c, d, b);
            return (abC == 0 || abD == 0 || Math.Sign(abC) != Math.Sign(abD)) &&
                   (cdA == 0 || cdB == 0 || Math.Sign(cdA) != Math.Sign(cdB));
        }

        internal static SimPoint Step(SimPoint from, SimPoint target, int budget)
        {
            int length = SimPoint.DistanceCeiling(from, target);
            if (length <= budget) return target;
            if (budget <= 0) return from;
            long dx = (long)target.X - from.X, dz = (long)target.Z - from.Z;
            int x = (int)(dx * budget / length), z = (int)(dz * budget / length);
            if (x == 0 && z == 0) { if (Math.Abs(dx) >= Math.Abs(dz)) x = Math.Sign(dx); else z = Math.Sign(dz); }
            return new SimPoint(from.X + x, from.Z + z);
        }
    }
}
