using System;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    public sealed partial class ProductShell
    {
        private string realm = "historical", mapId = "amber_crossing";
        private OfflineAiDifficulty difficulty = OfflineAiDifficulty.Normal;
        private static string DifficultyBriefing(OfflineAiDifficulty value) =>
            value == OfflineAiDifficulty.Easy ? "Easy rival: a small economy that only attacks from the last Era." :
            value == OfflineAiDifficulty.Hard ? "Hard rival: a larger economy, quicker reactions and three halls; attacks from minute six." :
            "Normal rival: attacks with a formed army from minute seven, once it has advanced an Era.";
        private void Skirmish()
        {
            if (!ContentRealms.IsPlayableFactionInRealm(faction, realm)) faction = ContentRealms.FactionsForRealm(realm)[0];
            if (!ContentRealms.IsMapAllowedInRealm(mapId, realm)) mapId = ContentRealms.DefaultMapForRealm(realm);
            Place(Label("Skirmish title", content, "Elige tu facción", 39, AlphaTheme.Ink, true).rectTransform, new Vector2(0,.9f), new Vector2(.49f,1));
            Place(Label("Realm separation", content, "Históricas, fantasía y navales: tres PvP separados.", 17, AlphaTheme.Muted).rectTransform, new Vector2(0,.825f), new Vector2(1,.9f));
            for (int i = 0; i < ContentRealms.All.Length; i++) RealmButton(ContentRealms.All[i], .49f + i * .17f);
            string[] ids = FrontierCodex.VisibleFactions(realm);
            for (int i = 0; i < ids.Length; i++) FrontierFactionCard(ids[i], i, ids.Length);
            for (int i = 0; i < FrontierCodex.MapIds.Length; i++)
            {
                string id = FrontierCodex.MapIds[i]; float span = 1f / FrontierCodex.MapIds.Length, x = i * span;
                var map = Action(content, "Choose map " + id, FrontierCodex.BiomeName(id) + "\n" + FrontierCodex.MapName(id), () => { mapId = id; Navigate("skirmish"); }, mapId == id);
                map.interactable = ContentRealms.IsMapAllowedInRealm(id, realm);
                Place((RectTransform)map.transform, new Vector2(x,.195f), new Vector2(x+span-.02f,.31f));
            }
            Place(Label("Biome briefing", content, realm == "naval" ? FrontierCodex.Availability("pirates") : FrontierCodex.MapDescription(mapId), 16, AlphaTheme.Muted).rectTransform, new Vector2(0,.12f), new Vector2(1,.19f));
            var conquest = Action(content, "Choose Conquest", "CONQUEST / Destroy Hearths", () => { mode = VictoryMode.Conquest; Navigate("skirmish"); }, mode == VictoryMode.Conquest);
            Place((RectTransform)conquest.transform, new Vector2(0,.005f), new Vector2(.215f,.11f));
            var dominion = Action(content, "Choose Dominion", "DOMINION / Hold beacons", () => { mode = VictoryMode.Dominion; Navigate("skirmish"); }, mode == VictoryMode.Dominion);
            Place((RectTransform)dominion.transform, new Vector2(.225f,.005f), new Vector2(.44f,.11f));
            var rival = Action(content, "Choose difficulty", "RIVAL AI / " + difficulty.ToString().ToUpperInvariant(), () => { difficulty = (OfflineAiDifficulty)(((int)difficulty + 1) % 3); Navigate("skirmish"); });
            Place((RectTransform)rival.transform, new Vector2(.45f,.005f), new Vector2(.665f,.11f));
            var start = Action(content, "Begin skirmish", "BEGIN " + FrontierCodex.RealmName(realm) + " BATTLE  ›", () => { Close(); match.StartOfflineMatch(faction, mode, mapId, difficulty); }, true);
            Place((RectTransform)start.transform, new Vector2(.68f,.005f), new Vector2(1,.11f));
            string victory = mode == VictoryMode.Conquest ? "Equal starts. Build your army and destroy every completed rival Hearth." : "Hold two of three beacons for eight uninterrupted minutes.";
            Place(Label("Victory description", content, victory + "  ·  " + DifficultyBriefing(difficulty), 15, AlphaTheme.Ink).rectTransform, new Vector2(0,-.055f), new Vector2(1,0));
        }
        private void RealmButton(string id, float x)
        {
            var button = Action(content, "Choose realm " + id, FrontierCodex.RealmName(id), () => { if (realm != id || !ContentRealms.IsMapAllowedInRealm(mapId, id)) mapId = ContentRealms.DefaultMapForRealm(id); realm = id; faction = ContentRealms.FactionsForRealm(id)[0]; Navigate("skirmish"); }, realm == id);
            Place((RectTransform)button.transform, new Vector2(x,.86f), new Vector2(x+.165f,.985f));
        }
        // A battlefield still on the last faction's suggestion follows the new one: the desert pair to Sunscar Basin and
        // back. A battlefield the player picked stays.
        private void SelectFaction(string id)
        {
            if (mapId == FrontierCodex.HomeMap(faction)) mapId = FrontierCodex.HomeMap(id);
            faction = id;
        }
        private void FrontierFactionCard(string id, int index, int count)
        {
            bool playable = ContentRealms.IsPlayableFactionInRealm(id, realm);
            float span = 1f / Math.Max(1, count), x = index * span;
            var card = Panel("Faction " + id, content); Place(card, new Vector2(x,.35f), new Vector2(x+span-.02f,.805f));
            var stripe = Rect("Faction accent", card); Place(stripe, Vector2.zero, new Vector2(.016f,1)); stripe.gameObject.AddComponent<Image>().color = faction == id ? AlphaTheme.Gold : AlphaTheme.Teal;
            var title = Label("Faction name", card, FrontierCodex.Name(id), 24, AlphaTheme.Ink, true);
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 17; title.resizeTextMaxSize = 24;
            Place(title.rectTransform, new Vector2(.07f,.78f), new Vector2(.95f,.97f));
            Place(Label("Faction identity", card, FrontierCodex.Identity(id), 13, AlphaTheme.Gold).rectTransform, new Vector2(.07f,.635f), new Vector2(.95f,.79f));
            Place(Label("Faction description", card, FrontierCodex.Description(id), 17, AlphaTheme.Muted).rectTransform, new Vector2(.07f,.3f), new Vector2(.94f,.64f));
            var select = Action(card, "Select " + id, !playable ? "NO DISPONIBLE" : faction == id ? "SELECCIONADA" : "ELEGIR FACCIÓN", () => { if (!ContentRealms.IsPlayableFactionInRealm(id, realm)) return; SelectFaction(id); Navigate("skirmish"); }, faction == id);
            select.interactable = playable;
            Place((RectTransform)select.transform, new Vector2(.07f,.035f), new Vector2(.94f,.285f));
        }
    }
}
