using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    public sealed partial class ProductShell
    {
        private string cosmeticSelection = "drake_frost", cosmeticFilter = "all", storeNotice = "Free alpha previews are for offline play. Listed prices are provisional.";
        private int catalogPage;
        private bool storeBusy;
        private bool disposed;
        private int storeSessionRevision = -1;
        private OnlineCosmeticsReply cosmeticsReply;
        private bool CurrentStoreSession(int revision) => !disposed && root != null && OnlineControls.Service.SignedIn && OnlineControls.Service.SessionRevision == revision;
        private void ObserveStoreSession()
        {
            int revision = OnlineControls.Service.SessionRevision;
            if (revision == storeSessionRevision) return;
            storeSessionRevision = revision; cosmeticsReply = null;
            if (IsOpen && page == "store") Navigate("store");
        }
        private void Store()
        {
            Place(Label("Store title", content, "The Quartermaster", 42, AlphaTheme.Ink, true).rectTransform, new Vector2(0,.89f), new Vector2(.7f,1));
            Place(Label("Store subtitle", content, "Make your army your own. Every item changes appearance only.", 17, AlphaTheme.Muted).rectTransform, new Vector2(0,.81f), new Vector2(1,.89f));
            StoreFilter("all", "TODAS", 0); StoreFilter("historical", FrontierCodex.RealmName("historical"), .155f); StoreFilter("fantasy", FrontierCodex.RealmName("fantasy"), .31f); StoreFilter("naval", FrontierCodex.RealmName("naval"), .465f);
            var available = new List<CosmeticCatalogItem>();
            foreach (var item in CosmeticLoadout.Catalog) if (cosmeticFilter == "all" || item.realmId == cosmeticFilter || item.realmId == "shared") available.Add(item);
            if (available.Count > 0 && !available.Exists(item => item.id == cosmeticSelection)) cosmeticSelection = available[0].id;
            int pages = Math.Max(1, (available.Count + 5) / 6); catalogPage = Math.Min(catalogPage, pages - 1);
            for (int slot = 0; slot < 6 && catalogPage * 6 + slot < available.Count; slot++)
            {
                var item = available[catalogPage * 6 + slot]; float x = slot % 2 * .31f, y = .493f - slot / 2 * .181f;
                var button = Action(content, "Inspect " + item.id, item.displayName + "\n" + (item.slot == "architecture" ? "Architecture collection" : "Army appearance"), () => { cosmeticSelection = item.id; Navigate("store"); }, item.id == cosmeticSelection);
                Place((RectTransform)button.transform, new Vector2(x,y), new Vector2(x+.293f,y+.155f));
                var color = Rect("Collection color", button.transform); Place(color, Vector2.zero, new Vector2(.025f,1)); color.gameObject.AddComponent<Image>().color = CosmeticLoadout.Style(item.styleId).Accent;
            }
            var previous = Action(content, "Previous collection page", "‹", () => { catalogPage = (catalogPage + pages - 1) % pages; Navigate("store"); });
            Place((RectTransform)previous.transform, new Vector2(0,.005f), new Vector2(.065f,.105f));
            Place(Label("Collection page", content, (catalogPage+1) + " / " + pages + "    ·    " + available.Count + " APPEARANCES", 14, AlphaTheme.Muted).rectTransform, new Vector2(.083f,.015f), new Vector2(.45f,.1f));
            var next = Action(content, "Next collection page", "›", () => { catalogPage = (catalogPage+1) % pages; Navigate("store"); });
            Place((RectTransform)next.transform, new Vector2(.54f,.005f), new Vector2(.603f,.105f));
            var clear = Action(content, "Clear cosmetic previews", "RESET OFFLINE APPEARANCE", () => { CosmeticLoadout.ClearPreviews(); storeNotice = "Offline army appearance restored."; Navigate("store"); });
            Place((RectTransform)clear.transform, new Vector2(0,-.105f), new Vector2(.295f,-.005f));
            var sync = Action(content, "Sync wardrobe", storeBusy ? "SYNCING…" : "SYNC ONLINE WARDROBE", RefreshWardrobe);
            Place((RectTransform)sync.transform, new Vector2(.31f,-.105f), new Vector2(.603f,-.005f)); sync.interactable = !storeBusy;
            StoreDetail(available.Find(item => item.id == cosmeticSelection));
            if (available.Count == 0)
                Place(Label("Empty collection", content, "Todavía no hay apariencias de este ámbito.", 20, AlphaTheme.Muted).rectTransform, new Vector2(0,.35f), new Vector2(.60f,.64f));
        }
        private void StoreFilter(string id, string text, float x)
        {
            var button = Action(content, "Store filter " + id, text, () => { cosmeticFilter = id; catalogPage = 0; Navigate("store"); }, cosmeticFilter == id);
            Place((RectTransform)button.transform, new Vector2(x,.695f), new Vector2(x+.14f,.795f));
        }
        private void StoreDetail(CosmeticCatalogItem item)
        {
            if (item == null) return;
            var panel = Panel("Cosmetic preview detail", content); Place(panel, new Vector2(.64f,-.075f), new Vector2(1,.795f));
            var preview = Rect("Live cosmetic model", panel); Place(preview, new Vector2(.025f,.545f), new Vector2(.975f,.985f));
            var picture = preview.gameObject.AddComponent<RawImage>(); picture.raycastTarget = false;
            preview.gameObject.AddComponent<CosmeticModelPreview>().Initialize(picture,item);
            Place(Label("Selected cosmetic name", panel, item.displayName, 27, AlphaTheme.Ink, true).rectTransform, new Vector2(.06f,.465f), new Vector2(.95f,.55f));
            Place(Label("Cosmetic description", panel, item.description, 16, AlphaTheme.Muted).rectTransform, new Vector2(.06f,.375f), new Vector2(.95f,.465f));
            string price = (item.priceMinor / 100m).ToString("0.00",CultureInfo.InvariantCulture) + " " + item.currency;
            Place(Label("Cosmetic price", panel, price + "  ·  PROVISIONAL", 14, AlphaTheme.Gold).rectTransform, new Vector2(.06f,.32f), new Vector2(.95f,.375f));
            var previewButton = Action(panel, "Preview " + item.id, CosmeticLoadout.IsPreviewEquipped(item.id) ? "OFFLINE PREVIEW EQUIPPED" : "TRY FREE IN OFFLINE PLAY", () => { CosmeticLoadout.EquipPreview(item.id); storeNotice = item.displayName + " preview equipped for offline play. No purchase was made."; Navigate("store"); }, true);
            Place((RectTransform)previewButton.transform, new Vector2(.055f,.19f), new Vector2(.945f,.315f));
            bool owned = cosmeticsReply != null && Array.IndexOf(cosmeticsReply.ownedIds ?? Array.Empty<string>(), item.id) >= 0;
            bool sandbox = false;
            foreach (var entitlement in cosmeticsReply?.entitlements ?? Array.Empty<CosmeticEntitlement>()) if (entitlement.itemId == item.id && entitlement.source == "sandbox") sandbox = true;
            var purchase = Action(panel, "Acquire " + item.id, owned ? sandbox ? "EQUIP ALPHA TEST ITEM" : "EQUIP IN ONLINE WARDROBE" : "PURCHASES OPEN SOON", () => EquipOnline(item.id));
            Place((RectTransform)purchase.transform, new Vector2(.055f,.055f), new Vector2(.945f,.18f)); purchase.interactable = owned && !storeBusy && OnlineControls.Service.SignedIn;
            footer.text = storeNotice;
        }
        private async void RefreshWardrobe()
        {
            if (storeBusy) return;
            if (!OnlineControls.Service.SignedIn) { storeNotice = "Sign in through Multiplayer to sync your online wardrobe."; Navigate("store"); return; }
            int revision = OnlineControls.Service.SessionRevision;
            await WardrobeOperation(async () => {
                var reply = await OnlineControls.Service.Send<OnlineCosmeticsReply>("/v1/cosmetics");
                if (!CurrentStoreSession(revision)) return;
                cosmeticsReply = reply;
                if (!match.World.IsNetworkReplica) CosmeticLoadout.SetOnlineEquipment(reply.equipped);
                storeNotice = "Online wardrobe synced. Offline previews do not grant paid ownership.";
            });
        }
        private async void EquipOnline(string id)
        {
            if (storeBusy || !OnlineControls.Service.SignedIn) return;
            int revision = OnlineControls.Service.SessionRevision;
            await WardrobeOperation(async () => {
                await OnlineControls.Service.Send<OnlineReply>("/v1/cosmetics/equip", "POST", new CosmeticEquipRequest { itemId = id });
                if (!CurrentStoreSession(revision)) return;
                var reply = await OnlineControls.Service.Send<OnlineCosmeticsReply>("/v1/cosmetics");
                if (!CurrentStoreSession(revision)) return;
                cosmeticsReply = reply;
                if (!match.World.IsNetworkReplica) CosmeticLoadout.SetOnlineEquipment(reply.equipped);
                storeNotice = "Online appearance saved for your next room. Combat rules are unchanged.";
            });
        }
        private async Task WardrobeOperation(Func<Task> action)
        {
            storeBusy = true; if (IsOpen && page == "store") Navigate("store");
            try { await action(); } catch (Exception error) { storeNotice = error.Message; }
            finally { storeBusy = false; if (root != null && IsOpen && page == "store") Navigate("store"); }
        }
        [Serializable] private sealed class CosmeticEquipRequest { public string itemId; }
    }
}
