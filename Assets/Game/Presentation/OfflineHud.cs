using System.Text;
using Emberfield.Simulation;

namespace Emberfield.Presentation
{
    public sealed partial class MatchHud
    {
        private readonly StringBuilder objectiveText = new StringBuilder(256);
        // What the line last showed: its counts and its clocks in whole seconds. It is composed again only when one moves.
        private readonly long[] shownObjective = new long[5];
        private void RefreshOfflineObjective()
        {
            var state = match.World.Match;
            int ours = 0, theirs = 0, contested = 0;
            if (state.Mode != VictoryMode.Conquest)
            {
                var objectives = state.Objectives;
                for (int i = 0; i < objectives.Count; i++)
                {
                    var point = objectives[i];
                    if (point.OwnerId == 1) ours++; else if (point.OwnerId == 2) theirs++;
                    if (point.IsContested || point.CapturingPlayerId != 0 && point.OwnerId != point.CapturingPlayerId) contested++;
                }
            }
            bool afk = match.World.IsNetworkReplica && match.Online.State != null && !state.IsFinished && match.Online.State.afkSecondsRemaining <= 60;
            long conquest = state.Mode == VictoryMode.Conquest ? 1 : 0;
            long first = conquest == 1 ? 0 : state.GetHoldTicks(1) / World.TickRate, second = conquest == 1 ? 0 : state.GetHoldTicks(2) / World.TickRate;
            long hold = conquest == 1 ? 0 : state.ContinuousHoldTicks / World.TickRate, waiting = afk ? match.Online.State.afkSecondsRemaining : -1;
            long counts = conquest + (ours + 1L) * 2 + (theirs + 1L) * 2048 + (contested + 1L) * 2097152;
            if (!rewrite && shownObjective[0] == counts && shownObjective[1] == first && shownObjective[2] == second &&
                shownObjective[3] == hold && shownObjective[4] == waiting) return;
            shownObjective[0] = counts; shownObjective[1] = first; shownObjective[2] = second; shownObjective[3] = hold; shownObjective[4] = waiting;
            objectiveText.Clear();
            if (state.Mode == VictoryMode.Conquest)
                objectiveText.Append("CONQUEST  ·  Scout, build an army and destroy every enemy Hearth. Protect yours.");
            else
                objectiveText.Append("BEACONS  ·  You ").Append(ours).Append(" / Rival ").Append(theirs).Append("  ·  Contested ").Append(contested)
                    .Append("  ·  Hold clock  ").Append(Emberfield.Presentation.OfflinePanel.Clock(state.GetHoldTicks(1))).Append(" : ").Append(Emberfield.Presentation.OfflinePanel.Clock(state.GetHoldTicks(2))).Append(" / ").Append(Emberfield.Presentation.OfflinePanel.Clock(state.ContinuousHoldTicks));
            if (afk)
                objectiveText.Append("  /  AFK: issue an order within ").Append(match.Online.State.afkSecondsRemaining).Append("s to avoid forfeit.");
            Show(objectiveStatus, objectiveText.ToString());
        }
    }
}
