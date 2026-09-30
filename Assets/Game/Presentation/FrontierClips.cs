using System;
using System.Collections.Generic;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// The CC0 recordings under Assets/Game/Resources/Audio (licences and the cue map in docs/art/audio.md),
    /// looked up by the cue that wants them. Everything here is optional: a cue with no real clip, or a clip
    /// that failed to load, falls back to <see cref="FrontierSounds"/>'s synthesis rather than to silence — the
    /// same "cached once per process" spirit as the synth voices, so a second match costs nothing to reload.
    /// Three of the eight <see cref="FeedbackCue"/>s stay purely synthesised on purpose: Order's own pluck
    /// already does the job (its real layer is the voiced acknowledgement below, not a replacement), Gather's
    /// only real clip is a cloth rustle standing in for grain (a weak approximation), and Defeat already reads
    /// well and fires often in a fight — the source review judged neither worth layering yet.
    /// Indexed by (int)FeedbackCue, the same convention SliceFeedback already uses for its own clip/material
    /// arrays: cheap, and free of any per-lookup allocation a dictionary keyed on an enum could risk.
    /// </summary>
    public static class FrontierClips
    {
        private const int CueCount = 8;
        private static readonly string[][] CuePaths = new string[CueCount][];
        private static readonly AudioClip[][] cache = new AudioClip[CueCount][];

        // The unit acknowledgement voice, for Order alone: neutral words only ("ready"/"go"/"hold"), never the
        // pack's "objective achieved" line, which reads as a modern shooter callout rather than an RTS bark.
        private static readonly string[] OrderVoicePaths =
        {
            "Audio/Units/Acknowledgement/unit_ack_m_ready", "Audio/Units/Acknowledgement/unit_ack_f_ready",
            "Audio/Units/Acknowledgement/unit_ack_m_go", "Audio/Units/Acknowledgement/unit_ack_f_go",
            "Audio/Units/Acknowledgement/unit_ack_m_hold", "Audio/Units/Acknowledgement/unit_ack_f_hold",
        };
        private static AudioClip[] orderVoice;

        static FrontierClips()
        {
            CuePaths[(int)FeedbackCue.Chop] = new[] { "Audio/Economy/Chopping/economy_chop_01" };
            CuePaths[(int)FeedbackCue.Mine] = new[]
            {
                "Audio/Economy/Mining/economy_mine_01", "Audio/Economy/Mining/economy_mine_02", "Audio/Economy/Mining/economy_mine_03",
                "Audio/Economy/Mining/economy_mine_04", "Audio/Economy/Mining/economy_mine_05",
            };
            CuePaths[(int)FeedbackCue.Complete] = new[] { "Audio/Economy/BuildingComplete/economy_building_complete_01" };
            CuePaths[(int)FeedbackCue.Impact] = new[]
            {
                "Audio/Combat/SwordSpearHits/combat_metal_light_01", "Audio/Combat/SwordSpearHits/combat_metal_light_02", "Audio/Combat/SwordSpearHits/combat_metal_light_03",
                "Audio/Combat/SwordSpearHits/combat_metal_medium_01", "Audio/Combat/SwordSpearHits/combat_metal_medium_02", "Audio/Combat/SwordSpearHits/combat_metal_medium_03",
                "Audio/Combat/SwordSpearHits/combat_metal_heavy_01", "Audio/Combat/SwordSpearHits/combat_metal_heavy_02", "Audio/Combat/SwordSpearHits/combat_metal_heavy_03",
            };
            CuePaths[(int)FeedbackCue.Objective] = new[] { "Audio/Ui/Notification/ui_notify_01", "Audio/Ui/Notification/ui_notify_02", "Audio/Ui/Notification/ui_notify_03" };
            // Order, Gather and Defeat are left null: FrontierSounds' own synthesis is all they play.
        }

        /// <summary>A random real variant for this cue's layer, or null when the cue has none (kept purely synthesised).</summary>
        public static AudioClip Real(FeedbackCue cue)
        {
            int index = (int)cue;
            var clips = cache[index] ??= Load(CuePaths[index] ?? Array.Empty<string>());
            return Pick(clips);
        }

        /// <summary>A random neutral acknowledgement line, or null if the voice pack failed to load.</summary>
        public static AudioClip OrderVoice() => Pick(orderVoice ??= Load(OrderVoicePaths));

        private static AudioClip Pick(AudioClip[] clips) => clips.Length == 0 ? null : clips[UnityEngine.Random.Range(0, clips.Length)];

        private static AudioClip[] Load(string[] paths)
        {
            var list = new List<AudioClip>(paths.Length);
            foreach (string path in paths)
            {
                var clip = Resources.Load<AudioClip>(path);
                if (clip != null) list.Add(clip);
            }
            return list.ToArray();
        }
    }
}
