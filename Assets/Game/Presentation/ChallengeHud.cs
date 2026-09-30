namespace Emberfield.Presentation
{
    public sealed partial class MatchHud
    {
        /// <summary>
        /// One line above the battlefield: what the exercise asks for, how far along it is and how long
        /// is left. When it ends, the same line carries the verdict instead of a clock, because a player
        /// who has just run out of time should not have to look anywhere else to find that out.
        /// </summary>
        private void RefreshChallengeObjective()
        {
            var run = match.Challenge;
            objectiveText.Clear();
            objectiveText.Append("CHALLENGE  ·  ");
            if (run.State == ChallengeState.Won) objectiveText.Append("Complete. ").Append(run.Definition.Name).Append(" is done.");
            else if (run.State == ChallengeState.Lost) objectiveText.Append("Out of time. Open the menu to try it again.");
            else objectiveText.Append(run.Definition.Brief).Append("  ·  ").Append(run.Counter()).Append("  ·  ").Append(run.Clock());
            Show(objectiveStatus, objectiveText.ToString());
        }
    }
}
