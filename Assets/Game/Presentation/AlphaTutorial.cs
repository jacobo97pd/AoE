using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    public enum AlphaTutorialStep { Camera, Selection, Movement, Delivery, Building, Training, Attack, Era, Victory, Complete }

    // Observation only: this guide never submits a command, changes rules or creates resources/entities.
    public sealed class AlphaTutorial
    {
        public const int AllSteps = (1 << (int)AlphaTutorialStep.Complete) - 1;
        private readonly World world;
        private readonly int playerId;
        private readonly HashSet<int> initialUnits = new HashSet<int>(), initialBuildings = new HashSet<int>();
        private readonly Dictionary<int, int> cargo = new Dictionary<int, int>();
        private readonly Dictionary<int, ResourceKind> cargoKind = new Dictionary<int, ResourceKind>();
        private readonly Dictionary<int, SimPoint> commandedMoves = new Dictionary<int, SimPoint>();
        private Vector3 cameraStart;
        private float zoomStart;
        private bool hasCameraStart;
        private ResourceAmount previousResources;
        private int initialEra;
        private long lastObservedTick = -1;
        public int Progress { get; private set; }
        public bool IsComplete => Progress == AllSteps;
        public AlphaTutorialStep Step
        {
            get { for (int i = 0; i < (int)AlphaTutorialStep.Complete; i++) if ((Progress & (1 << i)) == 0) return (AlphaTutorialStep)i; return AlphaTutorialStep.Complete; }
        }
        public int CompletedCount { get { int count = 0; for (int i = 0; i < 9; i++) if ((Progress & (1 << i)) != 0) count++; return count; } }
        public AlphaTutorial(World world, int playerId, int progress = 0)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world)); this.playerId = playerId;
            if (!world.TryGetPlayer(playerId, out var player)) throw new ArgumentException("Tutorial player must exist.", nameof(playerId));
            Progress = progress & AllSteps; previousResources = player.Resources; initialEra = player.EraTier;
            foreach (var unit in world.Units) if (unit.OwnerId == playerId)
            { initialUnits.Add(unit.Id); cargo[unit.Id] = unit.CarriedAmount; cargoKind[unit.Id] = unit.CarriedKind; }
            foreach (var building in world.Buildings) if (building.OwnerId == playerId) initialBuildings.Add(building.Id);
        }
        public void ObserveCamera(Vector3 position, float zoom)
        {
            if (!hasCameraStart) { cameraStart = position; zoomStart = zoom; hasCameraStart = true; return; }
            if ((position - cameraStart).sqrMagnitude > .64f || Mathf.Abs(zoom - zoomStart) > .25f) Complete(AlphaTutorialStep.Camera);
        }
        public void ObserveSelection(IReadOnlyList<int> selection)
        {
            if (selection == null) return;
            foreach (int id in selection)
                if (world.TryGetUnit(id, out var unit) && unit.OwnerId == playerId) { Complete(AlphaTutorialStep.Selection); break; }
        }
        public void ObserveCommand(IGameCommand command, CommandResult result)
        {
            if (command == null || command.PlayerId != playerId || !result.Accepted) return;
            if (command is MoveCommand move)
                foreach (int id in move.UnitIds) if (world.TryGetUnit(id, out var unit) && unit.OwnerId == playerId) commandedMoves[id] = unit.Position;
            if (command is AttackCommand) Complete(AlphaTutorialStep.Attack);
        }
        public void ObserveWorld()
        {
            // Surrender can finish the match without advancing its tick.
            if (world.Match != null && world.Match.IsFinished && world.Match.WinnerId == playerId) Complete(AlphaTutorialStep.Victory);
            if (lastObservedTick == world.TickIndex) return;
            lastObservedTick = world.TickIndex;
            world.TryGetPlayer(playerId, out var player);
            foreach (var unit in world.Units) if (unit.OwnerId == playerId)
            {
                if (!unit.IsPackedOutpost && !initialUnits.Contains(unit.Id)) Complete(AlphaTutorialStep.Training);
                if (commandedMoves.TryGetValue(unit.Id, out var start))
                {
                    long x = (long)unit.Position.X - start.X, z = (long)unit.Position.Z - start.Z;
                    if (x * x + z * z >= 250000) Complete(AlphaTutorialStep.Movement);
                }
                if (unit.IsWorker && cargo.TryGetValue(unit.Id, out int before) && before > 0 && unit.CarriedAmount == 0
                    && cargoKind.TryGetValue(unit.Id, out var kind) && player.Resources.Get(kind) > previousResources.Get(kind))
                    Complete(AlphaTutorialStep.Delivery);
                cargo[unit.Id] = unit.CarriedAmount; cargoKind[unit.Id] = unit.CarriedKind;
            }
            foreach (var building in world.Buildings)
                if (building.OwnerId == playerId && !initialBuildings.Contains(building.Id) && building.IsComplete)
                    Complete(AlphaTutorialStep.Building);
            if (player.EraTier > initialEra) Complete(AlphaTutorialStep.Era);
            if (world.Match != null && world.Match.IsFinished && world.Match.WinnerId == playerId) Complete(AlphaTutorialStep.Victory);
            previousResources = player.Resources;
        }
        public string Instruction
        {
            get
            {
                switch (Step)
                {
                    case AlphaTutorialStep.Camera: return "Explore the map: pan with the arrow keys, the screen edge or a finger drag. Pinch or use the mouse wheel to zoom.";
                    case AlphaTutorialStep.Selection: return "Tap one of your workers, or use Workers to select the workers visible on screen.";
                    case AlphaTutorialStep.Movement: return "With a unit selected, tap open ground and watch it move. Keep Select mode off when giving orders.";
                    case AlphaTutorialStep.Delivery: return "Select a Tender and tap Food or Wood. Wait for it to carry resources home: only a delivery adds to your stock.";
                    case AlphaTutorialStep.Building: return "Select a Tender, choose Shelter or Muster Hall, tap a clear location and Confirm. Gather more Wood if needed; wait for construction.";
                    case AlphaTutorialStep.Training: return "Select your Hearth to train a Tender, or a completed Muster Hall to train a soldier. Keep gathering and wait for the unit to appear.";
                    case AlphaTutorialStep.Attack: return "Select a soldier and tap a visible enemy to attack. Train at your Muster Hall if needed; scout to find the rival.";
                    case AlphaTutorialStep.Era: return "Open Research and advance to the next Era. Meet its building requirements and gather the displayed cost; let research finish.";
                    case AlphaTutorialStep.Victory: return world.Match == null ? "Open Play and start an AI match. Win Conquest by destroying every rival Hearth, or Dominion by holding two beacons for eight minutes. This guide continues there." : "Win this match: destroy every rival Hearth in Conquest, or hold two beacons for eight uninterrupted minutes in Dominion. Protect your economy.";
                    default: return "Training complete. You explored, gathered, built, trained, attacked, advanced an Era and won a match. Try the other faction or victory condition.";
                }
            }
        }
        private void Complete(AlphaTutorialStep step) => Progress |= 1 << (int)step;
    }
}
