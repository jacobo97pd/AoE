# UI and touch UX guide

Status: the Phase 8 touch, safe-area, responsive action layout and minimap implementation is integrated. [Current phase](../tasks/CURRENT_PHASE.md) states the tested subset. Full comfort on physical touch devices remains unverified; the installed editor lacks Android tooling and no target device has been supplied. Future interaction ideas below are labeled separately.

## Information hierarchy

Keep the battlefield dominant. The top resource bar shows Food, Wood, Metal, Stone and current/capacity population. Selection and contextual actions sit along the lower edge. Offline matches add the objective clock and an upper-right minimap. Persistent army groups remain future work.

Use role names beside the original unit names until players learn them. The selected worker's most common commands are easier to reach than infrequent technology details. Costs and missing prerequisites belong beside the affected action.

## Gesture contract

| Input | Intended action | Conflict rule |
| --- | --- | --- |
| One-finger drag | Pan camera | Drag threshold prevents the starting touch becoming an order. |
| Two fingers | Pan and zoom around their centre | Cancels pending taps/selection; any lifted or replaced contact consumes the remaining session. |
| Select ON | Tap owned units or immediately drag a selection rectangle | Terrain taps clear selection; this mode issues no orders. |
| Minimap tap/drag | Focus camera | Preserves selection and orders; consumes the gesture as UI. |
| Tap owned unit/building | Select | Ignore world input that starts over interactive UI. |
| Double tap unit | Select similar owned visible units | Restrict to visible/selectable entities; no hidden enemy reveal. |
| Long press then drag | Selection rectangle | Show hold progress/rectangle; do not pan at the same time. |
| Tap terrain with units selected | Move | Show a short destination acknowledgement. |
| Tap enemy with military selected | Attack | Requires legal visible target and ownership. |
| Tap resource with Tenders selected | Gather | State the task clearly; reject an unavailable/depleted target visibly. |
| Tap own building | Select or contextual interaction | Make selection primary; show gather/repair/drop-off action when ambiguous. |

Thresholds for movement, double tap and long press are configurable and must be tested at different device densities. Finger-up completes a tap; an interrupted/canceled gesture must not issue a stale command. Additional fingers, lost focus, UI overlays and simultaneous unit/camera gestures need deliberate handling.

For construction, enter placement mode from the selected Tender, position a footprint, show valid/invalid feedback and provide visible confirm/cancel. Keep placement mode distinct enough that panning or touching a button cannot accidentally purchase a building.

## Desktop testing equivalents

Provide mouse selection and destination commands plus camera pan/zoom in the editor. Keyboard shortcuts may improve developer convenience but cannot be required for the shipped match loop. Exact currently implemented controls belong in the scene instructions/current status, especially where a right-click testing shortcut differs from the intended touch flow.

## Selection and command panels

Future refinement: show category counts for a mixed army and let a category filter that selection. The current panel shows its selected totals and contextual actions. Never silently include unseen enemies or distant offscreen units when the action says “visible.”

| Selection | Contextual commands, introduced with their systems |
| --- | --- |
| Tender | Build, gather, repair, stop |
| Military | Move, attack, stop; formation later |
| Producer | Train, queue details, upgrade, rally |
| Foundation | Progress, assigned workers, cancel if supported |
| Resource | Type, remaining stock when knowable, gather intent |

A command acknowledgement distinguishes accepted, rejected and unreachable actions. Use brief visual feedback and optional sound; do not spam modal alerts during battle. Show blocked production due to cost/population and expose the exact reason.

## Tablet and phone layouts

The implemented logical reference height is 720 for safe aspects at least 1.6, otherwise 1080. Main actions are 52 units high, contextual actions 60, and grids wrap according to available width. The minimap is 180×135 on phones or 220×165 on tablets. Research and match choices remain separate panels. Physical thumb reach still needs device review.

Use safe-area-aware anchors for top bars, side controls and bottom actions. Validate wide and nearly square tablets, narrow phone landscapes, notches/cutouts and OS navigation regions. Orientation policy remains a product configuration decision; landscape is the initial play layout. Do not assume a desktop preview proves physical usability.

Aim for generous touch targets comparable to roughly 44–48 logical UI units after scaling, with separation between destructive/cancel and routine actions. This is a project starting guideline, not a claim of compliance with a particular platform standard. Selection hit regions can exceed small meshes, but crowded-unit disambiguation must remain predictable.

## Accessibility and learning

Never encode team, valid placement, missing resources or victory progress using color alone. Combine color with outlines, shape, text or iconography. Test text contrast over bright/dark map regions. Avoid tiny resource counters and text-heavy tooltips during active combat.

Introduce one action at a time in the eventual tutorial. Let the player perform the action before the next instruction. Show selection and command feedback promptly, then explain a missing prerequisite only when relevant. Post-game should support one useful next attempt, with restart/requeue easy to find.

## Validation scenarios

MobileInputTest should make pan, pinch/scroll zoom, tap selection, group selection, resource/building interaction and orders quick to exercise. Add phone/tablet aspect presets. Record command misfires, missed taps, camera boundary problems and obscured units. Physical device checks must include two-finger interruptions, UI/world overlap, long-press cancellation and large mixed armies.
