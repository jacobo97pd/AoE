using UnityEngine;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // The wall run on the HUD (WallRunTool): its three buttons on the context row, and the label that hangs beside the
    // run's end with what the green stretches cost against the stock. The label is placed every frame and written only
    // when the run is judged again, so a still pointer costs nothing.
    public sealed partial class MatchHud
    {
        private RectTransform wallRunTag;
        private Text wallRunText;
        private string wallRunLabel;

        private void CreateWallRunTag()
        {
            wallRunTag = Rect("Wall run cost", canvasObject.transform);
            var back = wallRunTag.gameObject.AddComponent<Image>();
            back.color = new Color(AlphaTheme.Background.r, AlphaTheme.Background.g, AlphaTheme.Background.b, .86f); back.raycastTarget = false;
            wallRunTag.anchorMin = wallRunTag.anchorMax = new Vector2(.5f, .5f); wallRunTag.pivot = Vector2.zero;
            wallRunTag.sizeDelta = new Vector2(360, 70);
            wallRunText = Label("Cost", wallRunTag, 15); Stretch(wallRunText.rectTransform, 10, 4);
            wallRunText.resizeTextForBestFit = true; wallRunText.resizeTextMinSize = 11; wallRunText.resizeTextMaxSize = 15;
            wallRunTag.gameObject.SetActive(false);
        }

        private void RefreshWallRun()
        {
            var run = match.Economy.WallRun;
            run.Sync();
            bool shown = run.HasLabel && PlaceWallRunTag(run.LabelAnchor);
            if (wallRunTag.gameObject.activeSelf != shown) wallRunTag.gameObject.SetActive(shown);
            if (!shown) return;
            if (!ReferenceEquals(wallRunLabel, run.Label))
            {
                // As tall as its lines: two for a clean run, three once something is closed, short or in the way.
                wallRunLabel = run.Label; int lines = 1;
                foreach (char c in wallRunLabel) if (c == '\n') lines++;
                wallRunTag.sizeDelta = new Vector2(wallRunTag.sizeDelta.x, 14 + lines * 20);
            }
            Show(wallRunText, run.Label);
            // The ghosts' own green, orange and red, in the theme's lighter shades so the text reads on the dark panel.
            var tone = run.Tone == WallRunTool.Buildable ? AlphaTheme.Positive : run.Tone == WallRunTool.Unaffordable ? AlphaTheme.Gold : AlphaTheme.Danger;
            if (wallRunText.color != tone) wallRunText.color = tone;
        }

        // Up and to the right of the run's end, and kept on the screen.
        private bool PlaceWallRunTag(Vector3 anchor)
        {
            var screen = match.Rig.Camera.WorldToScreenPoint(anchor);
            if (screen.z <= 0) return false;
            var full = (RectTransform)canvas.transform;
            var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(full, screen, eventCamera, out var local)) return false;
            var size = wallRunTag.sizeDelta; var bounds = full.rect;
            local.x = Mathf.Clamp(local.x + 24, bounds.xMin + 8, bounds.xMax - size.x - 8);
            local.y = Mathf.Clamp(local.y + 16, bounds.yMin + 8, bounds.yMax - size.y - 8);
            wallRunTag.anchoredPosition = local;
            return true;
        }

        private void AddWallRunActions()
        {
            var run = match.Economy.WallRun;
            Button(contextRow, "Confirm wall", () => match.Economy.ConfirmBuild()).GetComponentInParent<Button>().interactable = run.AcceptedCount > 0;
            Button(contextRow, "Undo corner", () => match.Economy.WallRun.UndoCorner()).GetComponentInParent<Button>().interactable = run.CornerCount > 0;
            Button(contextRow, "Cancel wall", match.Economy.CancelBuild);
        }
    }
}
