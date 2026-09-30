using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Presentation
{
    // Appended, never reordered: the cue is used as an index into the pooled voices and materials.
    public enum FeedbackCue { Order, Gather, Impact, Complete, Defeat, Objective, Chop, Mine }

    // Bounded cosmetic feedback, fed exclusively by visible presentation observations.
    // Clips are original deterministic synthesis; there are no gameplay animation events.
    public sealed class SliceFeedback : IDisposable
    {
        public const int ParticleCapacity = 16, VoiceCapacity = 6;
        private const int CueCount = 8;
        // Order's optional voiced acknowledgement (see FrontierClips.OrderVoice): occasional, quiet, its own
        // source so a rare bark never steals a slot from the round-robin pool every other cue actually depends on.
        private const float OrderVoiceChance = .25f, OrderVoiceVolume = .11f;
        private readonly Transform root;
        private readonly Camera camera;
        private readonly Transform[] particles = new Transform[ParticleCapacity];
        private readonly float[] born = new float[ParticleCapacity];
        private readonly Vector3[] origins = new Vector3[ParticleCapacity];
        private readonly AudioSource[] voices = new AudioSource[VoiceCapacity];
        private readonly AudioSource orderVoice;
        private readonly AudioClip[] clips = new AudioClip[CueCount];
        // Synthesised once per process, as FrontierAmbience keeps its beds: a second match costs nothing to voice.
        private static readonly AudioClip[] synthesised = new AudioClip[CueCount];
        private readonly Material[] materials = new Material[CueCount];
        private readonly float[] nextCue = new float[CueCount];
        private readonly Mesh mesh;
        private int nextParticle, nextVoice;
        private static bool muted;
        public static bool Muted => muted;
        public int EmittedParticles { get; private set; }
        public int PlayedSounds { get; private set; }
        public int ActiveParticles { get { int count = 0; foreach (var item in particles) if (item.gameObject.activeSelf) count++; return count; } }
        public SliceFeedback(Transform parent, Camera camera)
        {
            this.camera = camera; root = new GameObject("Bounded slice feedback").transform; root.SetParent(parent, false);
            if (camera.GetComponent<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
            var colors = new[] { new Color(.42f, .76f, .92f), new Color(.91f, .70f, .33f), new Color(1, .74f, .46f), new Color(.52f, .91f, .65f), new Color(.55f, .49f, .38f), new Color(1, .83f, .37f),
                new Color(.58f, .44f, .26f), new Color(.72f, .76f, .80f) };
            for (int i = 0; i < clips.Length; i++)
            {
                materials[i] = new Material(Resources.Load<Material>("Materials/Greybox")) { color = colors[i], enableInstancing = true, name = "Feedback " + (FeedbackCue)i };
                if (synthesised[i] == null) synthesised[i] = FrontierSounds.Cue((FeedbackCue)i);
                clips[i] = synthesised[i];
            }
            mesh = new Mesh { name = "Original six-sided spark" };
            mesh.vertices = new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            mesh.triangles = new[] { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 }; mesh.RecalculateNormals();
            for (int i = 0; i < particles.Length; i++)
            {
                var go = new GameObject("Pooled spark " + i); particles[i] = go.transform; particles[i].SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = materials[0];
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; go.SetActive(false);
            }
            for (int i = 0; i < voices.Length; i++)
            {
                var go = new GameObject("Feedback voice " + i); go.transform.SetParent(root, false);
                voices[i] = go.AddComponent<AudioSource>(); voices[i].playOnAwake = false; voices[i].spatialBlend = 0; voices[i].volume = .18f; voices[i].mute = muted;
            }
            var voiceGo = new GameObject("Order acknowledgement voice"); voiceGo.transform.SetParent(root, false);
            orderVoice = voiceGo.AddComponent<AudioSource>();
            orderVoice.playOnAwake = false; orderVoice.spatialBlend = 0; orderVoice.volume = OrderVoiceVolume; orderVoice.mute = muted;
        }
        public void ToggleMute() { muted = !muted; foreach (var voice in voices) voice.mute = muted; orderVoice.mute = muted; }
        public void Emit(FeedbackCue cue, Vector3 position, bool sound = true)
        {
            var screen = camera.WorldToViewportPoint(position);
            if (screen.z <= 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) return;
            int index = (int)cue; float now = Time.unscaledTime;
            // Aggregate noisy battles/gathering, rather than emitting once per simulation tick.
            if (now < nextCue[index]) return;
            nextCue[index] = now + (cue == FeedbackCue.Gather ? .22f : .09f);
            if (cue != FeedbackCue.Order)
            {
                int slot = nextParticle++ % particles.Length; born[slot] = now; origins[slot] = position + Vector3.up * .4f;
                particles[slot].position = origins[slot]; particles[slot].localScale = Vector3.one * .14f;
                particles[slot].GetComponent<Renderer>().sharedMaterial = materials[index]; particles[slot].gameObject.SetActive(true); EmittedParticles++;
            }
            if (sound && !muted)
            {
                var voice = voices[nextVoice++ % voices.Length];
                // A cataloged real clip (docs/art/audio.md) swaps in for the cues with a good recording; a cue
                // with none (Gather's weak approximation, Defeat's already-well-tuned synth) keeps its synthesis.
                var real = FrontierClips.Real(cue);
                voice.clip = real ?? clips[index]; voice.pitch = real != null ? UnityEngine.Random.Range(.96f, 1.04f) : 1f;
                voice.Play(); PlayedSounds++;
                if (cue == FeedbackCue.Order && UnityEngine.Random.value < OrderVoiceChance)
                {
                    var line = FrontierClips.OrderVoice();
                    if (line != null) { orderVoice.clip = line; orderVoice.pitch = UnityEngine.Random.Range(.97f, 1.03f); orderVoice.Play(); PlayedSounds++; }
                }
            }
        }
        public void Update()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < particles.Length; i++) if (particles[i].gameObject.activeSelf)
            {
                float age = (now - born[i]) / .42f;
                if (age >= 1) { particles[i].gameObject.SetActive(false); continue; }
                particles[i].position = origins[i] + Vector3.up * age * .8f;
                particles[i].localScale = new Vector3(.14f, .22f, .14f) * (1 - age);
                particles[i].localRotation = Quaternion.Euler(age * 150, i * 37, age * 110);
            }
        }
        public void Dispose()
        {
            foreach (var voice in voices) if (voice != null) voice.Stop();
            if (orderVoice != null) orderVoice.Stop();
            // The clips stay in the shared cache for the next match.
            foreach (var material in materials) UnityEngine.Object.Destroy(material);
            UnityEngine.Object.Destroy(mesh); if (root != null) UnityEngine.Object.Destroy(root.gameObject);
        }
    }
}
