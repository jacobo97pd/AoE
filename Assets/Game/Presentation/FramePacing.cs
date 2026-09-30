using System;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// How a desktop paces its frames. The default waits for the display (vertical sync): each frame is shown for one
    /// refresh at whatever rate the monitor runs, 60, 144 or 240 Hz, with no tearing and no software timer guessing
    /// when to present. Settings can cap at 60 or 30 instead, or leave the rate unlimited. Phones never take the
    /// choice: MobileQuality's tier caps pace them. Presentation only; the simulation keeps its 20 Hz tick either way.
    /// Development players and the editor take -emberfieldFrameRate display|60|30|unlimited over the saved choice.
    /// </summary>
    public static class FramePacing
    {
        public const string Flag = "-emberfieldFrameRate";
        public const int Display = 0, Cap60 = 1, Cap30 = 2, Unlimited = 3, Choices = 4;
        // A minimised window has no display refresh to wait for, so where the game keeps running out of focus (online)
        // the display choice would spin as fast as it can; out of focus it holds this cap instead, until focus returns.
        public const int BackgroundFrameRate = 30;
        private static int applied = -1;
        private static bool listening, background;

        /// <summary>The choice as the settings screen names it.</summary>
        public static string Name(int choice) => choice switch
        {
            Cap60 => "60 FPS", Cap30 => "30 FPS", Unlimited => "Unlimited", _ => "Display rate",
        };

        /// <summary>
        /// Match start, before anything draws. Stress and the probes run uncapped; a phone starts at 60 until its tier
        /// sets its own cap; a desktop waits for the display until AlphaControls applies the saved choice. vSync is set
        /// every time because it outlives the scene that chose it.
        /// </summary>
        public static void Begin(bool uncapped)
        {
            applied = -1; background = false;
            if (!listening) { Application.focusChanged += Focus; listening = true; }
            if (uncapped) Set(0, -1);
            else if (MobileQuality.Requested) Set(0, 60);
            else Set(1, -1);
        }

        /// <summary>Applies a desktop choice. Repeating the one in force changes nothing, so a probe's own pacing stays.</summary>
        public static void Choose(int choice)
        {
            if (MobileQuality.Requested) return;
            choice = Override() ?? choice;
            if (choice == applied) return;
            applied = choice; background = false;
            switch (choice)
            {
                case Cap60: Set(0, 60); break;
                case Cap30: Set(0, 30); break;
                case Unlimited: Set(0, -1); break;
                default: Set(1, -1); break;
            }
        }

        private static void Focus(bool focused)
        {
            if (applied != Display) return;
            if (!focused && Application.runInBackground && QualitySettings.vSyncCount == 1) { background = true; Set(0, BackgroundFrameRate); }
            else if (focused && background) { background = false; Set(1, -1); }
        }

        // Only real changes reach the engine: writing vSync again, even unchanged, can reset the swap chain's pacing.
        private static void Set(int vSync, int frameRate)
        {
            if (QualitySettings.vSyncCount != vSync) QualitySettings.vSyncCount = vSync;
            if (Application.targetFrameRate != frameRate) Application.targetFrameRate = frameRate;
        }

        private static int? Override()
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return null;
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, Flag);
            if (option < 0 || option + 1 >= args.Length) return null;
            switch (args[option + 1].Trim().ToLowerInvariant())
            {
                case "60": return Cap60;
                case "30": return Cap30;
                case "unlimited": return Unlimited;
                case "display": return Display;
                default: return null;
            }
        }
    }
}
