using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
using UnityEngine.Windows.Speech;
#endif

namespace Emberfield.Presentation
{
    /// <summary>A source of recognized phrases. It only listens for the phrases it was loaded with.</summary>
    public interface IVoiceRecognizer : IDisposable
    {
        bool IsAvailable { get; }
        bool IsListening { get; }
        /// <summary>Why recognition is unavailable or last stopped; null while it works.</summary>
        string Problem { get; }
        event Action<string> Recognized;
        void Load(IReadOnlyList<string> phrases);
        void Start();
        void Stop();
    }

    /// <summary>
    /// Keyword recognition through Windows' own speech system: a fixed phrase list matched on this device.
    /// Loading the list only prepares the recognizer; the microphone opens on Start.
    /// </summary>
    public sealed class WindowsVoiceRecognizer : IVoiceRecognizer
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
        private KeywordRecognizer keywords;
        private IReadOnlyList<string> loaded;
        private bool subscribed, disposed;
        public event Action<string> Recognized;
        public string Problem { get; private set; }
        public bool IsAvailable => keywords != null;
        public bool IsListening => keywords != null && keywords.IsRunning;

        public static bool IsSupported
        {
            get { try { return PhraseRecognitionSystem.isSupported; } catch (Exception) { return false; } }
        }

        public WindowsVoiceRecognizer()
        {
            if (!IsSupported) { Problem = "Windows speech recognition is not available on this device."; return; }
            PhraseRecognitionSystem.OnError += OnError; subscribed = true;
        }

        public void Load(IReadOnlyList<string> phrases)
        {
            if (disposed || !IsSupported || phrases == null || keywords != null && ReferenceEquals(phrases, loaded)) return;
            Release();
            // Speech support varies between Windows installs; any failure leaves voice unavailable, never the match broken.
            try
            {
                keywords = new KeywordRecognizer(phrases.ToArray(), ConfidenceLevel.Medium);
                keywords.OnPhraseRecognized += OnPhrase;
                loaded = phrases; Problem = null;
            }
            catch (Exception error) { keywords = null; Problem = "Windows could not prepare the voice commands: " + error.Message; }
        }

        public void Start()
        {
            if (keywords == null || keywords.IsRunning) return;
            Problem = null;
            try { keywords.Start(); }
            catch (Exception error) { Problem = "Listening could not start: " + error.Message; }
        }

        public void Stop() { if (keywords != null && keywords.IsRunning) keywords.Stop(); }

        private void OnPhrase(PhraseRecognizedEventArgs args)
        {
            if (args.confidence != ConfidenceLevel.Rejected) Recognized?.Invoke(args.text);
        }

        private void OnError(SpeechError error) =>
            Problem = error == SpeechError.MicrophoneUnavailable ? "No microphone is available, or Windows blocks microphone access for this game."
                : error == SpeechError.AudioQualityFailure ? "The microphone signal is too quiet or too noisy."
                : "Windows speech recognition stopped (" + error + ").";

        private void Release()
        {
            if (keywords == null) return;
            keywords.OnPhraseRecognized -= OnPhrase;
            if (keywords.IsRunning) keywords.Stop();
            keywords.Dispose(); keywords = null; loaded = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Release();
            if (subscribed) PhraseRecognitionSystem.OnError -= OnError;
        }
#else
        public event Action<string> Recognized { add { } remove { } }
        public static bool IsSupported => false;
        public string Problem => "Voice commands need Windows speech recognition.";
        public bool IsAvailable => false;
        public bool IsListening => false;
        public void Load(IReadOnlyList<string> phrases) { }
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
#endif
    }

    /// <summary>Delivers typed phrases as if they were heard. Used by tests and the packaged voice smoke.</summary>
    public sealed class ScriptedVoiceRecognizer : IVoiceRecognizer
    {
        private HashSet<string> listed = new HashSet<string>(StringComparer.Ordinal);
        public bool IsAvailable { get; set; } = true;
        public bool IsListening { get; private set; }
        public string Problem => IsAvailable ? null : "The scripted recognizer is switched off.";
        public IReadOnlyList<string> Phrases { get; private set; } = Array.Empty<string>();
        /// <summary>Like a keyword recognizer, only loaded phrases are heard unless this allows free text.</summary>
        public bool AllowUnlistedPhrases { get; set; }
        public event Action<string> Recognized;

        public void Load(IReadOnlyList<string> phrases)
        {
            Phrases = phrases ?? Array.Empty<string>();
            listed = new HashSet<string>(Phrases, StringComparer.Ordinal);
        }
        public void Start() { if (IsAvailable) IsListening = true; }
        public void Stop() => IsListening = false;
        /// <summary>Returns false when nothing was listening for this phrase.</summary>
        public bool Say(string phrase)
        {
            if (!IsListening || !AllowUnlistedPhrases && !listed.Contains(phrase)) return false;
            Recognized?.Invoke(phrase);
            return true;
        }
        public void Dispose() => IsListening = false;
    }
}
