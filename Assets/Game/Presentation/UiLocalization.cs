using System.Collections.Generic;
using Emberfield.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // Shows every uGUI Text, and every world TextMesh written through SetText, in the chosen language just
    // before the canvases draw. Screens keep composing text in their source language and the catalogs
    // translate it at display time, so no screen has to know about languages. Text a player types is never
    // touched. Nothing here allocates on a frame where no text changed: the canvases are found again only
    // when one may have come or gone, and a TextMesh (whose text Unity can only hand back as a new string)
    // is translated when it is written instead of being read back every frame.
    public static class UiLocalization
    {
        private sealed class Shown { public string Source, Output; public GameLanguage Language; public bool Typed, Translated; }
        private static readonly Dictionary<Object, Shown> shown = new Dictionary<Object, Shown>();
        private static readonly List<Text> texts = new List<Text>();
        private static readonly List<Object> gone = new List<Object>();
        private static readonly List<Canvas> canvases = new List<Canvas>();
        private static readonly List<TextMesh> meshes = new List<TextMesh>();
        private static TextTranslator spanish, english;
        private static int pruneFrame, canvasScanFrame = -1, scannedRaycasters = -1;
        private static bool canvasesChanged = true;
        // Every canvas the game makes carries a GraphicRaycaster, so a change in their number is a canvas made,
        // shown, hidden or destroyed; a scene change or a destroyed canvas also looks again. The slow sweep is
        // for a canvas without one, which would otherwise wait for the next scene.
        private const int CanvasSweepFrames = 60;

        /// <summary>Tests that assert source text turn translation off; the game always leaves it on.</summary>
        public static bool Enabled { get; set; } = true;
        public static GameLanguage Language { get; private set; } = GameLanguage.Spanish;
        public static TextTranslator Current
        {
            get
            {
                try
                {
                    return Language == GameLanguage.English
                        ? english ?? (english = EnglishCatalog.Create()) : spanish ?? (spanish = SpanishCatalog.Create());
                }
                catch (System.ArgumentException error)
                {
                    // A broken catalog must not break every frame: show source text and report once.
                    Enabled = false;
                    Debug.LogError("Translation catalog could not be built; showing source text. " + error.Message);
                    return new TextTranslator(Language);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDomain()
        {
            Canvas.willRenderCanvases -= Apply;
            SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneUnloaded -= SceneUnloaded;
            shown.Clear(); canvases.Clear(); meshes.Clear(); canvasesChanged = true; Enabled = true; Language = GameLanguage.Spanish;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            Canvas.willRenderCanvases -= Apply; Canvas.willRenderCanvases += Apply;
            SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.sceneUnloaded -= SceneUnloaded; SceneManager.sceneUnloaded += SceneUnloaded;
        }

        private static void SceneLoaded(Scene scene, LoadSceneMode mode) => canvasesChanged = true;
        private static void SceneUnloaded(Scene scene) => canvasesChanged = true;

        /// <summary>Texts already on screen switch at the next frame.</summary>
        public static void SetLanguage(GameLanguage language) => Language = language;

        public static string Translate(string text) => Enabled ? Current.Translate(text) : text;

        /// <summary>
        /// Writes a world label in the chosen language; it follows later language changes. A TextMesh is only
        /// translated when written through here.
        /// </summary>
        public static void SetText(TextMesh mesh, string source)
        {
            if (mesh == null) return;
            if (!shown.TryGetValue(mesh, out var entry)) { shown.Add(mesh, entry = new Shown()); meshes.Add(mesh); }
            entry.Source = source; entry.Translated = Enabled; entry.Language = Language;
            entry.Output = Enabled && !string.IsNullOrEmpty(source) ? Current.Translate(source) : source;
            mesh.text = entry.Output;
        }

        /// <summary>Translates everything on screen now, for code that reads labels back in the same frame.</summary>
        public static void ApplyNow() { canvasesChanged = true; Apply(); }

        private static void Apply()
        {
            if (!Enabled) return;
            using var marker = PresentationMarkers.Localization.Auto();
            var translator = Current;
            FindCanvases();
            foreach (var canvas in canvases)
            {
                if (!canvas.isRootCanvas) continue;
                canvas.GetComponentsInChildren(false, texts);
                foreach (var text in texts) Show(text, text.text, translator);
            }
            for (int i = meshes.Count - 1; i >= 0; i--)
            {
                var mesh = meshes[i];
                if (mesh == null) { meshes.RemoveAt(i); continue; }
                var entry = shown[mesh];
                if (entry.Translated && entry.Language == Language) continue;
                entry.Output = string.IsNullOrEmpty(entry.Source) ? entry.Source : translator.Translate(entry.Source);
                entry.Translated = true; entry.Language = Language;
                mesh.text = entry.Output;
            }
            if (Time.frameCount >= pruneFrame) Prune();
        }

        private static void FindCanvases()
        {
            int raycasters = RaycasterManager.GetRaycasters().Count;
            bool destroyed = false;
            foreach (var canvas in canvases) if (canvas == null) { destroyed = true; break; }
            if (!canvasesChanged && !destroyed && raycasters == scannedRaycasters && Time.frameCount < canvasScanFrame + CanvasSweepFrames) return;
            canvasesChanged = false; scannedRaycasters = raycasters; canvasScanFrame = Time.frameCount;
            canvases.Clear();
            // Inactive ones too: a canvas shown later is then already known.
            canvases.AddRange(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        }

        private static void Show(Text text, string current, TextTranslator translator)
        {
            if (string.IsNullOrEmpty(current)) return;
            if (!shown.TryGetValue(text, out var entry))
            {
                entry = new Shown { Typed = IsTyped(text) };
                shown.Add(text, entry);
            }
            if (entry.Typed) return;
            if (ReferenceEquals(current, entry.Output) || current == entry.Output)
            {
                if (entry.Language == Language) return;
                current = entry.Source; // The language changed: translate the original again.
            }
            string output = translator.Translate(current);
            entry.Source = current; entry.Output = output; entry.Language = Language;
            if (text.text != output) text.text = output;
        }

        private static bool IsTyped(Text text)
        {
            var field = text.GetComponentInParent<InputField>(true);
            return field != null && field.textComponent == text;
        }

        private static void Prune()
        {
            pruneFrame = Time.frameCount + 300;
            gone.Clear();
            foreach (var target in shown.Keys) if (target == null) gone.Add(target);
            foreach (var target in gone) shown.Remove(target);
        }
    }
}
