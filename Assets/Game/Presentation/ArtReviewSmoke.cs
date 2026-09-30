using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // Explicit developer gallery: authored review spawns are never a normal match start.
    public static class ArtReviewSmoke
    {
        public static IEnumerator Run(MatchController match, string folder)
        {
            Directory.CreateDirectory(folder); match.enabled = false; yield return null;
            match.Rig.Camera.transform.rotation = Quaternion.Euler(55, 0, 0);
            match.Rig.SetHome(new Vector3(21, 0, 23), 12); match.Rig.Home();
            match.SetFeedback("ART REVIEW · Original Aven kit · blue and red team masks · staged gallery, simulation paused");
            string[] names = { "Tender", "Reedguard", "Stringwarden", "Strider" };
            for (int i = 0; i < 4; i++) Label(names[i], new Vector3(12 + i * 6, .04f, 16));
            string[] buildings = { "Hearth", "Shelter", "Muster Hall", "Red team" };
            for (int i = 0; i < 4; i++) Label(buildings[i], new Vector3(12 + i * 6, .04f, 27));
            string[] resources = { "Food", "Wood", "Metal", "Stone" };
            for (int i = 0; i < 4; i++) Label(resources[i], new Vector3(12 + i * 6, .04f, 11));
            void Label(string caption, Vector3 position)
            {
                var go = new GameObject(caption); go.transform.SetParent(match.transform, false); go.transform.position = position;
                go.transform.rotation = match.Rig.Camera.transform.rotation;
                var text = go.AddComponent<TextMesh>(); UiLocalization.SetText(text, caption); text.fontSize = 64; text.characterSize = .1f;
                text.anchor = TextAnchor.MiddleCenter; text.color = new Color(.96f, .95f, .84f);
            }
            match.SyncPresentation(1); match.Hud.PrepareOffscreenCapture(match.Rig.Camera); Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
            // The gallery has its own caption; gameplay HUD captures are provided by the full-match runner.
            var safe = match.transform.Find("Tablet HUD/Safe area");
            foreach (Transform child in safe) child.gameObject.SetActive(false);
            var captionRoot = new GameObject("Gallery caption", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            captionRoot.SetParent(safe, false); captionRoot.anchorMin = new Vector2(0, 1); captionRoot.anchorMax = Vector2.one;
            captionRoot.offsetMin = new Vector2(18, -70); captionRoot.offsetMax = new Vector2(-18, -14);
            captionRoot.GetComponent<Image>().color = new Color(.055f, .085f, .095f, .96f);
            var captionText = new GameObject("Title", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            captionText.transform.SetParent(captionRoot, false); captionText.rectTransform.anchorMin = Vector2.zero; captionText.rectTransform.anchorMax = Vector2.one;
            captionText.rectTransform.offsetMin = new Vector2(12, 0); captionText.rectTransform.offsetMax = new Vector2(-12, 0);
            captionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); captionText.fontSize = 24;
            captionText.text = "EMBERFIELD / AVEN COMPACT     ·     ORIGINAL ART SLICE"; captionText.alignment = TextAnchor.MiddleCenter;
            Canvas.ForceUpdateCanvases();
            bool pixels = PlayerSmoke.Capture(match, Path.Combine(folder, "greybox.png"));
            int lodGroups = 0; var meshes = new HashSet<Mesh>(); var materials = new HashSet<Material>();
            foreach (var lod in match.GetComponentsInChildren<LODGroup>()) { lodGroups++; foreach (var filter in lod.GetComponentsInChildren<MeshFilter>()) meshes.Add(filter.sharedMesh); }
            foreach (var lod in match.GetComponentsInChildren<LODGroup>()) foreach (var renderer in lod.GetComponentsInChildren<Renderer>()) materials.Add(renderer.sharedMaterial);
            bool passed = pixels && lodGroups == 16 && meshes.Count == 22 && materials.Count == 1 && match.World.TickIndex < 10;
            File.WriteAllText(Path.Combine(folder, "smoke.txt"), "Passed: " + passed + "\nScenario: ArtReview\nBuildGuid: " + Application.buildGUID + "\nResolution: " + Screen.width + "x" + Screen.height
                + "\nLodInstances: " + lodGroups + "\nUniqueLodMeshes: " + meshes.Count + "\nSharedArtMaterials: " + materials.Count + "\nCaptureHasPixels: " + pixels
                + "\nMethod: Explicit staged gallery, both Aven teams and eleven model identities; not a starting economy or frame-rate measurement.\n");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
