using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    /// <summary>
    /// The animation driver chooses each rig's gait from the rig's own clip speeds, and in the match's own frame leaves
    /// the evaluation to Unity's animator update, which must pose the rig as the immediate evaluation did.
    /// </summary>
    public sealed class AnimationDriverTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private long tick = 1;

        [TearDown]
        public void TearDown()
        {
            CorsairAnimationDriver.EndEngineFrame();
            foreach (var go in created) if (go) Object.Destroy(go);
            created.Clear();
        }

        private static GameDefinition Rules() => JsonUtility.FromJson<GameDefinition>(Resources.Load<TextAsset>("Definitions/greybox").text);

        /// <summary>A world with one reedguard marching east at the given pace, a tick into its move.</summary>
        private static UnitState Mover(int millimetresPerSecond, out World world)
        {
            var rules = Rules();
            rules.Units.Single(u => u.Id == "reedguard").MoveSpeedMillimetresPerSecond = millimetresPerSecond;
            world = new World(rules, new MapDefinition
            {
                Id = "animation_driver", RealmId = ContentRealms.Naval, WidthCells = 64, HeightCells = 24,
                PlayerFactions = new[] {
                    new PlayerFactionDefinition { PlayerId = 1, FactionId = "pirates" },
                    new PlayerFactionDefinition { PlayerId = 2, FactionId = "pirates" }
                },
                UnitSpawns = new[] { new UnitSpawnDefinition { Id = 1, OwnerId = 1, DefinitionId = "reedguard", Position = new SimPoint(4500, 10500) } }
            });
            Assert.That(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(60500, 10500))).Accepted, Is.True);
            world.Tick();
            Assert.That(world.TryGetUnit(1, out var unit), Is.True);
            return unit;
        }

        private CorsairAnimationDriver Rig(string prefab, Vector3 position)
        {
            var instance = Object.Instantiate(Resources.Load<GameObject>(prefab), position, Quaternion.identity);
            created.Add(instance);
            var driver = instance.GetComponent<CorsairAnimationDriver>();
            Assert.That(driver != null && driver.HasValidRig, Is.True, prefab + " rig");
            return driver;
        }

        private void March(CorsairAnimationDriver driver, UnitState unit, World world, int ticks)
        {
            for (int i = 0; i < ticks; i++) { world.Tick(); driver.Sync(unit, tick++, 0); }
        }

        [Test]
        public void EveryRigWalksPlantedUpToItsSwitchAndMountsGallopAtTheirOwnPace()
        {
            var rules = Rules();
            foreach (var entry in MeshyUnitVisuals.Entries)
            {
                var driver = Rig(entry.prefab, Vector3.zero);
                float walk = driver.WalkMetresPerSecond, run = driver.RunMetresPerSecond, runAbove = driver.RunAboveMetresPerSecond;
                Assert.That(runAbove / walk, Is.LessThanOrEqualTo(CorsairAnimationDriver.FastestWalkPlayback + 1e-4f), entry.id + ": the walk skates before the switch");
                Assert.That(driver.WalkBelowMetresPerSecond / run, Is.GreaterThanOrEqualTo(CorsairAnimationDriver.SlowestPlayback - 1e-4f), entry.id + ": the run skates before it walks again");
                Assert.That(driver.WalkBelowMetresPerSecond, Is.LessThan(runAbove), entry.id + " keeps a band between its gaits");
                // A biped's clips already met at a natural pace: its switch is where it always was.
                if (run / walk < 2.7f) Assert.That(runAbove, Is.EqualTo(Mathf.Sqrt(walk * run)).Within(1e-4f), entry.id + " biped switch");
                var unit = rules.Units.Single(u => u.Id == entry.unit);
                if ((unit.Tags & CombatTags.Cavalry) != 0)
                    Assert.That(unit.MoveSpeedMillimetresPerSecond * .001f, Is.GreaterThan(runAbove), entry.id + " gallops at its own pace");
                Object.Destroy(driver.gameObject);
            }
        }

        [Test]
        public void AMountWalksWhenSlowGallopsWhenFastAndDoesNotFlickerAtTheSwitch()
        {
            var knight = Rig("MeshyUnits/kingdom_knight", Vector3.zero);
            float runAbove = knight.RunAboveMetresPerSecond;
            Assert.That(runAbove, Is.GreaterThan(1.1f), "The switch sits above the mount's crawl.");
            var slow = Mover(1000, out var slowWorld);
            var fast = Mover(5000, out var fastWorld);
            var near = Mover(Mathf.RoundToInt(runAbove * 950), out var nearWorld);
            March(knight, slow, slowWorld, 4);
            Assert.That(knight.CurrentState, Is.EqualTo("Walk"), "1 m/s is a walk.");
            March(knight, near, nearWorld, 4);
            Assert.That(knight.CurrentState, Is.EqualTo("Walk"), "Just under the switch it keeps walking.");
            March(knight, fast, fastWorld, 4);
            Assert.That(knight.CurrentState, Is.EqualTo("Run"), "At its own 5 m/s it gallops.");
            March(knight, near, nearWorld, 4);
            Assert.That(knight.CurrentState, Is.EqualTo("Run"), "Slowing just under the switch it keeps galloping.");
            March(knight, slow, slowWorld, 4);
            Assert.That(knight.CurrentState, Is.EqualTo("Walk"));
        }

        [UnityTest]
        public IEnumerator TheEngineFramePosesTheRigAsTheImmediateSyncDid()
        {
            var deferred = Rig("MeshyUnits/kingdom_knight", Vector3.zero);
            var immediate = Rig("MeshyUnits/kingdom_knight", Vector3.right * 4);
            yield return null;
            var unit = Mover(5000, out var world);
            for (int frame = 0; frame < 12; frame++)
            {
                world.Tick();
                CorsairAnimationDriver.BeginEngineFrame(null);
                deferred.Sync(unit, tick, .5f);
                CorsairAnimationDriver.EndEngineFrame();
                immediate.Sync(unit, tick, .5f);
                tick++;
                yield return null;
            }
            Assert.That(deferred.CurrentState, Is.EqualTo("Run"));
            AssertSamePose(deferred, immediate);
        }

        [UnityTest]
        public IEnumerator ARigOffTheScreenWaitsAndCatchesUpWhenSeen()
        {
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); created.Add(camera.gameObject);
            camera.transform.SetPositionAndRotation(new Vector3(0, 10, -20), Quaternion.Euler(30, 180, 0));
            var hidden = Rig("MeshyUnits/kingdom_knight", Vector3.zero);
            var reference = Rig("MeshyUnits/kingdom_knight", Vector3.right * 4);
            yield return null;
            var unit = Mover(5000, out var world);
            for (int frame = 0; frame < 8; frame++)
            {
                world.Tick();
                CorsairAnimationDriver.BeginEngineFrame(camera);
                hidden.Sync(unit, tick, 0);
                CorsairAnimationDriver.EndEngineFrame();
                reference.Sync(unit, tick, 0);
                tick++;
                yield return null;
            }
            Assert.That(hidden.Animator.cullingMode, Is.EqualTo(AnimatorCullingMode.CullCompletely), "Behind the camera it is not animated.");
            camera.transform.rotation = Quaternion.Euler(30, 0, 0);
            world.Tick();
            CorsairAnimationDriver.BeginEngineFrame(camera);
            hidden.Sync(unit, tick, 0);
            CorsairAnimationDriver.EndEngineFrame();
            reference.Sync(unit, tick, 0);
            yield return null;
            Assert.That(hidden.Animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));
            // Back in view it has played the time it missed: same state, same phase as the rig that never left.
            var a = hidden.Animator.GetCurrentAnimatorStateInfo(0);
            var b = reference.Animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(a.fullPathHash, Is.EqualTo(b.fullPathHash));
            Assert.That(a.normalizedTime, Is.EqualTo(b.normalizedTime).Within(.01f));
        }

        private static void AssertSamePose(CorsairAnimationDriver a, CorsairAnimationDriver b)
        {
            var first = a.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            var second = b.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            Assert.That(first.Length, Is.EqualTo(second.Length));
            float worst = 0;
            for (int i = 0; i < first.Length; i++) worst = Mathf.Max(worst, Quaternion.Angle(first[i].localRotation, second[i].localRotation));
            Assert.That(worst, Is.LessThan(.05f), "Largest bone rotation difference in degrees.");
            var x = a.Animator.GetCurrentAnimatorStateInfo(0);
            var y = b.Animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(x.fullPathHash, Is.EqualTo(y.fullPathHash));
            Assert.That(x.normalizedTime, Is.EqualTo(y.normalizedTime).Within(1e-3f));
        }
    }
}
