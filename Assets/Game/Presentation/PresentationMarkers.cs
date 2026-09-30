using Unity.Profiling;

namespace Emberfield.Presentation
{
    /// <summary>
    /// Profiler markers around the parts of a presentation frame, so a profiler capture (the editor's, or
    /// AllocationCapture's in a development player) says which part a frame spent its time in. They cost nothing in
    /// a release player and next to nothing elsewhere while no profiler is recording.
    /// </summary>
    internal static class PresentationMarkers
    {
        internal static readonly ProfilerMarker Units = new ProfilerMarker("WorldView.Units");
        internal static readonly ProfilerMarker Buildings = new ProfilerMarker("WorldView.Buildings");
        internal static readonly ProfilerMarker Resources = new ProfilerMarker("WorldView.Resources");
        internal static readonly ProfilerMarker Removed = new ProfilerMarker("WorldView.Removed");
        internal static readonly ProfilerMarker Effects = new ProfilerMarker("WorldView.Effects");
        internal static readonly ProfilerMarker Fog = new ProfilerMarker("WorldView.Fog");
        internal static readonly ProfilerMarker Scenery = new ProfilerMarker("WorldView.Scenery");
        internal static readonly ProfilerMarker Hud = new ProfilerMarker("MatchHud.Refresh");
        internal static readonly ProfilerMarker HudText = new ProfilerMarker("MatchHud.Text");
        internal static readonly ProfilerMarker Minimap = new ProfilerMarker("Minimap.Paint");
        internal static readonly ProfilerMarker Localization = new ProfilerMarker("UiLocalization.Apply");
    }
}
