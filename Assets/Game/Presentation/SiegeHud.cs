using Emberfield.Simulation;

namespace Emberfield.Presentation
{
    public sealed partial class MatchHud
    {
        // One page of build tiles plus its pager is one row of context actions at the readable tile width.
        private const int BuildPageSize = 5;
        private int constructionPage;
        private void AddSiegeBuildingActions(BuildingState building)
        {
            var definition = match.Economy.BuildingDefinition(building.DefinitionId);
            if (definition.IsGate)
                Button(contextRow,building.GateOpen ? "Close gate\nClear the doorway first" : "Open gate\nAllows passage",() => match.Issue(new SetGateCommand(MatchController.LocalPlayer,building.Id,!building.GateOpen),"Gate order issued."));
            if (definition.IsWall)
                Button(contextRow,"Wall garrison\nSelect nearby infantry",() => {
                    var ids = new System.Collections.Generic.List<int>();
                    foreach (var unit in match.World.Units)
                    {
                        if (unit.OwnerId != MatchController.LocalPlayer || unit.IsWorker || (unit.Tags & (CombatTags.Infantry | CombatTags.Ranged)) == 0 || (unit.Tags & (CombatTags.Creature | CombatTags.Cavalry | CombatTags.Siege)) != 0) continue;
                        long x = (long)unit.Position.X-building.Position.X, z = (long)unit.Position.Z-building.Position.Z;
                        if (x*x+z*z <= 36000000 && ids.Count < definition.WallCapacity) ids.Add(unit.Id);
                    }
                    match.Select(ids); match.ChooseWall();
                });
        }
        private void AddSiegeUnitActions()
        {
            if (match.WallInfantry(false).Length > 0) Button(contextRow,"Board / assault wall\nInfantry and archers",match.ChooseWall);
            if (match.WallInfantry(true).Length > 0) Button(contextRow,"Descend wall\nChoose nearby ground",match.ChooseDescent);
        }
    }
}
