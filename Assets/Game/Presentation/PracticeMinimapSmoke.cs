using System.Collections;
using System.IO;
using UnityEngine;

namespace Emberfield.Presentation
{
    // Opt-in headless-safe capture of the practice sandbox (DefinitionLoader/amber_reach) with its minimap
    // visible, for a build's product review. Never runs for an ordinary launch.
    internal static class PracticeMinimapSmoke
    {
        public static IEnumerator Run(MatchController match, string folder)
        {
            Directory.CreateDirectory(folder);
            while (!Application.isBatchMode && !UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            // A fresh standalone launch opens the front door over the practice running underneath it;
            // close it so the practice HUD, guide and minimap are what the capture shows.
            match.Shell?.Close();
            match.Hud.Invalidate(); match.SyncPresentation(1);
            yield return null;
            // The HUD draws as a screen-space overlay by default, which a camera-only render request never
            // sees; borrow the camera for one frame so the minimap and the rest of the HUD are in the capture.
            match.Hud.PrepareOffscreenCapture(match.Rig.Camera);
            yield return new WaitForEndOfFrame();
            bool hasMinimap = match.Hud.Minimap != null;
            bool hasPixels = PlayerSmoke.Capture(match, Path.Combine(folder, "practice-minimap.png"));
            bool passed = hasMinimap && hasPixels && match.World.Match == null && !match.Shell.IsOpen;
            File.WriteAllText(Path.Combine(folder, "practice-minimap.txt"),
                $"Passed: {passed}\nHasMinimap: {hasMinimap}\nHasPixels: {hasPixels}\nMap: {match.World.Map.Id}\n");
            Debug.Log("EMBERFIELD_PRACTICE_MINIMAP_CAPTURE " + passed);
            Application.Quit(passed ? 0 : 1);
        }
    }
}
