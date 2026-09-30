using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    public sealed partial class MatchController
    {
        public bool ChoosingWall { get; private set; }
        public bool ChoosingDescent { get; private set; }
        public void CancelSiegeTarget() { ChoosingWall = ChoosingDescent = false; Hud?.Invalidate(); }
        public int[] WallInfantry(bool onWall)
        {
            if (Selection.Count == 0) return Array.Empty<int>();
            var ids = new List<int>();
            foreach (int id in SelectedUnits())
                if (World.TryGetUnit(id,out var unit) && !unit.IsWorker && (unit.Tags & (CombatTags.Infantry | CombatTags.Ranged)) != 0 &&
                    (unit.Tags & (CombatTags.Creature | CombatTags.Cavalry | CombatTags.Siege)) == 0 && (unit.WallId != 0) == onWall) ids.Add(id);
            return ids.ToArray();
        }
        public void ChooseWall()
        {
            TouchSelectionMode = false;
            Economy.CancelBuild(); Factions.CancelDeploy(); CancelNavalTarget(); ChoosingWall = true; ChoosingDescent = false;
            SetFeedback("Tap a wall near the selected infantry. Enemy walls need your ladder or siege tower beside them.");
        }
        public void ChooseDescent()
        {
            TouchSelectionMode = false;
            Economy.CancelBuild(); Factions.CancelDeploy(); CancelNavalTarget(); ChoosingDescent = true; ChoosingWall = false;
            SetFeedback("Tap clear ground beside the occupied wall to descend.");
        }
        private bool HandleSiegeTap(Vector2 point)
        {
            if (ChoosingDescent)
            {
                if (Rig.GroundPoint(point,out var destination))
                {
                    var result = SubmitPlayerCommand(new LeaveWallCommand(LocalPlayer,WallInfantry(true),DefinitionLoader.ToSimulation(destination)));
                    if (result.Accepted) CancelSiegeTarget();
                    SetFeedback(result.Accepted ? "Descent order issued." : result.Message);
                }
                return true;
            }
            if (!ChoosingWall) return false;
            int target = View.Pick(Rig.Camera,point,Mathf.Max(18,Screen.height / 38f));
            if (!World.TryGetBuilding(target,out var wall) || !Economy.BuildingDefinition(wall.DefinitionId).IsWall)
            { SetFeedback("Tap a completed wall segment."); return true; }
            int equipment = 0; long nearest = long.MaxValue;
            if (wall.OwnerId != LocalPlayer)
                foreach (var unit in World.Units)
                {
                    var kind = Economy.UnitDefinition(unit.DefinitionId).SiegeEquipment;
                    if (unit.OwnerId != LocalPlayer || unit.Order != UnitOrder.Idle || kind != SiegeEquipmentKind.Ladder && kind != SiegeEquipmentKind.Tower) continue;
                    // The state's footprint, which a wall turned north-south has swapped.
                    long edgeX = System.Math.Max(0,System.Math.Abs((long)unit.Position.X-wall.Position.X)-wall.WidthCells*World.Map.CellSizeMillimetres/2);
                    long edgeZ = System.Math.Max(0,System.Math.Abs((long)unit.Position.Z-wall.Position.Z)-wall.DepthCells*World.Map.CellSizeMillimetres/2);
                    long reach = 1300+unit.RadiusMillimetres;
                    if (edgeX*edgeX+edgeZ*edgeZ > reach*reach) continue;
                    long x = (long)unit.Position.X-wall.Position.X, z = (long)unit.Position.Z-wall.Position.Z, distance = x*x+z*z;
                    if (distance < nearest) { nearest = distance; equipment = unit.Id; }
                }
            var accepted = SubmitPlayerCommand(new BoardWallCommand(LocalPlayer,WallInfantry(false),target,equipment));
            if (accepted.Accepted) CancelSiegeTarget();
            SetFeedback(accepted.Accepted ? "Wall boarding started. Protect the climbing infantry." : accepted.Message);
            return true;
        }
    }
}
