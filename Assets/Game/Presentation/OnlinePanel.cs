using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    public sealed class OnlinePanel
    {
        private readonly MatchController match;
        private readonly Font font;
        private readonly RectTransform root, content;
        private readonly ScrollRect scroll;
        private readonly Text status, profile, history, faction, mode, realm, map;
        private readonly InputField address, username, password, room;
        private readonly GameObject loginGroup, homeGroup, lobbyGroup, queueGroup, matchGroup, plannedNaval;
        private readonly Button ready, surrender, confirm, leave, logout, profileRefresh;
        private bool confirming;
        private string displayedPhase;
        public RectTransform Root => root;
        public OnlinePanel(MatchController match, Transform parent, Font font)
        {
            this.match = match; this.font = AlphaTheme.Body;
            root = Rect("Online lobby", parent); Stretch(root, 0); AlphaPanelDecor.Apply(root);
            var title = Label("EMBERFIELD / ONLINE", root, 28); title.rectTransform.anchorMin = new Vector2(0, 1); title.rectTransform.anchorMax = Vector2.one;
            title.font = AlphaTheme.Display; title.color = AlphaTheme.Gold;
            title.rectTransform.offsetMin = new Vector2(24, -78); title.rectTransform.offsetMax = new Vector2(-230, -18);
            var close = Button("Return to map", root, match.Online.Close); var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = Vector2.one; closeRect.offsetMin = new Vector2(-220, -78); closeRect.offsetMax = new Vector2(-24, -18);
            var viewport = Rect("Online scroll viewport", root); Stretch(viewport, 24); viewport.offsetMax = new Vector2(-24, -94);
            viewport.gameObject.AddComponent<Image>().color = AlphaTheme.Background; viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.viewport = viewport;
            content = Rect("Online options", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            Layout(content); content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; scroll.content = content;
            status = TextRow("Sign in to play.", content, 100);
            status.font = AlphaTheme.Strong; status.color = AlphaTheme.Gold;
            loginGroup = Group("Sign in", content);
            address = Input("Server address", OnlineControls.Service.Address, loginGroup.transform, false);
            TextRow("127.0.0.1 connects only to this computer and needs a running local server.\nTo play from different computers, both players need the same HTTPS server address.", loginGroup.transform, 98);
            Button("Check server connection", loginGroup.transform, () => match.Online.CheckConnection(address.text));
            username = Input("Username (3-24 letters, numbers or underscore)", "", loginGroup.transform, false);
            password = Input("Password (at least 10 characters)", "", loginGroup.transform, true);
            var auth = Group("Account actions", loginGroup.transform);
            Button("Sign in", auth.transform, () => Authenticate(false)); Button("Create account", auth.transform, () => Authenticate(true));
            profile = TextRow("", content, 60);
            homeGroup = Group("Find a match", content);
            realm = Button("PvP realm", homeGroup.transform, match.Online.ChooseRealm).GetComponentInChildren<Text>();
            map = Button("Battlefield", homeGroup.transform, match.Online.ChooseMap).GetComponentInChildren<Text>();
            faction = Button("Faction", homeGroup.transform, match.Online.ChooseFaction).GetComponentInChildren<Text>();
            mode = Button("Mode", homeGroup.transform, match.Online.ChooseMode).GetComponentInChildren<Text>();
            plannedNaval = Group("Planned naval factions", homeGroup.transform);
            TextRow(FrontierCodex.NavalBriefing + "\n" + FrontierCodex.NavalCounters, plannedNaval.transform, 90);
            foreach (string id in FrontierCodex.PlannedNavalFactions)
            {
                var unavailable = Button(FrontierCodex.Name(id) + " / NO DISPONIBLE", plannedNaval.transform, () => { });
                unavailable.name = "Planned faction " + id; unavailable.interactable = false;
            }
            TextRow(FrontierCodex.Availability("skeleton_fleet"), plannedNaval.transform, 68);
            Button("Host private 1v1", homeGroup.transform, match.Online.Host);
            room = Input("Private room code", "", homeGroup.transform, false); room.characterLimit = 12;
            Button("Join private room", homeGroup.transform, () => match.Online.Join(room.text));
            Button("Find casual opponent", homeGroup.transform, () => match.Online.Queue(false));
            Button("Find ranked opponent", homeGroup.transform, () => match.Online.Queue(true));
            lobbyGroup = Group("Private room", content); ready = Button("Ready to start", lobbyGroup.transform, match.Online.Ready);
            Button("Leave room", lobbyGroup.transform, match.Online.Leave);
            queueGroup = Group("Matchmaking", content); Button("Cancel matchmaking", queueGroup.transform, match.Online.CancelQueue);
            matchGroup = Group("Match actions", content);
            surrender = Button("Surrender...", matchGroup.transform, () => confirming = true);
            confirm = Button("Confirm surrender", matchGroup.transform, () => { confirming = false; match.Online.Surrender(); });
            leave = Button("Return to lobby", matchGroup.transform, match.Online.Leave);
            profileRefresh = Button("Refresh profile and history", content, match.Online.Profile);
            history = TextRow("", content, 260);
            logout = Button("Sign out", content, match.Online.SignOut);
            TextRow("Históricas, fantasía y navales tienen oponentes, rangos e historial separados.\nOnline matches continue in menus. Reconnect promptly after a lost connection.\nPrivate matches do not change rank. Ranked results come from the server.", content, 112);
            root.gameObject.SetActive(false);
        }
        private void Authenticate(bool register)
        {
            string secret = password.text; password.text = "";
            match.Online.Authenticate(address.text, username.text, secret, register);
        }
        public void Refresh()
        {
            var online = match.Online;
            root.gameObject.SetActive(online.IsOpen); if (!online.IsOpen) return;
            root.SetAsLastSibling();
            bool signed = OnlineControls.Service.SignedIn;
            var state = online.State; string phase = state?.status ?? "idle";
            bool active = phase == "active" || phase == "starting";
            var text = new StringBuilder(512); text.Append(online.Busy ? "Working...\n" : "").Append(online.Status);
            if (signed && state != null)
            {
                text.Append("\n").Append(phase.ToUpperInvariant()).Append(" / ").Append(FrontierCodex.RealmName(state.realmId)).Append(" / ").Append(FrontierCodex.MapName(state.mapId));
                if (!string.IsNullOrEmpty(state.roomCode)) text.Append(" / Room ").Append(state.roomCode);
                if (!string.IsNullOrEmpty(state.queue)) text.Append(" / ").Append(state.queue).Append(" / ").Append(state.mode);
                if (state.players != null) foreach (var seat in state.players) text.Append("\n").Append(seat.username).Append(" / ").Append(FrontierCodex.Name(seat.factionId)).Append(seat.ready ? " / ready" : "").Append(active && !seat.connected ? " / disconnected" : "");
                if (phase == "queued") text.Append("\nWaiting ").Append(state.queueSeconds).Append("s. Search widens over time.");
                if (active && state.afkSecondsRemaining <= 60) text.Append("\nAFK: issue an order within ").Append(state.afkSecondsRemaining).Append("s to avoid forfeit.");
                if (state.Result != null) text.Append("\n").Append(phase == "aborted" ? "MATCH ABORTED" : state.Result.winnerPlayerId == 0 ? "MATCH DRAWN" : state.Result.winnerPlayerId == state.playerId ? "VICTORY" : "DEFEAT").Append(" / ").Append(state.Result.reason);
            }
            status.text = text.ToString(); status.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(100, 30 * (status.text.Split('\n').Length + 1));
            loginGroup.SetActive(!signed); homeGroup.SetActive(signed && phase == "idle" && !online.IsMatch); lobbyGroup.SetActive(signed && phase == "lobby");
            queueGroup.SetActive(signed && phase == "queued"); matchGroup.SetActive(signed && (online.IsMatch || active || phase == "finished" || phase == "aborted"));
            surrender.gameObject.SetActive(active && !confirming); confirm.gameObject.SetActive(active && confirming); leave.gameObject.SetActive(!active);
            ready.interactable = !online.Busy; logout.gameObject.SetActive(signed);
            profileRefresh.gameObject.SetActive(signed);
            logout.interactable = !active; // Avoid accidentally giving up the only in-memory session during a live match.
            realm.text = "PvP: " + FrontierCodex.RealmName(online.Realm);
            plannedNaval.SetActive(online.Realm == "naval");
            map.text = "Battlefield: " + (online.MapId == "legend_lands" ? "Lands of Legend / Homelands" : online.MapId == "amber_crossing" ? "Amber Crossing / Forest" : online.MapId == "sapphire_coast" ? "Sapphire Coast / Caribbean" : "Sunscar Basin / Desert");
            faction.text = "Faction: " + online.FactionName; mode.text = "Mode: " + online.Mode;
            profile.text = signed ? "Player: " + OnlineControls.Service.Profile?.username : "";
            if (signed && OnlineControls.Service.Profile?.ratings != null)
                foreach (var rating in OnlineControls.Service.Profile.ratings) if (rating.realmId == online.Realm) profile.text += "\n" + rating.queue + " / " + rating.visibleRank + " / " + rating.rankPoints + " points / " + rating.wins + " wins";
            profile.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(40, profile.text.Split('\n').Length * 28);
            var recent = new StringBuilder("PARTIDAS RECIENTES / " + FrontierCodex.RealmName(online.Realm));
            int shown = 0;
            foreach (var entry in online.History)
            {
                if (entry.realmId != online.Realm) continue;
                if (shown++ == 5) break;
                recent.Append("\n").Append(FrontierCodex.RealmName(entry.realmId)).Append(" / ").Append(entry.queue).Append(" / ").Append(entry.mode).Append(" / ").Append(entry.outcome).Append(" vs ").Append(entry.opponent);
                if (entry.statistics != null) recent.Append("\nEra ").Append(entry.statistics.eraTier).Append(" / Workers ").Append(entry.statistics.workersRemaining).Append(" / Army ").Append(entry.statistics.armyRemaining).Append(" / Buildings ").Append(entry.statistics.buildingsRemaining);
            }
            history.text = recent.ToString(); history.gameObject.SetActive(signed); history.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(70, recent.ToString().Split('\n').Length * 28);
            string page = signed + ":" + phase;
            if (page != displayedPhase) { displayedPhase = page; scroll.StopMovement(); scroll.verticalNormalizedPosition = 1; }
        }
        private GameObject Group(string name, Transform parent) { var rect = Rect(name, parent); Layout(rect); AlphaTheme.StylePanel(rect); return rect.gameObject; }
        private static void Layout(RectTransform rect)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(12, 12, 8, 8); layout.spacing = 10;
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        }
        private Text TextRow(string value, Transform parent, float height) { var label = Label(value, parent, 21); label.alignment = TextAnchor.UpperLeft; label.gameObject.AddComponent<LayoutElement>().preferredHeight = height; return label; }
        private Text Label(string value, Transform parent, int size)
        {
            var label = Rect("Text", parent).gameObject.AddComponent<Text>(); label.font = font; label.fontSize = size; label.text = value;
            label.color = AlphaTheme.Ink; label.supportRichText = false; label.raycastTarget = false; return label;
        }
        private Button Button(string value, Transform parent, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(value, parent); rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 58;
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.18f, .32f, .34f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            bool primary = value == "Sign in" || value == "Host private 1v1" || value == "Join private room" || value == "Ready to start" || value == "Find ranked opponent";
            button.onClick.AddListener(() => UiSound.Play(primary ? UiCue.Confirm : UiCue.Click));
            var label = Label(value, rect, 22); Stretch(label.rectTransform, 8); label.alignment = TextAnchor.MiddleCenter;
            AlphaTheme.StyleButton(button, primary); return button;
        }
        private InputField Input(string hint, string value, Transform parent, bool secret)
        {
            var rect = Rect(hint, parent); rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            AlphaTheme.StylePanel(rect, true); var image = rect.GetComponent<Image>();
            var field = rect.gameObject.AddComponent<InputField>(); field.targetGraphic = image; field.characterLimit = secret ? 128 : 200;
            var label = Label("", rect, 22); Stretch(label.rectTransform, 12); label.alignment = TextAnchor.MiddleLeft; field.textComponent = label;
            var placeholder = Label(hint, rect, 20); Stretch(placeholder.rectTransform, 12); placeholder.color = AlphaTheme.Muted; placeholder.alignment = TextAnchor.MiddleLeft; field.placeholder = placeholder;
            field.selectionColor = new Color(AlphaTheme.Teal.r, AlphaTheme.Teal.g, AlphaTheme.Teal.b, .45f);
            if (secret) field.contentType = InputField.ContentType.Password;
            field.text = value; return field;
        }
        private static RectTransform Rect(string name, Transform parent) { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect; }
        private static void Stretch(RectTransform rect, float margin) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(margin, margin); rect.offsetMax = new Vector2(-margin, -margin); }
    }
}
