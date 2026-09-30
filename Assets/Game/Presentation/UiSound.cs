using System.Collections.Generic;
using UnityEngine;

namespace Emberfield.Presentation
{
    public enum UiCue { Click, Confirm }

    /// <summary>
    /// Chrome the game had none of before this pass (docs/art/audio.md): a small pooled voice for every button
    /// press across the HUD and its panels, real CC0 clips picked at random with a little pitch variation. The
    /// pool is created lazily on first use — a menu button can be pressed before any match, and so before any
    /// other audio system exists — and then lives for the process, the same "cached once" spirit as
    /// FrontierSounds and FrontierAmbience. It follows the game's one mute switch (SliceFeedback.Muted, kept in
    /// sync with AlphaSettings.SoundEnabled by AlphaControls) and the shared AudioListener.volume master level;
    /// it does not read SoundVolume itself, exactly like SliceFeedback's own voices.
    /// </summary>
    public static class UiSound
    {
        private const int VoiceCapacity = 4;
        private const float Volume = .22f;

        private static readonly string[] ClickPaths =
        {
            "Audio/Ui/Click/ui_click_01", "Audio/Ui/Click/ui_click_02", "Audio/Ui/Click/ui_click_03",
            "Audio/Ui/Click/ui_click_04", "Audio/Ui/Click/ui_click_05", "Audio/Ui/Click/ui_click_06",
        };
        private static readonly string[] ConfirmPaths =
        {
            "Audio/Ui/Confirm/ui_confirm_01", "Audio/Ui/Confirm/ui_confirm_02",
            "Audio/Ui/Confirm/ui_confirm_03", "Audio/Ui/Confirm/ui_confirm_04",
        };

        private static AudioSource[] voices;
        private static AudioClip[] clickClips, confirmClips;
        private static int nextVoice;

        public static int PlayedSounds { get; private set; }

        public static void Play(UiCue cue)
        {
            if (SliceFeedback.Muted) return;
            EnsurePool();
            var clips = cue == UiCue.Confirm ? confirmClips : clickClips;
            if (clips.Length == 0) return;
            var voice = voices[nextVoice++ % voices.Length];
            voice.clip = clips[Random.Range(0, clips.Length)];
            voice.pitch = Random.Range(.97f, 1.03f);
            voice.Play();
            PlayedSounds++;
        }

        private static void EnsurePool()
        {
            if (voices != null) return;
            clickClips = Load(ClickPaths);
            confirmClips = Load(ConfirmPaths);
            var root = new GameObject("UI sound pool");
            Object.DontDestroyOnLoad(root);
            voices = new AudioSource[VoiceCapacity];
            for (int i = 0; i < voices.Length; i++)
            {
                var go = new GameObject("UI voice " + i); go.transform.SetParent(root.transform, false);
                voices[i] = go.AddComponent<AudioSource>();
                voices[i].playOnAwake = false; voices[i].spatialBlend = 0; voices[i].volume = Volume;
            }
        }

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
