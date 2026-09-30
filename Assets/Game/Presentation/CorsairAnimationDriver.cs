using System;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>Visual-only skeletal animation. Simulation owns position, damage, timings and death.</summary>
    [DisallowMultipleComponent]
    public sealed class CorsairAnimationDriver : MonoBehaviour
    {
        // Nominal ground speed at 1x, measured where available or authored in the rig manifest,
        // and written onto the prefab. Playback scales with the simulated speed, so the
        // planted foot keeps pace with the ground instead of skating.
        [Min(.1f)] public float WalkMetresPerSecond = 1.8f;
        [Min(.1f)] public float RunMetresPerSecond = 3.6f;
        [Min(0)] public float AttackBlendSeconds = .10f;
        // How far a gait's playback may stray from its authored pace. Inside these the planted foot keeps pace with the
        // ground; past them it skates. A walk may be hurried further than a run: the mounts' donor walks are authored at
        // about a fifth of their gallop, so their walk only reaches a mount's slow pace when played up to 2.5 times.
        public const float SlowestPlayback = .35f, FastestWalkPlayback = 2.5f, FastestRunPlayback = 2f;
        // A run played slower than this reads as slow motion, so a unit keeps walking up to it while its walk can.
        public const float SlowestNaturalRun = .6f;
        // Once running, a unit slows this far below the switch before it walks again, so a unit moving at about the
        // switch speed does not flicker between its gaits from one tick to the next.
        public const float GaitHysteresis = .85f;
        // An animator off the screen stops, and catches up at most this much time when it comes back into view.
        private const float MostUnseenSeconds = 2;
        // Margin around a unit's position for the screen test: covers the tallest model and a shadow cast just inside.
        private static readonly Vector3 ScreenTestSize = new Vector3(8, 8, 8);
        private static readonly string[] States = { "Idle", "Walk", "Run", "Attack", "Hit", "Death", "Work", "Aim" };
        private const int Idle = 0, Walk = 1, Run = 2, Attack = 3, Hit = 4, Death = 5, Work = 6, Aim = 7;
        private readonly float[] durations = new float[8];
        private readonly int[] hashes = new int[8];
        private readonly bool[] resolved = new bool[8];
        private Animator animator;
        private RuntimeAnimatorController validController;
        private bool initialized, observed, dying, manual, validRig, culled;
        private int lastCooldown, lastHealth, current;
        private long lastTick;
        private double lastTime;
        private float stateTime, playbackRate = 1;
        // Time this frame's Sync left for the engine's animator update, and time missed while off the screen.
        private float deferred, unseen;
        private int deferredFrame = -1;
        private World corpseWorld;
        private Renderer[] corpseRenderers;
        private Transform muzzle;
        private static bool engineFrame, screenKnown;
        private static readonly Plane[] screen = new Plane[6];
        public string CurrentState => States[current];
        public bool IsDying => dying;
        public Animator Animator { get { Initialize(); return animator; } }
        public Vector3 MuzzleWorldPosition
        {
            get
            {
                if (muzzle == null)
                    foreach (var child in GetComponentsInChildren<Transform>(true))
                        if (child.name == "Muzzle") { muzzle = child; break; }
                return muzzle != null ? muzzle.position : transform.TransformPoint(new Vector3(.30f, 1.45f, .70f));
            }
        }
        /// <summary>
        /// Checked on every unit every frame, so a valid answer is kept until the controller changes: reading
        /// <c>SkinnedMeshRenderer.bones</c> builds a new array each time.
        /// </summary>
        public bool HasValidRig
        {
            get
            {
                Initialize();
                if (validRig && animator != null && animator.runtimeAnimatorController == validController) return true;
                var renderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
                validRig = animator != null && animator.runtimeAnimatorController != null && animator.avatar != null && animator.avatar.isValid &&
                    renderer != null && renderer.sharedMesh != null && renderer.bones.Length > 0;
                validController = validRig ? animator.runtimeAnimatorController : null;
                return validRig;
            }
        }

        /// <summary>
        /// The speed above which this rig runs rather than walks, from its own clips. Where the two clips play equally
        /// far from their authored pace (the geometric mean of the two speeds), moved up while the run would be slower
        /// than <see cref="SlowestNaturalRun"/> and the walk can still keep its feet planted. A biped's clips put that at
        /// about 45% of its marching speed; a mount's slow donor walk puts it at about 25-30% of its gallop.
        /// </summary>
        public float RunAboveMetresPerSecond =>
            Mathf.Min(Mathf.Max(Mathf.Sqrt(WalkMetresPerSecond * RunMetresPerSecond), RunMetresPerSecond * SlowestNaturalRun),
                WalkMetresPerSecond * FastestWalkPlayback);

        /// <summary>The speed below which a running rig walks again: a little under the switch, never where the run skates.</summary>
        public float WalkBelowMetresPerSecond =>
            Mathf.Max(RunAboveMetresPerSecond * GaitHysteresis, RunMetresPerSecond * SlowestPlayback);

        /// <summary>
        /// The match's own frame: between these calls, each Sync leaves its advance to Unity's animator update, which
        /// runs after every Update and before LateUpdate and evaluates all the animators of the frame together on the
        /// worker threads, instead of evaluating each rig on the main thread as it syncs (9 ms of a 500-unit frame).
        /// Only a caller whose frame reaches that update before anything is drawn or read may open one; every other
        /// caller (the probes, films and tests that render or read bones straight after a sync) keeps the immediate
        /// evaluation. Units off <paramref name="camera"/>'s view are not animated until they come back into it.
        /// </summary>
        public static void BeginEngineFrame(Camera camera)
        {
            engineFrame = true;
            screenKnown = camera != null;
            if (screenKnown) GeometryUtility.CalculateFrustumPlanes(camera, screen);
        }

        public static void EndEngineFrame() => engineFrame = false;

        private void Awake() => Initialize();
        private void Initialize()
        {
            if (initialized) return;
            animator = GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController == null) return;
            initialized = true;
            animator.applyRootMotion = false;
            // WorldView already culls invisible entities. Manual evaluation must also work
            // during hidden-window native reviews where renderer visibility is unreliable.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            for (int i = 0; i < States.Length; i++)
            {
                durations[i] = 1;
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    if (clip != null && (clip.name == States[i] || clip.name == "Corsair_" + States[i]))
                    { durations[i] = Mathf.Max(.05f, clip.length); break; }
            }
        }

        public void ResetObservation(UnitState unit, long tick, float interpolation)
        {
            if (unit == null) return;
            lastCooldown = unit.AttackCooldownTicks;
            lastHealth = unit.Health;
            lastTick = tick;
            lastTime = (tick + Mathf.Clamp01(interpolation)) * (double)World.TickSeconds;
            observed = true;
        }

        public void Sync(UnitState unit, long tick, float interpolation)
        {
            if (dying || unit == null || !HasValidRig) return;
            double now = (tick + Mathf.Clamp01(interpolation)) * (double)World.TickSeconds;
            if (!observed)
            {
                ResetObservation(unit, tick, interpolation);
                manual = true;
                animator.speed = 0;
                Switch(Idle, true);
            }
            float delta = (float)Math.Max(0, Math.Min(.25, now - lastTime));
            lastTime = Math.Max(lastTime, now);
            bool attack = tick != lastTick && unit.AttackCooldownTicks > lastCooldown;
            bool hit = tick != lastTick && unit.Health < lastHealth;
            lastTick = tick;
            lastCooldown = unit.AttackCooldownTicks;
            lastHealth = unit.Health;

            var displacement = DefinitionLoader.ToWorld(unit.Position) - DefinitionLoader.ToWorld(unit.PreviousPosition);
            float speed = displacement.magnitude / World.TickSeconds;
            // The gait follows this rig's own clip speeds; a running unit keeps running a little below the switch.
            float runAbove = current == Run ? WalkBelowMetresPerSecond : RunAboveMetresPerSecond;
            int locomotion = speed > runAbove ? Run : speed > .02f || unit.BoardingWallId != 0 ? Walk : Idle;
            if (locomotion == Idle && (unit.WorkerTask == WorkerTask.Gathering || unit.WorkerTask == WorkerTask.Constructing) && HasState(Work))
                locomotion = Work;
            // A ranged unit holds its aim between shots instead of lowering the weapon.
            else if (locomotion == Idle && unit.AttackTargetId != 0 && HasState(Aim))
                locomotion = Aim;
            stateTime += delta * playbackRate;
            if (attack) Switch(Attack, true);
            else if (hit && current != Attack) Switch(Hit, true);
            else if (current != Attack && current != Hit || stateTime >= durations[current])
                Switch(locomotion, false);

            playbackRate = current == Run ? Mathf.Clamp(speed / RunMetresPerSecond, SlowestPlayback, FastestRunPlayback) :
                current == Walk ? Mathf.Clamp(speed / WalkMetresPerSecond, SlowestPlayback, FastestWalkPlayback) : 1;
            // Advance only by simulated time. Pausing the match freezes locomotion and
            // attacks, and repeated presentation updates cannot advance gameplay.
            manual = true;
            if (engineFrame && Time.deltaTime > 0)
            {
                float advance = delta * playbackRate;
                if (deferredFrame != Time.frameCount) { deferredFrame = Time.frameCount; deferred = 0; }
                if (OffScreen())
                {
                    // Nobody sees it: the engine skips it, and what it missed is played when it comes back into view.
                    unseen = Mathf.Min(unseen + deferred + advance, MostUnseenSeconds);
                    deferred = 0;
                    animator.speed = 0;
                    Cull(true);
                    return;
                }
                Cull(false);
                deferred += unseen + advance;
                unseen = 0;
                // The engine advances every animator by Time.deltaTime times its speed later in this frame.
                animator.speed = deferred / Time.deltaTime;
                return;
            }
            ApplyPendingAdvance();
            animator.speed = playbackRate;
            animator.Update(delta);
            animator.speed = 0;
        }

        /// <summary>
        /// Evaluates now what this frame's Sync left for the engine (and what the unit missed off the screen), so a
        /// caller that poses the rig itself afterwards (the siege ladder's climb) starts from the pose it had before.
        /// </summary>
        public void ApplyPendingAdvance()
        {
            if (animator == null) return;
            float pending = (deferredFrame == Time.frameCount ? deferred : 0) + unseen;
            deferred = unseen = 0;
            deferredFrame = -1;
            Cull(false);
            if (pending <= 0) { if (manual) animator.speed = 0; return; }
            animator.speed = 1;
            animator.Update(pending);
            animator.speed = manual ? 0 : 1;
        }

        private bool OffScreen() => screenKnown &&
            !GeometryUtility.TestPlanesAABB(screen, new Bounds(transform.position, ScreenTestSize));

        private void Cull(bool off)
        {
            if (culled == off) return;
            culled = off;
            // Unity skips a culled animator only while its renderers are also unseen, so a camera test that errs
            // (a hidden window) never freezes a unit in view: at worst it is evaluated standing still.
            animator.cullingMode = off ? AnimatorCullingMode.CullCompletely : AnimatorCullingMode.AlwaysAnimate;
        }

        public bool PlayPreview(string state)
        {
            int index = Array.IndexOf(States, state);
            if (dying || index < 0 || !HasValidRig) return false;
            ApplyPendingAdvance();
            observed = false;
            manual = false;
            animator.speed = 1;
            return Switch(index, true);
        }

        public void BeginDeath(World world)
        {
            if (dying || !HasValidRig) return;
            ApplyPendingAdvance();
            dying = true;
            manual = false;
            corpseWorld = world;
            corpseRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            animator.speed = 1;
            Switch(Death, true);
            UpdateCorpseVisibility();
            Destroy(gameObject, Duration("Death") + 1.5f);
        }

        private bool Switch(int state, bool restart)
        {
            if (!restart && current == state) return true;
            int hash = StateHash(state);
            if (hash == 0) return false;
            // A second sync in one frame: play what the first left for the engine before the cross-fade starts.
            if (manual && deferredFrame == Time.frameCount && deferred > 0) ApplyPendingAdvance();
            int from = current;
            current = state;
            stateTime = 0;
            // Settling into or out of a held stance (aim, crouched work) reads as a
            // movement, not a cut; gait changes and reactions stay quick.
            float blend = state == Attack ? AttackBlendSeconds :
                state == Aim || state == Work || from == Aim || from == Work ? .22f : .10f;
            if (blend <= 0) animator.Play(hash, 0, 0);
            else animator.CrossFadeInFixedTime(hash, blend, 0, 0);
            if (manual) animator.Update(0);
            return true;
        }

        /// <summary>The state's hash in the controller (bare or under "Base Layer."), 0 when it has none.</summary>
        private int StateHash(int index)
        {
            if (resolved[index]) return hashes[index];
            string state = States[index];
            int hash = UnityEngine.Animator.StringToHash(state);
            if (!animator.HasState(0, hash))
            {
                hash = UnityEngine.Animator.StringToHash("Base Layer." + state);
                if (!animator.HasState(0, hash)) hash = 0;
            }
            // An animator not yet initialised (an inactive instance) answers no for every state: ask again later.
            if (animator.isInitialized) { hashes[index] = hash; resolved[index] = true; }
            return hash;
        }

        private bool HasState(int index) => StateHash(index) != 0;

        public bool HasState(string state)
        {
            Initialize();
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            int index = Array.IndexOf(States, state);
            if (index >= 0) return HasState(index);
            return animator.HasState(0, UnityEngine.Animator.StringToHash(state)) ||
                animator.HasState(0, UnityEngine.Animator.StringToHash("Base Layer." + state));
        }

        public float Duration(string state)
        {
            Initialize();
            int index = Array.IndexOf(States, state);
            return index < 0 ? 1 : Mathf.Max(.05f, durations[index]);
        }

        private void LateUpdate()
        {
            if (dying) { UpdateCorpseVisibility(); return; }
            // Unity's animator update has played this frame's advance by now; hold the pose until the next Sync.
            if (deferredFrame < 0) return;
            if (deferredFrame == Time.frameCount && animator != null && manual) animator.speed = 0;
            deferred = 0;
            deferredFrame = -1;
        }

        private void UpdateCorpseVisibility()
        {
            bool visible = corpseWorld?.Vision == null || corpseWorld.Vision.IsVisible(MatchController.LocalPlayer,
                DefinitionLoader.ToSimulation(transform.position));
            foreach (var renderer in corpseRenderers) if (renderer != null) renderer.enabled = visible;
        }
    }
}
