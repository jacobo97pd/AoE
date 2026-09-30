# Alpha 0.3 Windows package acceptance

All twelve functional checks passed on application GUID **35abc41770ce473f8606780a352eccaf**, Unity6000.3.23f1, version0.3.0. [Build](build.txt). [Run inventory](player-runs.json). [Visual gallery](gallery.html).

| Check | Format | Evidence and scope |
| --- | --- | --- |
| Expansion | 1280×720 | [Report](expansion-smoke.json): 76 real button clicks, eight factions across three maps, six new signature units trained by ordinary AI, six live store inspections, cosmetic preview leaves World unchanged |
| Frontend | 1280×720 | [Report](frontend-1280x720.json): home, store, settings, online entry, real setup, local pause and developed settlement |
| Frontend | 1440×1080 | [Report](frontend-1440x1080.json): same flow at tablet aspect |
| Aven faction | 1280×720 | [Report](faction-aven.txt): physical resource gathering, actual chooser override, paid charter/Threadkeeper/relay/research |
| Serevin faction | 1440×1080 | [Report](faction-serevin.txt): actual chooser, Ashrunner reposition, outpost pack/deploy, health retained and unique research |
| Shared research | 1440×1080 | [Report](research.txt): all four Eras, nine shared research items, existing/new troop upgrades and unchanged enemy |
| Aven Conquest | 1280×720 | [Report](offline-conquest.txt): natural victory at459.50 simulated seconds, fog, frozen result and fresh restart |
| Serevin Dominion | 1440×1080 | [Report](offline-dominion.txt): natural objective victory at884.50 seconds, fog, frozen result and fresh restart |
| Economy | 1280×720 | [Report](economy.txt): gather, deliver, build, train and fight |
| Combat | 1440×1080 | [Report](combat.txt): attack taps, real damage/projectiles/deaths and view cleanup |
| Historical PvP / forest | 1280×720 | [Report](online-historical.json): two real clients, HTTP/SQLite/C# authority, killed/restarted guest, recovered orders, scoped history and return to lobby |
| Fantasy PvP / Caribbean | 1440×1080 | [Report](online-fantasy.json): same native and authoritative reconnect flow in the fantasy realm |

All offline/practice development drivers accelerate simulation; they are not FPS tests. Online runs operate the real authority in normal time. Only the two frontend drivers observe an additional eight-second ordinary capped gameplay window. None is a human playtest.

The retained economy/combat text reports do not embed an application GUID. Their run inventory records that they were executed sequentially between GUID-verified runs without rebuilding the player. The other native reports identify the actual loaded application GUID.

The separate [render report](render.json) passed the fixed20Hz,400-tick serialized offscreen probe. [Capture](render.png). Its timings do not represent display FPS or mobile performance; the [verification report](../../ALPHA_03_VERIFICATION.md) states the measurement method and limits.

## Additional native views

- [Tablet main menu](frontend-1440x1080-main-menu.png), [store](frontend-1440x1080-store.png), [setup](frontend-1440x1080-skirmish-setup.png), [settings](frontend-1440x1080-settings.png), [research](frontend-1440x1080-research.png).
- [Historical online battle](online-historical-gameplay.png), [reconnection](online-historical-peer-reconnected.png), [history](online-historical-history.png), [returned lobby](online-historical-returned-lobby.png).
- [Fantasy online battle](online-fantasy-gameplay.png), [reconnection](online-fantasy-peer-reconnected.png), [history](online-fantasy-history.png), [returned lobby](online-fantasy-returned-lobby.png).

The gallery's menu illustration is decorative key art. The models, terrain, units, interface and wardrobe previews in native captures are rendered by the packaged Unity game. Creature overlap at battle density remains an alpha art/navigation limit; individual models can be inspected in the wardrobe.
