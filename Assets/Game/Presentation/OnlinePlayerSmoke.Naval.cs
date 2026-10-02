using System;
using System.Collections;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    public sealed partial class OnlinePlayerSmoke
    {
        // Optional native naval drill. All resources, buildings, units and cargo come from the authority.
        // Planning reads only the replica's public terrain and received entities; no fixture mutates gameplay.
        private IEnumerator NavalActions(int hearthId)
        {
            Check(config.RealmId == ContentRealms.Naval, "The naval drill requires the naval realm.");
            Check(match.World.TryGetBuilding(hearthId, out var hearth), "Naval opening Hearth missing.");
            var workers = OwnedWorkers(); report.PassengerId = workers[2];
            var dock = match.Economy.BuildingDefinition("dock");
            var house = match.Economy.BuildingDefinition("shelter");
            // Each seat launches its own faction's hull: the pirates' sloop, the English frigate or the Spanish galleon.
            UnitDefinition shipDefinition = null;
            foreach (var definition in match.World.Definition.Units)
                if (definition.Domain == MovementDomain.Water && dock != null && Array.IndexOf(dock.TrainableUnitIds ?? Array.Empty<string>(), definition.Id) >= 0 &&
                    match.World.ValidateUnitRecruitment(1, definition.Id).Accepted) shipDefinition = definition;
            Check(dock != null && shipDefinition != null, "Playable dock or the faction's ship definition missing.");
            report.ShipDefinitionId = shipDefinition.Id;
            foreach (int worker in workers) if (worker != report.PassengerId) AssignGather(worker, ResourceKind.Wood);
            var coast = CoastalSites(dock, hearth.Position);
            Check(coast.Count > 0, "Public terrain has no suitable shoreline dock site.");
            SimPoint site = default; bool found = false;
            // Scout the nearest coast using one ordinary worker. Placement is decided only after fog updates.
            for (int i = 0; i < Math.Min(coast.Count, 6) && !found; i++)
            {
                Check(match.World.Submit(new MoveCommand(1, new[] { report.PassengerId }, coast[i])).Accepted, "Coast scouting order failed.");
                float until = Mathf.Min(deadline, Time.realtimeSinceStartup + 18);
                while (Time.realtimeSinceStartup < until)
                {
                    foreach (var candidate in coast)
                        if (match.World.ValidatePlacement(1, new[] { report.PassengerId }, dock.Id, candidate).Accepted)
                        { site = candidate; found = true; break; }
                    if (found) break;
                    yield return null;
                }
            }
            Check(found, "The scout did not reveal a legal shoreline dock placement.");
            yield return Wait(() => LocalResources().Wood >= dock.Cost.Wood + shipDefinition.Cost.Wood + house.Cost.Wood && LocalResources().Food >= shipDefinition.Cost.Food,
                "Ordinary gathering did not fund the dock and ship.", 100);
            Check(match.World.Submit(new StopCommand(1, workers.ToArray())).Accepted, "Naval gatherers could not stop.");
            long stop = match.Online.SubmittedSequence;
            yield return Wait(() => match.Online.State.lastAcceptedSequence >= stop && match.Online.PendingOrderCount == 0, "Gathering stop was not acknowledged.", 10);
            // Five opening workers plus a two-population hull need an ordinary house before training.
            var homeSite = FindHouseSite(workers[0], house, hearth.Position);
            Check(match.World.Submit(new BuildCommand(1, new[] { workers[0] }, house.Id, homeSite)).Accepted, "Population house could not be queued.");
            yield return Wait(() => OwnedBuilding(house.Id, homeSite) != null, "Population house did not appear on the authority.", 12);
            yield return Wait(() => OwnedBuilding(house.Id, homeSite).IsComplete, "Population house did not finish.", 40);
            // A builder stays beside the finished house. Move that worker off the route to the harbour
            // before sending the other builder through it; both orders use the normal authority.
            SimPoint parking = BuilderParking(workers[0], hearth.Position, site);
            Check(match.World.Submit(new MoveCommand(1, new[] { workers[0] }, parking)).Accepted, "House builder could not clear the harbour route.");
            yield return Wait(() => match.World.TryGetUnit(workers[0], out var parked) && Squared(parked.Position, parking) <= 250000,
                "House builder did not clear the harbour route.", 18);
            Check(match.World.Submit(new StopCommand(1, new[] { workers[0] })).Accepted, "House builder could not stop in its clearing.");
            long parkingStop = match.Online.SubmittedSequence;
            yield return Wait(() => match.Online.State.lastAcceptedSequence >= parkingStop && match.Online.PendingOrderCount == 0,
                "House builder parking was not acknowledged.", 10);
            // Moving the house builder changes current vision. Revalidate the dock against the latest
            // observation instead of keeping a foundation revealed earlier by that worker.
            Check(match.World.TryGetUnit(report.PassengerId, out var scout), "Dock builder disappeared.");
            found = false;
            foreach (var candidate in CoastalSites(dock, scout.Position))
                if (match.World.ValidatePlacement(1, new[] { report.PassengerId }, dock.Id, candidate).Accepted)
                { site = candidate; found = true; break; }
            Check(found, "The dock builder has no currently visible shoreline foundation.");
            int wood = LocalResources().Wood;
            Check(match.World.Submit(new BuildCommand(1, new[] { report.PassengerId }, dock.Id, site)).Accepted, "Dock construction could not be queued.");
            yield return Wait(() => OwnedDock(site) != null, "Authority did not create the paid shoreline dock.", 12);
            report.DockId = OwnedDock(site).Id; report.DockWoodSpent = wood - LocalResources().Wood;
            Check(report.DockWoodSpent == dock.Cost.Wood, "Dock wood cost did not match its definition.");
            yield return Wait(() => match.World.TryGetBuilding(report.DockId, out var built) && built.IsComplete, "Dock did not finish construction.", 45);
            match.Select(new[] { report.DockId });
            wood = LocalResources().Wood;
            Check(match.Economy.Train(shipDefinition.Id).Accepted, "Dock could not queue its ship.");
            yield return Wait(() => FindOwnedShip() != null, "Authority did not launch the trained ship.", 35);
            var ship = FindOwnedShip(); report.ShipId = ship.Id; report.ShipWoodSpent = wood - LocalResources().Wood;
            report.ShipTrained = ship.Domain == MovementDomain.Water && match.World.IsSailable(ship.Position) && report.ShipWoodSpent == shipDefinition.Cost.Wood;
            Check(report.ShipTrained, "Ship spawn domain or paid cost was incorrect.");
            match.Select(new[] { report.ShipId }); match.Rig.Focus(DefinitionLoader.ToWorld(ship.Position));
            yield return Capture("naval-dock-and-ship.png");
            Check(match.World.Submit(new EmbarkCommand(1, new[] { report.PassengerId }, report.ShipId)).Accepted, "Embark order could not be queued.");
            yield return Wait(() => PassengerAboard(), "The worker did not embark the transport.", 30);
            var from = FindOwnedShip().Position;
            var landing = LandingSite(from, dock, site);
            report.LandingX = landing.X; report.LandingZ = landing.Z;
            // This order makes the hull travel to another part of the shoreline before placing the passenger on land.
            Check(match.World.Submit(new DisembarkCommand(1, report.ShipId, landing)).Accepted, "Landing voyage could not be queued.");
            yield return Wait(() => match.World.TryGetUnit(report.PassengerId, out var landed) && landed.CarrierId == 0,
                "The ship did not sail and land its worker.", 50);
            report.ShipSailed = Moved(report.ShipId, from);
            report.PassengerLanded = match.World.TryGetUnit(report.PassengerId, out var passenger) && match.World.IsWalkable(passenger.Position);
            Check(report.ShipSailed && report.PassengerLanded, "Transport did not travel on water and land on clear ground.");
            match.Rig.Focus(DefinitionLoader.ToWorld(FindOwnedShip().Position));
            match.Select(new[] { report.PassengerId }); yield return Capture("naval-worker-landed.png");
            Check(match.World.Submit(new EmbarkCommand(1, new[] { report.PassengerId }, report.ShipId)).Accepted, "Worker could not board again for reconnect verification.");
            yield return Wait(PassengerAboard, "Cargo did not reboard before the process restart.", 25);
            report.AcceptedSequence = match.Online.State.lastAcceptedSequence;
            match.Select(new[] { report.ShipId }); SaveProgress(); yield return Capture("naval-cargo-before-reconnect.png");
        }

        private List<SimPoint> CoastalSites(BuildingDefinition dock, SimPoint origin)
        {
            int cell = match.World.Map.CellSizeMillimetres; var sites = new List<SimPoint>();
            for (int z = 0; z <= match.World.Map.HeightCells - dock.DepthCells; z++)
            for (int x = 0; x <= match.World.Map.WidthCells - dock.WidthCells; x++)
            {
                bool clear = true, sea = false;
                for (int dz = 0; dz < dock.DepthCells && clear; dz++)
                for (int dx = 0; dx < dock.WidthCells; dx++)
                    if (!match.World.IsWalkable(new SimPoint((x + dx) * cell + cell / 2, (z + dz) * cell + cell / 2))) { clear = false; break; }
                if (!clear) continue;
                for (int dx = 0; dx < dock.WidthCells; dx++)
                    sea |= match.World.IsSailable(new SimPoint((x + dx) * cell + cell / 2, z * cell - cell / 2)) ||
                           match.World.IsSailable(new SimPoint((x + dx) * cell + cell / 2, (z + dock.DepthCells) * cell + cell / 2));
                for (int dz = 0; dz < dock.DepthCells; dz++)
                    sea |= match.World.IsSailable(new SimPoint(x * cell - cell / 2, (z + dz) * cell + cell / 2)) ||
                           match.World.IsSailable(new SimPoint((x + dock.WidthCells) * cell + cell / 2, (z + dz) * cell + cell / 2));
                if (sea) sites.Add(new SimPoint(x * cell + dock.WidthCells * cell / 2, z * cell + dock.DepthCells * cell / 2));
            }
            sites.Sort((a, b) => { int distance = Squared(a, origin).CompareTo(Squared(b, origin)); return distance != 0 ? distance : a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z); });
            return sites;
        }

        private SimPoint LandingSite(SimPoint ship, BuildingDefinition dock, SimPoint site)
        {
            var candidates = CoastalSites(dock, site);
            foreach (var candidate in candidates)
                if (Squared(candidate, ship) >= 49000000 && Squared(candidate, ship) <= 400000000) return candidate;
            Check(false, "No nearby second shore for the transport voyage."); return default;
        }
        private SimPoint FindHouseSite(int worker, BuildingDefinition house, SimPoint hearth)
        {
            int cell = match.World.Map.CellSizeMillimetres;
            for (int radius = 3; radius <= 12; radius++)
            for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
            {
                if (Math.Max(Math.Abs(x), Math.Abs(z)) != radius) continue;
                var point = new SimPoint((hearth.X / cell + x) * cell + house.WidthCells * cell / 2,
                    (hearth.Z / cell + z) * cell + house.DepthCells * cell / 2);
                if (match.World.ValidatePlacement(1, new[] { worker }, house.Id, point).Accepted) return point;
            }
            Check(false, "No legal house site near the opening base."); return default;
        }
        private SimPoint BuilderParking(int worker, SimPoint hearth, SimPoint harbour)
        {
            int cell = match.World.Map.CellSizeMillimetres;
            for (int radius = 4; radius <= 9; radius++)
            for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
            {
                if (Math.Max(Math.Abs(x), Math.Abs(z)) != radius || (long)x * (harbour.X - hearth.X) + (long)z * (harbour.Z - hearth.Z) > 0) continue;
                var point = new SimPoint((hearth.X / cell + x) * cell + cell / 2, (hearth.Z / cell + z) * cell + cell / 2);
                if (!match.World.IsWalkable(point)) continue;
                bool free = true;
                foreach (var other in match.World.Units) if (other.Id != worker && Squared(other.Position, point) < 1000000) { free = false; break; }
                if (free) return point;
            }
            Check(false, "No visible clearing for the house builder."); return default;
        }
        private BuildingState OwnedBuilding(string definition, SimPoint site)
        { foreach (var b in match.World.Buildings) if (b.OwnerId == 1 && b.DefinitionId == definition && b.Position == site) return b; return null; }
        private static long Squared(SimPoint a, SimPoint b) { long x = (long)a.X - b.X, z = (long)a.Z - b.Z; return x * x + z * z; }
        private BuildingState OwnedDock(SimPoint site)
        { foreach (var b in match.World.Buildings) if (b.OwnerId == 1 && b.DefinitionId == "dock" && b.Position == site) return b; return null; }
        private UnitState FindOwnedShip()
        { foreach (var u in match.World.Units) if (u.OwnerId == 1 && u.Domain == MovementDomain.Water) return u; return null; }
        private bool PassengerAboard() => match.World.TryGetPassenger(report.PassengerId, out var passenger) && passenger.CarrierId == report.ShipId &&
            match.World.TryGetUnit(report.ShipId, out var ship) && ship.CargoCount == 1;
        private void VerifyPersistentAssets()
        {
            if (config.NavalSlice)
            {
                report.NavalAssetsPersisted = match.World.TryGetBuilding(report.DockId, out var dock) && dock.IsComplete && dock.OwnerId == 1 && PassengerAboard();
                Check(report.NavalAssetsPersisted, "Paid dock, ship or cargo IDs did not survive reconnection.");
            }
            else
            {
                report.FortificationsPersisted = VerifyFortifications(true);
                Check(report.FortificationsPersisted, "Completed north-south fortifications did not survive reconnection.");
            }
        }
        private IEnumerator LandSmokePassenger()
        {
            Check(match.World.Submit(new DisembarkCommand(1, report.ShipId, new SimPoint(report.LandingX, report.LandingZ))).Accepted, "Post-reconnect landing could not be queued.");
            yield return Wait(() => match.World.TryGetUnit(report.PassengerId, out _), "The restored passenger could not disembark after reconnect.", 15);
        }
    }
}
