using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Emberfield.Presentation
{
    /// <summary>A real animated asset viewer. The play button enters the existing offline game.</summary>
    public sealed class CrimsonCorsairReview : MonoBehaviour
    {
        [Serializable] public sealed class CharacterEntry
        {
            public string Id, DisplayName, Role;
            public GameObject Character;
        }

        // Optional roster keeps the original one-captain scene compatible. The
        // gameplay integration registers the crew route once its scenario exists.
        public CharacterEntry[] Roster;
        public bool CrewReview;
        public int CharacterIndex;
        public static Action StartCrewMatch;
        public GameObject Character;
        public Animator Animator;
        public Camera[] Cameras;
        public RenderPipelineAsset Pipeline;
        public bool Controls = true;
        public int View;
        public Camera Active => Cameras[View];
        // Six shared clips, then the optional Work and Aim loops a character carries.
        private static readonly string[] AllStates = { "Idle", "Walk", "Run", "Attack", "Hit", "Death", "Work", "Aim" };
        private static readonly string[] Labels = { "I · Reposo", "W · Caminar", "R · Correr", "A · Atacar", "H · Impacto", "D · Caer", "E · Trabajo", "P · Apuntar" };
        private RenderPipelineAsset previous;
        private Vector3[] positions;
        private Quaternion[] rotations;
        private float[] fovs, sizes;
        private bool capturing, paused;
        private bool workAvailable;
        private string[] availableStates = { "Idle", "Walk", "Run", "Attack", "Hit", "Death" };
        private string[] AvailableStates => availableStates;
        private string currentState = "Idle";
        private CorsairAnimationDriver driver;

        private void Awake()
        {
            previous = QualitySettings.renderPipeline;
            if (Pipeline) QualitySettings.renderPipeline = Pipeline;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            if (Roster != null && Roster.Length > 0) SelectCharacter(CharacterIndex);
            else { driver = Character ? Character.GetComponent<CorsairAnimationDriver>() : null; RefreshAvailableStates(); }
        }

        private void Start()
        {
            Remember(); SetView(0); Play("Idle");
            var args = Environment.GetCommandLineArgs();
            int review = Array.IndexOf(args, CrewReview ? "-pirateCrewReview" : "-corsairReview");
            if (review >= 0 && review + 1 < args.Length)
            {
                capturing = true; Controls = false;
                StartCoroutine(CrewReview ? CaptureRoster(args[review + 1]) : Capture(args[review + 1]));
            }
            // A recorded full match and the product-shell and voice smokes start from the ordinary
            // skirmish scene, not a review fixture.
            else if (Array.IndexOf(args, "-emberfieldFullMatch") >= 0 || Array.IndexOf(args, "-emberfieldProductSmoke") >= 0 ||
                Array.IndexOf(args, "-emberfieldVoiceSmoke") >= 0)
                UnityEngine.SceneManagement.SceneManager.LoadScene("Greybox");
            else if (CrewReview && (Array.IndexOf(args, "-emberfieldPirateCrew") >= 0 || Array.IndexOf(args, "-pirateCrewGameReview") >= 0)) PlayGame();
            else if (!CrewReview && (Array.IndexOf(args, "-emberfieldCorsairHero") >= 0 || CorsairGameSmoke.Requested)) PlayGame();
        }

        private void OnDestroy()
        {
            if (Application.isPlaying && QualitySettings.renderPipeline == Pipeline) QualitySettings.renderPipeline = previous;
        }

        private void Remember()
        {
            if (positions != null || Cameras == null) return;
            positions = new Vector3[Cameras.Length]; rotations = new Quaternion[Cameras.Length];
            fovs = new float[Cameras.Length]; sizes = new float[Cameras.Length];
            for (int i = 0; i < Cameras.Length; i++)
            {
                positions[i] = Cameras[i].transform.position; rotations[i] = Cameras[i].transform.rotation;
                fovs[i] = Cameras[i].fieldOfView; sizes[i] = Cameras[i].orthographicSize;
            }
        }

        public void SetView(int index)
        {
            Remember(); View = Mathf.Clamp(index, 0, Cameras.Length - 1);
            for (int i = 0; i < Cameras.Length; i++) Cameras[i].enabled = i == View;
            Active.transform.SetPositionAndRotation(positions[View], rotations[View]);
            Active.fieldOfView = fovs[View]; Active.orthographicSize = sizes[View];
            if (Character && Character.TryGetComponent<LODGroup>(out var lod)) lod.ForceLOD(View == 2 ? -1 : 0);
        }

        public void SelectCharacter(int index)
        {
            if (Roster == null || Roster.Length == 0) return;
            CharacterIndex = Mathf.Clamp(index, 0, Roster.Length - 1);
            for (int i = 0; i < Roster.Length; i++) if (Roster[i].Character) Roster[i].Character.SetActive(i == CharacterIndex);
            Character = Roster[CharacterIndex].Character;
            Animator = Character ? Character.GetComponent<Animator>() : null;
            driver = Character ? Character.GetComponent<CorsairAnimationDriver>() : null;
            RefreshAvailableStates();
            if (Application.isPlaying) Play("Idle");
            if (Cameras != null && Cameras.Length > 0) SetView(View);
        }

        private void RefreshAvailableStates()
        {
            var states = new List<string>();
            for (int i = 0; i < 6; i++) states.Add(AllStates[i]);
            if (Animator && Animator.runtimeAnimatorController)
                foreach (string optional in new[] { "Work", "Aim" })
                    foreach (var clip in Animator.runtimeAnimatorController.animationClips)
                        if (clip && (clip.name == optional || clip.name == "Corsair_" + optional)) { states.Add(optional); break; }
            availableStates = states.ToArray();
            workAvailable = states.Contains("Work");
        }

        private void Play(string state)
        {
            currentState = state; paused = false;
            if (driver) driver.PlayPreview(state);
            else if (Animator) { Animator.speed = 1; Animator.CrossFadeInFixedTime(state, .1f, 0, 0); }
        }

        private void PlayGame()
        {
            if (CrewReview) StartCrewMatch?.Invoke();
            else CorsairHeroScenario.StartReviewMatch();
        }

        private void Update()
        {
            if (capturing) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SetView(0);
                if (keyboard.digit2Key.wasPressedThisFrame) SetView(1);
                if (keyboard.digit3Key.wasPressedThisFrame) SetView(2);
                if (Roster != null && Roster.Length > 1)
                {
                    if (keyboard.leftArrowKey.wasPressedThisFrame) SelectCharacter((CharacterIndex + Roster.Length - 1) % Roster.Length);
                    if (keyboard.rightArrowKey.wasPressedThisFrame) SelectCharacter((CharacterIndex + 1) % Roster.Length);
                }
                if (keyboard.iKey.wasPressedThisFrame) Play("Idle");
                if (keyboard.wKey.wasPressedThisFrame) Play("Walk");
                if (keyboard.rKey.wasPressedThisFrame) Play("Run");
                if (keyboard.aKey.wasPressedThisFrame) Play("Attack");
                if (keyboard.hKey.wasPressedThisFrame) Play("Hit");
                if (keyboard.dKey.wasPressedThisFrame) Play("Death");
                if (workAvailable && keyboard.eKey.wasPressedThisFrame) Play("Work");
                if (Array.IndexOf(availableStates, "Aim") >= 0 && keyboard.pKey.wasPressedThisFrame) Play("Aim");
                if (keyboard.tabKey.wasPressedThisFrame) Controls = !Controls;
                if (keyboard.spaceKey.wasPressedThisFrame) { paused = !paused; if (Animator) Animator.speed = paused ? 0 : 1; }
            }
            var mouse = Mouse.current;
            if (mouse == null) return;
            var p = mouse.position.ReadValue();
            float scale = Mathf.Max(.7f, Screen.height / 1080f);
            if (Controls && (p.y < 120 * scale || p.y > Screen.height - (CrewReview ? 110 : 64) * scale)) return;
            float wheel = mouse.scroll.ReadValue().y;
            if (Active.orthographic) Active.orthographicSize = Mathf.Clamp(Active.orthographicSize * Mathf.Exp(-wheel * .0008f), 1.5f, 10);
            else Active.fieldOfView = Mathf.Clamp(Active.fieldOfView * Mathf.Exp(-wheel * .0007f), 18, 58);
            if (mouse.leftButton.isPressed && View != 2) Active.transform.RotateAround(new Vector3(0, 1.25f, 0), Vector3.up, mouse.delta.ReadValue().x * .15f);
        }

        private void OnGUI()
        {
            if (!Controls || capturing) return;
            float scale = Mathf.Max(.7f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            var heading = new GUIStyle(GUI.skin.label) { fontSize = 22, normal = { textColor = new Color(.94f, .83f, .63f) } };
            var detail = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = new Color(.81f, .83f, .85f) } };
            var entry = Roster != null && Roster.Length > 0 ? Roster[CharacterIndex] : null;
            GUI.Box(new Rect(0, 0, width, CrewReview ? 110 : 64), GUIContent.none);
            GUI.Label(new Rect(25, 9, width - 230, 30), entry == null ? "CORSARIO CARMESÍ" : entry.DisplayName.ToUpperInvariant(), heading);
            GUI.Label(new Rect(26, 38, width - 230, 24), (entry == null ? "Héroe pirata" : entry.Role) + " · " + currentState + (paused ? " · pausa" : ""), detail);
            GUI.enabled = !CrewReview || StartCrewMatch != null;
            if (GUI.Button(new Rect(width - 186, 12, 160, 40), "JUGAR")) PlayGame();
            GUI.enabled = true;
            if (CrewReview && Roster != null)
            {
                float buttonWidth = Mathf.Min(230, (width - 48) / Roster.Length);
                for (int i = 0; i < Roster.Length; i++)
                {
                    GUI.enabled = i != CharacterIndex;
                    if (GUI.Button(new Rect(width * .5f - buttonWidth * Roster.Length * .5f + i * buttonWidth, 70, buttonWidth - 8, 30), Roster[i].DisplayName)) SelectCharacter(i);
                }
                GUI.enabled = true;
            }
            GUI.Box(new Rect(0, height - 120, width, 120), GUIContent.none);
            string[] cameras = { "1 · Cercana", "2 · Media", "3 · RTS" };
            for (int i = 0; i < cameras.Length; i++) if (GUI.Button(new Rect(width * .5f - 242 + i * 165, height - 109, 155, 34), cameras[i])) SetView(i);
            var available = AvailableStates;
            for (int i = 0; i < available.Length; i++) if (GUI.Button(new Rect(width * .5f - available.Length * 145 * .5f + i * 145, height - 65, 135, 34), workAvailable && available[i] == "Attack" ? "A · Inspeccionar" : Labels[Array.IndexOf(AllStates, available[i])])) Play(available[i]);
            GUI.Label(new Rect(24, height - 24, width - 48, 24), "Arrastra para girar · Rueda para acercar · Espacio: pausa · Tab: ocultar controles", detail);
        }

        [Serializable] private sealed class AnimationEvidence
        {
            public string state;
            public bool entered, moving;
            public float sampleDisplacement;
        }
        [Serializable] private sealed class Evidence
        {
            public string buildGuid, unity, graphicsDevice;
            public int width, height, characters = 1;
            public bool passed;
            public string scope = "Native player renders and Animator states with sampled skinned vertex deformation. Not a frame-rate benchmark or gameplay balance certification.";
            public string[] images;
            public List<AnimationEvidence> animations = new List<AnimationEvidence>();
        }

        private IEnumerator Capture(string folder)
        {
            Evidence result = null;
            yield return CaptureCharacter(folder, value => result = value);
            Application.Quit(result != null && result.passed ? 0 : 1);
        }

        [Serializable] private sealed class RosterEvidence
        {
            public string buildGuid, unity, graphicsDevice;
            public bool passed;
            public int characters, animationStates;
            public List<CharacterEvidence> roster = new List<CharacterEvidence>();
        }
        [Serializable] private sealed class CharacterEvidence
        {
            public string id, displayName, folder;
            public Evidence review;
        }

        private IEnumerator CaptureRoster(string folder)
        {
            Directory.CreateDirectory(folder);
            var result = new RosterEvidence { buildGuid = Application.buildGUID, unity = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceName, passed = Roster != null && Roster.Length == 4 };
            if (Roster != null)
            {
                for (int i = 0; i < Roster.Length; i++)
                {
                    SelectCharacter(i); yield return null;
                    string characterFolder = Path.Combine(folder, Roster[i].Id);
                    Evidence report = null;
                    yield return CaptureCharacter(characterFolder, value => report = value);
                    result.roster.Add(new CharacterEvidence { id = Roster[i].Id, displayName = Roster[i].DisplayName, folder = Roster[i].Id, review = report });
                    result.characters++;
                    if (report != null) result.animationStates += report.animations.Count;
                    int expectedStates = Roster[i].Id == "treasure_seeker" || Roster[i].Id == "gunpowder_corsair" ? 7 : 6;
                    result.passed &= report != null && report.passed && report.animations.Count == expectedStates;
                }
            }
            File.WriteAllText(Path.Combine(folder, "crew-review.json"), JsonUtility.ToJson(result, true));
            Application.Quit(result.passed ? 0 : 1);
        }

        private IEnumerator CaptureCharacter(string folder, Action<Evidence> completed)
        {
            Directory.CreateDirectory(folder);
            yield return null;
            var evidence = new Evidence { buildGuid = Application.buildGUID, unity = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceName, width = Screen.width, height = Screen.height, passed = Character && Animator && Animator.runtimeAnimatorController };
            string[] names = { "close", "medium", "rts" };
            Play("Idle"); yield return new WaitForSeconds(.3f);
            for (int i = 0; i < names.Length; i++)
            {
                SetView(i); yield return new WaitForEndOfFrame();
                CaptureCamera(Active, Screen.width, Screen.height, Path.Combine(folder, names[i] + ".png"));
            }
            SetView(1);
            SkinnedMeshRenderer skin = null;
            foreach (var candidate in Character.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (candidate.name.IndexOf("LOD0", StringComparison.OrdinalIgnoreCase) >= 0) { skin = candidate; break; }
            var baked = new Mesh();
            foreach (string state in AvailableStates)
            {
                Animator.speed = 0;
                Animator.Play(state, 0, 0); Animator.Update(0);
                Vector3[] before = null;
                if (skin) { skin.BakeMesh(baked); before = baked.vertices; }
                float duration = driver ? driver.Duration(state) : 1;
                Animator.speed = 1;
                yield return new WaitForSeconds(duration * .45f);
                Animator.speed = 0;
                float movement = 0;
                if (skin && before != null)
                {
                    skin.BakeMesh(baked); var after = baked.vertices;
                    for (int i = 0; i < before.Length; i += Math.Max(1, before.Length / 1000)) movement = Mathf.Max(movement, Vector3.Distance(before[i], after[i]));
                }
                bool entered = Animator.GetCurrentAnimatorStateInfo(0).IsName(state);
                evidence.animations.Add(new AnimationEvidence { state = state, entered = entered, moving = movement > .0001f, sampleDisplacement = movement });
                evidence.passed &= entered && movement > .0001f;
                yield return new WaitForEndOfFrame();
                CaptureCamera(Active, Screen.width, Screen.height, Path.Combine(folder, "animation-" + state.ToLowerInvariant() + ".png"));
            }
            Destroy(baked);
            var images = new List<string> { "close.png", "medium.png", "rts.png" };
            foreach (string state in AvailableStates) images.Add("animation-" + state.ToLowerInvariant() + ".png");
            evidence.images = images.ToArray();
            File.WriteAllText(Path.Combine(folder, "review.json"), JsonUtility.ToJson(evidence, true));
            completed(evidence);
        }

        public static void CaptureCamera(Camera camera, int width, int height, string path)
        {
            RoyalSoldierReview.CaptureCamera(camera, width, height, path);
        }
    }
}
