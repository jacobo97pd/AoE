using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Emberfield.Presentation
{
    /// <summary>
    /// The habit every strategy player brings with them: Control and a digit remembers who you have,
    /// the digit alone brings them back, and pressing it twice takes the camera to them. Groups hold
    /// identifiers rather than units, so anything that dies simply leaves the group behind it.
    /// </summary>
    public sealed class ControlGroups
    {
        public const int Slots = 10;
        // A second press this soon after the first means "and show me", not "select again".
        private const float JumpSeconds = .5f;
        private readonly MatchController match;
        private readonly List<int>[] groups = new List<int>[Slots];
        private float lastRecall = -10;
        private int lastSlot = -1;

        public ControlGroups(MatchController match) { this.match = match; }

        public int Size(int slot) { Prune(slot); return groups[slot]?.Count ?? 0; }

        /// <summary>Stores whatever is selected right now, replacing the slot. An empty selection clears it.</summary>
        public void Assign(int slot)
        {
            if (slot < 0 || slot >= Slots) return;
            var stored = groups[slot] ?? (groups[slot] = new List<int>());
            stored.Clear(); stored.AddRange(match.Selection);
            match.SetFeedback(stored.Count == 0 ? "Group " + slot + " cleared."
                : "Group " + slot + " set: " + stored.Count + (stored.Count == 1 ? " unit." : " units."));
        }

        /// <summary>Selects the slot. Returns false when nothing in it is still alive and yours.</summary>
        public bool Recall(int slot, bool add = false)
        {
            if (slot < 0 || slot >= Slots) return false;
            Prune(slot);
            var stored = groups[slot];
            if (stored == null || stored.Count == 0) { match.SetFeedback("Group " + slot + " is empty."); return false; }
            match.Select(stored, add);
            match.SetFeedback("Group " + slot + " selected: " + stored.Count + (stored.Count == 1 ? " unit." : " units."));
            return true;
        }

        /// <summary>Takes the camera to the middle of a group without changing the selection.</summary>
        public bool Jump(int slot)
        {
            Prune(slot);
            var stored = slot >= 0 && slot < Slots ? groups[slot] : null;
            if (stored == null || stored.Count == 0) return false;
            var centre = Vector3.zero; int counted = 0;
            foreach (int id in stored)
            {
                if (match.World.TryGetUnit(id, out var unit)) { centre += DefinitionLoader.ToWorld(unit.Position); counted++; }
                else if (match.World.TryGetBuilding(id, out var building)) { centre += DefinitionLoader.ToWorld(building.Position); counted++; }
            }
            if (counted == 0) return false;
            match.Rig.Focus(centre / counted);
            return true;
        }

        public void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.homeKey.wasPressedThisFrame) Home();
            bool assigning = keyboard.ctrlKey.isPressed;
            bool add = keyboard.shiftKey.isPressed;
            for (int slot = 0; slot < Slots; slot++)
            {
                if (!Digit(keyboard, slot).wasPressedThisFrame) continue;
                if (assigning) { Assign(slot); lastSlot = -1; return; }
                // The same digit twice in quick succession is the player asking to be taken there.
                if (slot == lastSlot && Time.unscaledTime - lastRecall < JumpSeconds) Jump(slot);
                else Recall(slot, add);
                lastSlot = slot; lastRecall = Time.unscaledTime;
                return;
            }
        }

        /// <summary>Home takes the camera to the seat of the settlement: the building that trains workers and takes deliveries.</summary>
        private void Home()
        {
            BuildingState seat = null, fallback = null;
            foreach (var building in match.World.Buildings)
            {
                if (building.OwnerId != MatchController.LocalPlayer) continue;
                if (fallback == null && building.IsComplete) fallback = building;
                var definition = match.Economy.BuildingDefinition(building.DefinitionId);
                if (definition == null || !definition.CanDropOff) continue;
                foreach (string id in definition.TrainableUnitIds ?? Array.Empty<string>())
                    if (match.Economy.UnitDefinition(id)?.IsWorker == true) { seat = building; break; }
                if (seat != null) break;
            }
            seat = seat ?? fallback;
            if (seat != null) match.Rig.Focus(DefinitionLoader.ToWorld(seat.Position)); else match.Rig.Home();
            match.SetFeedback("Camera on your base.");
        }

        private void Prune(int slot)
        {
            var stored = slot >= 0 && slot < Slots ? groups[slot] : null;
            if (stored == null) return;
            for (int index = stored.Count - 1; index >= 0; index--)
            {
                int id = stored[index];
                bool alive = match.World.TryGetUnit(id, out var unit) && unit.OwnerId == MatchController.LocalPlayer;
                alive |= match.World.TryGetBuilding(id, out var building) && building.OwnerId == MatchController.LocalPlayer;
                if (!alive) stored.RemoveAt(index);
            }
        }

        private static ButtonControl Digit(Keyboard keyboard, int slot)
        {
            switch (slot)
            {
                case 1: return keyboard.digit1Key;
                case 2: return keyboard.digit2Key;
                case 3: return keyboard.digit3Key;
                case 4: return keyboard.digit4Key;
                case 5: return keyboard.digit5Key;
                case 6: return keyboard.digit6Key;
                case 7: return keyboard.digit7Key;
                case 8: return keyboard.digit8Key;
                case 9: return keyboard.digit9Key;
                default: return keyboard.digit0Key;
            }
        }
    }

    public sealed partial class MatchController
    {
        private ControlGroups controlGroups;
        public ControlGroups Groups => controlGroups ?? (controlGroups = new ControlGroups(this));
    }
}
