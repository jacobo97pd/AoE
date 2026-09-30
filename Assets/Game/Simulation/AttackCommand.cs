using System.Collections.Generic;

namespace Emberfield.Simulation
{
    public sealed class AttackCommand : IGameCommand
    {
        public int PlayerId { get; }
        public IReadOnlyList<int> UnitIds { get; }
        public int TargetEntityId { get; }
        public AttackCommand(int playerId, int[] unitIds, int targetEntityId)
        { PlayerId = playerId; UnitIds = GatherCommand.Copy(unitIds); TargetEntityId = targetEntityId; }
    }
}
