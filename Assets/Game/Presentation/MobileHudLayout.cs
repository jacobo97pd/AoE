using UnityEngine;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // Deterministic layout from the usable viewport; callers supply ordinary screen/safe-area measurements.
    public static class MobileHudLayout
    {
        public const float MinimumTarget = 48;
        // Readable widths for the two rows. A command keeps its label on one line at this width, and a
        // context tile still fits "name + cost" on two: the whole band is one row of each whenever it can be.
        public const float MinimumActionWidth = 90, MinimumContextWidth = 140;
        public const float Gap = 8;
        private const float Margin = 18;

        public readonly struct ButtonRows
        {
            public readonly int Columns, Rows;
            public readonly Vector2 CellSize;
            public float Height => Rows == 0 ? 0 : Rows * CellSize.y + (Rows - 1) * Gap;
            public ButtonRows(int columns, int rows, Vector2 cellSize)
            { Columns = columns; Rows = rows; CellSize = cellSize; }
        }

        public readonly struct Metrics
        {
            public readonly Rect SafePixels;
            public readonly Vector2 AnchorMin, AnchorMax, ReferenceResolution, LogicalSize;
            public readonly float Scale, CommandHeight;
            public readonly Rect Actions, Context, Details, Feedback;
            public readonly ButtonRows ActionButtons, ContextButtons;
            public Metrics(Rect safe, Vector2 anchorMin, Vector2 anchorMax, Vector2 reference, Vector2 logical,
                float scale, float commandHeight, Rect actions, Rect context, Rect details, Rect feedback,
                ButtonRows actionButtons, ButtonRows contextButtons)
            {
                SafePixels = safe; AnchorMin = anchorMin; AnchorMax = anchorMax;
                ReferenceResolution = reference; LogicalSize = logical; Scale = scale; CommandHeight = commandHeight;
                Actions = actions; Context = context; Details = details; Feedback = feedback;
                ActionButtons = actionButtons; ContextButtons = contextButtons;
            }
        }

        public static Metrics Resolve(int screenWidth, int screenHeight, Rect safeArea, int actionCount, int contextCount)
        {
            int width = Mathf.Max(1, screenWidth), height = Mathf.Max(1, screenHeight);
            var safe = Rect.MinMaxRect(Mathf.Clamp(safeArea.xMin, 0, width), Mathf.Clamp(safeArea.yMin, 0, height),
                Mathf.Clamp(safeArea.xMax, 0, width), Mathf.Clamp(safeArea.yMax, 0, height));
            if (!(safe.width > 0) || !(safe.height > 0)) safe = new Rect(0, 0, width, height);
            bool wide = safe.width / safe.height >= 1.6f;
            float logicalHeight = wide ? 720 : 1080;
            float scale = safe.height / logicalHeight;
            var logical = safe.size / scale;
            // CanvasScaler matches full-screen height; compensate for safe-area vertical insets.
            var reference = new Vector2(wide ? 1280 : 1440, logicalHeight * height / safe.height);
            float rowWidth = Mathf.Max(1, logical.x - Margin * 2);
            var actions = Rows(actionCount, rowWidth, MinimumActionWidth, MinimumTarget);
            float selectionWidth = Mathf.Clamp(logical.x * .26f, 280, 370);
            float contextWidth = contextCount > 0 ? rowWidth - selectionWidth - Gap * 2 : rowWidth;
            // Tall enough for a name over a three-resource cost; the selection block sets the band's height
            // anyway while the context stays on one row, so this costs nothing there.
            var context = Rows(contextCount, contextWidth, MinimumContextWidth, 62);
            var feedbackRect = new Rect(Margin, 6, rowWidth, 26);
            var actionRect = new Rect(Margin, feedbackRect.yMax + Gap, rowWidth, actions.Height);
            float contentY = actionRect.yMax + Gap;
            float contentHeight = contextCount > 0 ? Mathf.Max(72, context.Height) : 56;
            var contextRect = new Rect(Margin + (contextCount > 0 ? selectionWidth + Gap * 2 : 0), contentY, contextWidth, context.Height);
            var detailsRect = new Rect(Margin, contentY, contextCount > 0 ? selectionWidth : rowWidth, contentHeight);
            return new Metrics(safe, new Vector2(safe.xMin / width, safe.yMin / height),
                new Vector2(safe.xMax / width, safe.yMax / height), reference, logical, scale, detailsRect.yMax + 12,
                actionRect, contextRect, detailsRect, feedbackRect, actions, context);
        }

        private static ButtonRows Rows(int count, float width, float minimumWidth, float height)
        {
            count = Mathf.Max(0, count);
            int columns = Mathf.Clamp(Mathf.FloorToInt((width + Gap) / (minimumWidth + Gap)), 1, Mathf.Max(1, count));
            int rows = Mathf.CeilToInt(count / (float)columns);
            return new ButtonRows(columns, rows, new Vector2((width - (columns - 1) * Gap) / columns, height));
        }

        public static void Apply(Metrics metrics, RectTransform safeRoot, RectTransform commands,
            RectTransform actions, GridLayoutGroup actionGrid, RectTransform context, GridLayoutGroup contextGrid,
            RectTransform details, RectTransform feedback)
        {
            safeRoot.anchorMin = metrics.AnchorMin; safeRoot.anchorMax = metrics.AnchorMax;
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
            commands.anchorMin = Vector2.zero; commands.anchorMax = new Vector2(1, 0);
            commands.offsetMin = Vector2.zero; commands.offsetMax = new Vector2(0, metrics.CommandHeight);
            Place(actions, metrics.Actions); Place(context, metrics.Context);
            Place(details, metrics.Details); Place(feedback, metrics.Feedback);
            ApplyGrid(actionGrid, metrics.ActionButtons); ApplyGrid(contextGrid, metrics.ContextButtons);
        }

        private static void Place(RectTransform rect, Rect area)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = area.position; rect.sizeDelta = area.size;
        }

        private static void ApplyGrid(GridLayoutGroup grid, ButtonRows rows)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = rows.Columns;
            grid.spacing = new Vector2(Gap, Gap); grid.cellSize = rows.CellSize;
            grid.childAlignment = TextAnchor.UpperLeft;
        }

        public static bool PlaceSelection(RectTransform fullCanvas, RectTransform box, Rect screenRect, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(fullCanvas, screenRect.min, eventCamera, out var min) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(fullCanvas, screenRect.max, eventCamera, out var max)) return false;
            // The box belongs to the full canvas, not the inset safe area: screen/world selection must not shift at a notch.
            Place(box, new Rect(min - fullCanvas.rect.min, max - min));
            return true;
        }
    }
}
