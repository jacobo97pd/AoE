using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>
    /// Every accepted order, in the tick it was accepted. The rules are deterministic, so a match is fully
    /// described by the world it started from plus this list: replaying it rebuilds the same battlefield
    /// down to the last hit point. That is what a saved game is here, and it costs a few kilobytes rather
    /// than a dump of every entity, which would have to be revisited every time a field is added.
    ///
    /// Orders from the offline opponent are recorded alongside the player's, so a reload does not depend on
    /// the opponent reasoning its way to the same decisions a second time.
    /// </summary>
    public sealed class MatchJournal
    {
        public const int Capacity = 250000;

        public readonly struct Entry
        {
            public readonly long Tick;
            public readonly IGameCommand Command;
            public Entry(long tick, IGameCommand command) { Tick = tick; Command = command; }
        }

        private readonly List<Entry> entries = new List<Entry>();
        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>True once orders have been dropped, which makes the journal no longer a faithful record.</summary>
        public bool Overflowed { get; private set; }

        internal void Record(long tick, IGameCommand command)
        {
            if (command == null) return;
            if (entries.Count >= Capacity) { Overflowed = true; return; }
            entries.Add(new Entry(tick, command));
        }

        public void Clear() { entries.Clear(); Overflowed = false; }
    }
}
