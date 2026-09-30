using System;
using Emberfield.Simulation;

namespace Emberfield.Voice
{
    /// <summary>What a spoken command asks for. Presentation carries it out through the same handlers as the HUD.</summary>
    public enum VoiceAction
    {
        None,
        Help, CloseHelp,
        SelectArmy, SelectWorkers, SelectIdleWorkers, SelectUnits, SelectBuilding, ClearSelection,
        Attack, Move, Stop, Retreat, Gather, ReturnCargo, CycleFormation,
        Train, Build, Confirm, Cancel,
        Research, AdvanceEra,
        CameraHome, CameraSelection, CameraRotate, ZoomIn, ZoomOut,
        Pause, Resume
    }

    public enum VoiceLanguage { Spanish, English }

    /// <summary>A parsed command: the action and, when it needs them, a target id, a count or a resource.</summary>
    public sealed class VoiceIntent : IEquatable<VoiceIntent>
    {
        public static readonly VoiceIntent None = new VoiceIntent(VoiceAction.None);
        public VoiceAction Action { get; }
        /// <summary>Unit or building definition id, or research family, for the actions that name one.</summary>
        public string TargetId { get; }
        public int Count { get; }
        public ResourceKind Resource { get; }
        public bool IsNone => Action == VoiceAction.None;

        public VoiceIntent(VoiceAction action, string targetId = null, int count = 1, ResourceKind resource = ResourceKind.Food)
        {
            Action = action; TargetId = targetId; Resource = resource;
            Count = count < 1 ? 1 : count > VoiceText.MaximumCount ? VoiceText.MaximumCount : count;
        }

        public bool Equals(VoiceIntent other) =>
            other != null && Action == other.Action && TargetId == other.TargetId && Count == other.Count && Resource == other.Resource;
        public override bool Equals(object obj) => Equals(obj as VoiceIntent);
        public override int GetHashCode() { unchecked { return (((int)Action * 397 ^ (TargetId?.GetHashCode() ?? 0)) * 397 ^ Count) * 397 ^ (int)Resource; } }

        public override string ToString() =>
            Action + (TargetId != null ? " " + TargetId : "") + (Count > 1 ? " x" + Count : "") + (Action == VoiceAction.Gather ? " " + Resource : "");
    }

    /// <summary>A phrase offered to a keyword recognizer and the command it stands for.</summary>
    public sealed class VoicePhrase
    {
        public string Text { get; }
        public VoiceIntent Intent { get; }
        public VoicePhrase(string text, VoiceIntent intent) { Text = text; Intent = intent; }
        public override string ToString() => Text + " → " + Intent;
    }
}
