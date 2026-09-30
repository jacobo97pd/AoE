using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Emberfield.Presentation
{
    public sealed class ReferenceCharacterReview : MonoBehaviour
    {
        public const string SceneName = "CharacterCollection";
        public Camera ViewCamera;
        public RenderPipelineAsset Pipeline;
        private RenderPipelineAsset previousPipeline;
        private GameObject character;
        private CorsairAnimationDriver driver;
        private int selected, view = 1;
        private Vector2 scroll;
        private bool capturing;
        private string state = "Idle";
        private Bounds bounds;
        private GUIStyle title, copy, button, small;
        private static Scene suspendedScene;
        private static GameObject[] suspendedRoots;
        private static bool opening;
        private void Awake()
        {
            previousPipeline = QualitySettings.renderPipeline;
            if (Pipeline) QualitySettings.renderPipeline = Pipeline;
        }
        private void OnDestroy() => RestorePipeline();
        private void RestorePipeline()
        {
            if (Application.isPlaying && Pipeline && QualitySettings.renderPipeline == Pipeline)
                QualitySettings.renderPipeline = previousPipeline;
        }
        public static bool CanOpen(OnlineState state, bool busy = false) => !busy && (state == null || string.IsNullOrEmpty(state.status) || state.status == "idle" || state.status == "finished" || state.status == "aborted");

        public static void OpenFromGame()
        {
            if (opening || SceneManager.GetSceneByName(SceneName).isLoaded) return;
            var match = FindFirstObjectByType<MatchController>();
            if (match != null && !CanOpen(match.Online.State, match.Online.Busy)) { match.Shell.Close(); match.Online.Open(); return; }
            opening = true; suspendedScene = SceneManager.GetActiveScene();
            suspendedRoots = suspendedScene.GetRootGameObjects().Where(o => o.activeSelf).ToArray();
            var operation = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            operation.completed += _ =>
            {
                var scene = SceneManager.GetSceneByName(SceneName);
                if (scene.isLoaded)
                {
                    foreach (var root in suspendedRoots) if (root) root.SetActive(false);
                    SceneManager.SetActiveScene(scene);
                }
                opening = false;
            };
        }

        public void ReturnToGame()
        {
            RestorePipeline();
            if (suspendedScene.IsValid() && suspendedScene.isLoaded && suspendedRoots != null)
            {
                SceneManager.SetActiveScene(suspendedScene);
                foreach (var root in suspendedRoots) if (root) root.SetActive(true);
                suspendedRoots = null;
                var unload = SceneManager.UnloadSceneAsync(SceneName);
                unload.completed += _ => { ReferenceCharacterVisuals.ReleasePrefabCache(); Resources.UnloadUnusedAssets(); };
            }
            else SceneManager.LoadScene("Greybox");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OpenCaptureScene()
        {
            if (Environment.GetCommandLineArgs().Contains("-characterCollectionReview") && SceneManager.GetActiveScene().name != SceneName)
                SceneManager.LoadScene(SceneName);
        }

        private void Start()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60;
            Select(0);
            var args = Environment.GetCommandLineArgs(); int option = Array.IndexOf(args, "-characterCollectionReview");
            if (option >= 0 && option + 1 < args.Length) { capturing = true; StartCoroutine(CaptureGuarded(args[option + 1])); }
        }
        public void Select(int index)
        {
            var entries = ReferenceCharacterVisuals.Entries;
            if (entries.Length == 0) return;
            selected = (index + entries.Length) % entries.Length;
            if (character) { character.SetActive(false); Destroy(character); }
            var prefab = ReferenceCharacterVisuals.Prefab(entries[selected].id);
            if (!prefab) return;
            character = Instantiate(prefab); character.name = entries[selected].name;
            SceneManager.MoveGameObjectToScene(character, gameObject.scene);
            driver = character.GetComponent<CorsairAnimationDriver>();
            bounds = new Bounds(Vector3.up * entries[selected].heightMetres * .5f, Vector3.one * .1f);
            foreach (var renderer in character.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            Play("Idle"); SetView(view);
        }
        private void Play(string value) { state = value; driver?.PlayPreview(value); }
        public void SetView(int index)
        {
            view = Mathf.Clamp(index, 0, 2);
            if (!ViewCamera) return;
            float height = Mathf.Max(1.5f, bounds.size.y), span = Mathf.Max(bounds.size.x, height);
            var target = Vector3.up * height * (view == 0 ? .68f : .47f);
            var direction = (view == 2 ? new Vector3(1, 1.65f, 1.2f) : new Vector3(.55f, view == 0 ? .19f : .42f, 1.4f)).normalized;
            ViewCamera.orthographic = view == 2;
            ViewCamera.fieldOfView = view == 0 ? 30 : 36;
            ViewCamera.orthographicSize = Mathf.Max(2.5f, span * 1.18f);
            ViewCamera.transform.position = target + direction * span * (view == 0 ? 1.6f : view == 2 ? 4.5f : 2.5f);
            ViewCamera.transform.LookAt(target);
            if (character && character.TryGetComponent<LODGroup>(out var lod)) lod.ForceLOD(view == 2 ? -1 : 0);
        }
        private void Update()
        {
            if (capturing || Keyboard.current == null) return;
            var key = Keyboard.current;
            if (key.leftArrowKey.wasPressedThisFrame) Select(selected - 1);
            if (key.rightArrowKey.wasPressedThisFrame) Select(selected + 1);
            if (key.digit1Key.wasPressedThisFrame) SetView(0);
            if (key.digit2Key.wasPressedThisFrame) SetView(1);
            if (key.digit3Key.wasPressedThisFrame) SetView(2);
            if (key.rKey.wasPressedThisFrame) Play("Run");
            if (key.dKey.wasPressedThisFrame) Play("Death");
            if (key.iKey.wasPressedThisFrame) Play("Idle");
        }
        private void OnGUI()
        {
            if (capturing) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
                copy = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
                small = new GUIStyle(copy) { fontSize = 13 };
                button = new GUIStyle(GUI.skin.button) { fontSize = 15, wordWrap = true };
            }
            float scale = Screen.height / 900f; GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            GUI.Box(new Rect(18, 18, 290, 862), GUIContent.none);
            GUI.Label(new Rect(36, 32, 260, 42), "PERSONAJES", title);
            GUI.Label(new Rect(36, 77, 255, 45), "Reinos, pueblos y criaturas", copy);
            var entries = ReferenceCharacterVisuals.Entries;
            scroll = GUI.BeginScrollView(new Rect(31, 133, 268, 650), scroll, new Rect(0, 0, 245, entries.Length * 52));
            for (int i = 0; i < entries.Length; i++)
            {
                GUI.color = i == selected ? new Color(1, .82f, .47f) : Color.white;
                if (GUI.Button(new Rect(0, i * 52, 242, 46), entries[i].name, button)) Select(i);
            }
            GUI.color = Color.white; GUI.EndScrollView();
            if (GUI.Button(new Rect(35, 814, 255, 46), "VOLVER AL JUEGO", button)) ReturnToGame();
            if (entries.Length == 0) { GUI.Label(new Rect(350, 30, 700, 60), "La colección todavía no está importada.", title); return; }
            var entry = entries[selected];
            GUI.Label(new Rect(340, 30, width - 375, 46), entry.name, title);
            GUI.Label(new Rect(342, 83, width - 390, 50), ContentRealms.DisplayName(entry.realm).ToUpperInvariant() + "  ·  " + entry.role, copy);
            GUI.Box(new Rect(328, 768, width - 346, 112), GUIContent.none);
            string[] labels = { "I · REPOSO", "R · CORRER", "D · CAER", "1 · CERCA", "2 · MEDIA", "3 · RTS" };
            for (int i = 0; i < labels.Length; i++)
                if (GUI.Button(new Rect(342 + i * (width - 378) / 6, 789, (width - 390) / 6 - 7, 42), labels[i], button))
                { if (i < 3) Play(new[] { "Idle", "Run", "Death" }[i]); else SetView(i - 3); }
            GUI.Label(new Rect(342, 837, width - 380, 29), "Modelos 3D de interpretación. Flechas: cambiar personaje. Caída: una reproducción.", small);
        }
        [Serializable] private sealed class PoseEvidence { public string state; public bool entered, deforms; public float duration, displacement; }
        [Serializable] private sealed class CharacterEvidence { public string id, name; public bool passed; public List<PoseEvidence> poses = new List<PoseEvidence>(); }
        [Serializable] private sealed class Report
        {
            public string buildGuid, unity, generatedUtc, graphicsDevice;
            public bool passed;
            public string scope = "Actual native Unity renders and skeletal deformation. Artistic interpretations, not a claim of reference identity or a performance benchmark.";
            public List<CharacterEvidence> characters = new List<CharacterEvidence>();
        }
        private IEnumerator Capture(string folder)
        {
            Directory.CreateDirectory(folder);
            var report = new Report { buildGuid = Application.buildGUID, unity = Application.unityVersion, generatedUtc = DateTime.UtcNow.ToString("O"), graphicsDevice = SystemInfo.graphicsDeviceName, passed = ReferenceCharacterVisuals.Entries.Length == 26 };
            for (int i = 0; i < ReferenceCharacterVisuals.Entries.Length; i++)
            {
                Select(i); yield return null;
                var entry = ReferenceCharacterVisuals.Entries[i]; string destination = Path.Combine(folder, entry.id); Directory.CreateDirectory(destination);
                var result = new CharacterEvidence { id = entry.id, name = entry.name, passed = driver && driver.HasValidRig };
                report.characters.Add(result);
                if (!result.passed) { report.passed = false; continue; }
                Play("Idle"); yield return new WaitForSeconds(.12f);
                for (int camera = 0; camera < 3; camera++)
                { SetView(camera); yield return new WaitForEndOfFrame(); CrimsonCorsairReview.CaptureCamera(ViewCamera, 1600, 900, Path.Combine(destination, new[] { "close", "medium", "rts" }[camera] + ".png")); }
                var skin = character.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(s => s.sharedMesh.vertexCount).First(); var baked = new Mesh();
                foreach (string clip in new[] { "Idle", "Run", "Death" })
                {
                    var animator = driver.Animator; animator.speed = 0; animator.Play(clip, 0, 0); animator.Update(0);
                    skin.BakeMesh(baked); var before = baked.vertices;
                    float sample = driver.Duration(clip) * (clip == "Death" ? .97f : .39f);
                    animator.speed = 1; animator.Update(sample); animator.speed = 0; skin.BakeMesh(baked); var after = baked.vertices;
                    float movement = 0; for (int v = 0; v < before.Length; v += Math.Max(1, before.Length / 2000)) movement = Mathf.Max(movement, Vector3.Distance(before[v], after[v]));
                    var pose = new PoseEvidence { state = clip, entered = animator.GetCurrentAnimatorStateInfo(0).IsName(clip), deforms = movement > .00001f, duration = driver.Duration(clip), displacement = movement };
                    result.poses.Add(pose); result.passed &= pose.entered && pose.deforms;
                    SetView(1); yield return new WaitForEndOfFrame(); CrimsonCorsairReview.CaptureCamera(ViewCamera, 1600, 900, Path.Combine(destination, clip.ToLowerInvariant() + ".png"));
                }
                Destroy(baked); report.passed &= result.passed;
                File.WriteAllText(Path.Combine(folder, "collection-review.json"), JsonUtility.ToJson(report, true));
            }
            File.WriteAllText(Path.Combine(folder, "collection-review.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 1);
        }
        private IEnumerator CaptureGuarded(string folder)
        {
            var capture = Capture(folder);
            while (true)
            {
                Exception failure = null; bool more = false; object next = null;
                try { more = capture.MoveNext(); if (more) next = capture.Current; }
                catch (Exception error) { failure = error; }
                if (failure != null)
                {
                    Debug.LogException(failure); Directory.CreateDirectory(folder);
                    File.WriteAllText(Path.Combine(folder, "capture-failure.txt"), failure.ToString());
                    Application.Quit(1); yield break;
                }
                if (!more) yield break;
                yield return next;
            }
        }
    }
}
