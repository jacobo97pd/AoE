using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>
    /// Route storage recycled between searches. A unit holds the only reference to its route, so the
    /// list it drops is cleared and handed to the next search instead of becoming garbage: steady
    /// movement, chasing and gathering then allocate nothing. Contents are always rebuilt from empty,
    /// so which list a route lives in never affects the simulation. One pool per thread.
    /// </summary>
    internal static class RouteLists
    {
        private const int Capacity = 1024;
        [ThreadStatic] private static List<List<SimPoint>> spare;

        internal static List<SimPoint> Rent()
        {
            var lists = spare;
            if (lists == null || lists.Count == 0) return new List<SimPoint>();
            var list = lists[lists.Count - 1];
            lists.RemoveAt(lists.Count - 1);
            return list;
        }

        /// <summary>Takes back a route nothing references any more.</summary>
        internal static void Return(List<SimPoint> list)
        {
            if (list == null) return;
            var lists = spare ?? (spare = new List<List<SimPoint>>(64));
            if (lists.Count == Capacity) return;
            list.Clear();
            lists.Add(list);
        }
    }
}
