using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    /// <summary>
    /// FrontierAmbience's wind bed, on a map whose lands wear different cultures (MapLands): one bed a land, its
    /// volume the land's share of the view around the camera, eased rather than cut when the camera pans across a
    /// border, exactly as LandAtmosphere grades the same lands. Every other map keeps the single bed it always had.
    /// </summary>
    public sealed class FrontierAmbienceLandTests
    {
        private GameObject root;

        [UnityTearDown] public IEnumerator TearDown() { if (root != null) Object.Destroy(root); yield return null; }

        private static Dictionary<string, AudioSource> Beds(FrontierAmbience ambience) =>
            (Dictionary<string, AudioSource>)typeof(FrontierAmbience).GetField("beds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ambience);

        private static void RefreshBeds(FrontierAmbience ambience, float seconds) =>
            typeof(FrontierAmbience).GetMethod("RefreshBeds", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ambience, new object[] { seconds });

        [UnityTest]
        public IEnumerator TheWindBedFollowsTheLandUnderTheCamera()
        {
            root = new GameObject("Ambience follows the land"); root.SetActive(false);
            var match = root.AddComponent<MatchController>();
            match.InitialOfflineFactionId = "ashen"; match.InitialOfflineMode = VictoryMode.Dominion;
            root.SetActive(true); match.enabled = false;
            yield return null;
            Assert.AreEqual("legend_lands", match.World.Map.Id);
            var ambience = root.GetComponent<FrontierAmbience>();
            Assert.IsNotNull(ambience, "A legend_lands match scores its lands.");
            var beds = Beds(ambience);
            Assert.IsNotNull(beds, "More than one land is in play (ashen against its opponent), so it builds a bed a land.");
            Assert.That(beds.Keys, Is.EquivalentTo(match.World.Lands.Biomes), "One bed for every land the map shows, the map's own first.");
            Assert.IsTrue(beds.ContainsKey(MapLands.Highland), "The map's own highland keeps the wind bed it always had.");

            var volcanicHearth = Vector3.zero;
            foreach (var building in match.World.Buildings)
                if (building.DefinitionId == "hearth" && match.World.Lands.BiomeAt(building.Position) == MapLands.Volcanic)
                { volcanicHearth = DefinitionLoader.ToWorld(building.Position); break; }
            Assert.AreNotEqual(Vector3.zero, volcanicHearth, "The pairing must field a volcanic start to test.");

            // Looking at the volcanic settlement, its own bed leads over the highland's.
            match.Rig.SetHome(volcanicHearth, 8);
            RefreshBeds(ambience, 30); // a long step snaps the weights, the same trick LegendLandsLookTests uses on LandAtmosphere
            float atVolcano = beds[MapLands.Volcanic].volume;
            Assert.That(atVolcano, Is.GreaterThan(beds[MapLands.Highland].volume), "Over the volcanic land its own rumble leads.");
            Assert.That(atVolcano, Is.GreaterThan(0), "The volcanic bed must actually be audible there.");

            // Panning to the neutral highland centre eases the volcanic bed down and the highland's up, not a cut.
            match.Rig.SetHome(new Vector3(72, 0, 56), 5);
            RefreshBeds(ambience, .2f);
            Assert.That(beds[MapLands.Volcanic].volume, Is.LessThan(atVolcano), "The step must actually ease the volcanic bed down.");
            Assert.That(beds[MapLands.Volcanic].volume, Is.GreaterThan(0), "A pan crossfades; it does not cut.");
            for (int i = 0; i < 30; i++) RefreshBeds(ambience, .1f);
            Assert.That(beds[MapLands.Volcanic].volume, Is.LessThan(.01f), "Settled on the highland, the volcanic rumble is gone.");
            Assert.That(beds[MapLands.Highland].volume, Is.GreaterThan(.1f), "Settled on the highland, its own wind carries the bed.");

            // Every bed keeps answering the mute setting, exactly as the single bed always did.
            Assert.IsFalse(beds[MapLands.Highland].mute);
            match.Alpha.Settings.Value.SoundEnabled = false;
            typeof(FrontierAmbience).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ambience, null);
            foreach (var source in beds.Values) Assert.IsTrue(source.mute, "Muting the match must mute every land's bed.");
        }

        [UnityTest]
        public IEnumerator AMapWithOneLandKeepsItsSingleBed()
        {
            root = new GameObject("Ambience single bed"); root.SetActive(false);
            var match = root.AddComponent<MatchController>();
            match.InitialOfflineFactionId = "aven"; match.InitialOfflineMode = VictoryMode.Conquest;
            root.SetActive(true); match.enabled = false;
            yield return null;
            var ambience = root.GetComponent<FrontierAmbience>();
            Assert.IsNotNull(ambience);
            Assert.IsNull(Beds(ambience), "A map without more than one land in view keeps the single bed it always had.");
        }
    }
}
