using System;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    public static class DefinitionLoader
    {
        public static World CreateWorld(string mapResourcePath = "Maps/amber_reach")
        {
            var rules = Load<GameDefinition>("Definitions/greybox");
            var map = Load<MapDefinition>(mapResourcePath);
            return new World(rules, map);
        }

        public static World CreateFactionWorld(string factionId)
        {
            if (ContentRealms.RealmForFaction(factionId) == "naval")
                return CreateOfflineWorld(factionId, VictoryMode.Conquest, ContentRealms.DefaultMapForRealm("naval"));
            var rules = Load<GameDefinition>("Definitions/greybox");
            var map = Load<MapDefinition>("Maps/faction_proving_ground");
            FactionDefinition local = null, opponent = null;
            foreach (var faction in rules.Factions)
            {
                if (faction.Id == factionId) local = faction;
                if (faction.Id == ContentRealms.OpponentFaction(factionId)) opponent = faction;
            }
            if (local == null || opponent == null) throw new ArgumentException("Choose a configured faction.", nameof(factionId));
            map.RealmId = ContentRealms.RealmForFaction(factionId);
            map.PlayerFactions = new[] {
                new PlayerFactionDefinition { PlayerId = 1, FactionId = local.Id },
                new PlayerFactionDefinition { PlayerId = 2, FactionId = opponent.Id }
            };
            ContentRealms.PrepareStartingUnits(map);
            return new World(rules, map);
        }

        public static World CreateOfflineWorld(string factionId, VictoryMode mode, string mapId = "amber_crossing")
        {
            var rules = Load<GameDefinition>("Definitions/greybox");
            if (!ContentRealms.IsMapAllowedInRealm(mapId, ContentRealms.RealmForFaction(factionId)))
                throw new ArgumentException("Choose an available battlefield.", nameof(mapId));
            var map = Load<MapDefinition>("Maps/" + ContentRealms.MapResourceId(mapId, ContentRealms.RealmForFaction(factionId)));
            string opponent = ContentRealms.OpponentFaction(factionId);
            if (opponent == null) throw new ArgumentException("Choose a configured faction.", nameof(factionId));
            map.RealmId = ContentRealms.RealmForFaction(factionId);
            map.PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = factionId }, new PlayerFactionDefinition { PlayerId = 2, FactionId = opponent } };
            ContentRealms.PrepareStartingUnits(map);
            map.OfflineMatch.Mode = mode;
            return new World(rules, map);
        }

        private static T Load<T>(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) throw new InvalidOperationException("Missing definition asset: " + path);
            var result = JsonUtility.FromJson<T>(asset.text);
            if (result == null) throw new InvalidOperationException("Invalid definition asset: " + path);
            return result;
        }

        public static Vector3 ToWorld(SimPoint p) => new Vector3(p.X * .001f, 0, p.Z * .001f);
        public static SimPoint ToSimulation(Vector3 p) => new SimPoint(Mathf.RoundToInt(p.x * 1000), Mathf.RoundToInt(p.z * 1000));
    }
}
