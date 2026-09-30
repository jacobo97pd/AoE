using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    public sealed partial class MatchController
    {
        public bool ChoosingShip { get; private set; }
        public bool ChoosingLanding { get; private set; }
        public void CancelNavalTarget() { if (!ChoosingShip && !ChoosingLanding) return; ChoosingShip = ChoosingLanding = false; Hud?.Invalidate(); }

        /// <summary>The selected troops that can go aboard a ship: every land unit of yours, workers included.</summary>
        public int[] BoardingTroops()
        {
            if (Selection.Count == 0) return System.Array.Empty<int>();
            var ids = new List<int>();
            foreach (int id in Selection)
                if (World.TryGetUnit(id, out var unit) && CanBoardShip(unit))
                    ids.Add(id);
            return ids.ToArray();
        }

        internal static bool CanBoardShip(UnitState unit) => unit.OwnerId == LocalPlayer && unit.Domain == MovementDomain.Land &&
            !unit.IsPackedOutpost && unit.WallId == 0 && unit.BoardingWallId == 0 &&
            unit.ThreadkeeperRemainingTicks == 0 && unit.RelocationStage == RelocationStage.None;

        /// <summary>The selected ships of yours with troops aboard.</summary>
        public int[] LoadedShips()
        {
            if (Selection.Count == 0) return System.Array.Empty<int>();
            var ids = new List<int>();
            foreach (int id in Selection)
                if (World.TryGetUnit(id, out var unit) && unit.OwnerId == LocalPlayer && unit.Domain == MovementDomain.Water && unit.CargoCount > 0) ids.Add(id);
            return ids.ToArray();
        }

        /// <summary>Whether any ship of yours has room aboard, so a Board order has somewhere to go.</summary>
        public bool HasShipWithRoom()
        {
            foreach (var unit in World.Units)
            {
                if (unit.OwnerId != LocalPlayer || unit.CargoCapacity <= unit.CargoCount) continue;
                int reserved = unit.CargoCount;
                foreach (var boarding in World.Units)
                    if (boarding.EmbarkShipId == unit.Id && !Selection.Contains(boarding.Id)) reserved++;
                if (reserved < unit.CargoCapacity) return true;
            }
            return false;
        }

        public void ChooseShip()
        {
            if (BoardingTroops().Length == 0) { SetFeedback("Select the troops to embark."); return; }
            TouchSelectionMode = false;
            Economy.CancelBuild(); Factions.CancelDeploy(); CancelSiegeTarget(); ChoosingShip = true; ChoosingLanding = false;
            SetFeedback("Tap one of your ships. It must lie alongside a shore the troops can reach.");
            Hud?.Invalidate();
        }

        public void ChooseLanding()
        {
            if (LoadedShips().Length == 0) { SetFeedback("Select a ship with troops aboard."); return; }
            TouchSelectionMode = false;
            Economy.CancelBuild(); Factions.CancelDeploy(); CancelSiegeTarget(); ChoosingLanding = true; ChoosingShip = false;
            SetFeedback("Tap open shore ground. The ship sails beside it and lands its troops.");
            Hud?.Invalidate();
        }

        private bool HandleNavalTap(Vector2 point)
        {
            if (ChoosingLanding)
            {
                if (Rig.GroundPoint(point, out var ground)) OrderGround(DefinitionLoader.ToSimulation(ground));
                return true;
            }
            if (!ChoosingShip) return false;
            int target = View.Pick(Rig.Camera, point, Mathf.Max(18, Screen.height / 38f));
            if (!World.TryGetUnit(target, out var ship) || ship.OwnerId != LocalPlayer || ship.CargoCapacity == 0)
            { SetFeedback("Tap one of your ships."); return true; }
            if (EmbarkSelected(ship.Id)) CancelNavalTarget();
            return true;
        }

        // Tapping a ship of yours with troops selected sends them aboard, the way the classics do it.
        private bool TryBoard(UnitState tapped)
        {
            if (tapped.CargoCapacity == 0 || Selection.Contains(tapped.Id) || BoardingTroops().Length == 0) return false;
            EmbarkSelected(tapped.Id);
            return true;
        }

        // Tapping the shore with only laden ships selected lands their troops there instead of trying to sail ashore.
        private bool TryLand(SimPoint point)
        {
            if (World.IsSailable(point) || !World.IsWalkable(point)) return false;
            foreach (int id in SelectedUnits())
                if (World.TryGetUnit(id, out var unit) && (unit.Domain != MovementDomain.Water || unit.CargoCount == 0)) return false;
            if (LoadedShips().Length == 0) return false;
            DisembarkSelected(point);
            return true;
        }

        /// <summary>Shared by ground taps and minimap orders: loaded ships land on shore, and sail to open water.</summary>
        public void OrderGround(SimPoint point)
        {
            if (ChoosingLanding)
            {
                if (DisembarkSelected(point)) CancelNavalTarget();
                return;
            }
            if (ChoosingShip) { SetFeedback("Tap one of your ships."); return; }
            var selected = SelectedUnits();
            if (selected.Length == 0 || TryLand(point)) return;
            Issue(new MoveCommand(LocalPlayer, selected, point, Formation), "Moving in " + Formation.ToString().ToLowerInvariant() + " formation.");
        }

        public bool EmbarkSelected(int shipId)
        {
            var result = SubmitPlayerCommand(new EmbarkCommand(LocalPlayer, BoardingTroops(), shipId));
            SetFeedback(result.Accepted ? World.IsNetworkReplica ? "Order sent. Waiting for the server." : "Troops heading aboard." : result.Message);
            if (result.Accepted) View.Feedback?.Emit(FeedbackCue.Order, View.RootFor(shipId)?.position ?? Vector3.zero);
            Hud?.Invalidate();
            return result.Accepted;
        }

        public bool DisembarkSelected(SimPoint point)
        {
            CommandResult last = default; bool any = false;
            foreach (int id in LoadedShips())
            {
                last = SubmitPlayerCommand(new DisembarkCommand(LocalPlayer, id, point));
                any |= last.Accepted;
            }
            SetFeedback(any ? World.IsNetworkReplica ? "Order sent. Waiting for the server." : "Sailing to land the troops." : last.Message ?? "Select a ship with troops aboard.");
            Hud?.Invalidate();
            return any;
        }
    }
}
