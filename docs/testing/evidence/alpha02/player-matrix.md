# Alpha 0.2 Windows package acceptance

All eleven functional checks below passed on application GUID **`0e78fcce58ec4fb489ca9baf9ed74b19`**, Unity **6000.3.23f1**, version **0.2.0**. The Windows build has zero errors/warnings and is 170,386,522 bytes. [Build](build.txt), [runner outcomes](player-runs.json), [delivery report](../../../tasks/ALPHA_02_REPORT.md).

| Check | Resolution | Verified behavior | Evidence |
| --- | --- | --- | --- |
| Product frontend | 1280×720 | 10 visible controls; Home, Store, Settings, Multiplayer, both faction/mode choices; chosen local match develops | [Report](frontend-1280x720.json) |
| Product frontend | 1440×1080 | Same complete journey on tablet aspect ratio | [Report](frontend-1440x1080.json) |
| Two-client online | 1280×720 | Paid orders, training/delivery, hidden enemy, actual guest process restart, reconnect, result/history and both returned lobbies | [Report](online-1280x720.json) |
| Two-client online | 1440×1080 | Same real authority and native UI flow | [Report](online-1440x1080.json) |
| Aven faction | 1280×720 | Paid charter/Threadkeeper/relay and unique research | [Report](faction-aven.txt) |
| Serevin faction | 1440×1080 | Ashrunner reposition, outpost pack/deploy with damage retained and unique research | [Report](faction-serevin.txt) |
| Shared research | 1440×1080 | All four Eras, all nine shared research items and existing/new-unit upgrades | [Report](research.txt) |
| Aven Conquest | 1280×720 | Ordinary AI match naturally finishes; fog, pause/results/restart; 419.8 simulated seconds | [Report](offline-conquest.txt) |
| Serevin Dominion | 1440×1080 | Ordinary AI objective match naturally finishes; 1066.65 simulated seconds | [Report](offline-dominion.txt) |
| Economy | 1280×720 | Gather, deliver, build, train and fight | [Report](economy.txt) |
| Combat | 1440×1080 | Targeted orders, damage/death and drill completion | [Report](combat.txt) |

The seven offline/practice regression drivers accelerate simulation. Product setup develops two minutes of simulation quickly before observing eight seconds of ordinary capped gameplay. Online runs use independent native processes and a real HTTP/SQLite/C# authority at normal speed. None of these automated runs is a human playtest.

## Native captures

These PNGs render the game's camera and native Unity UI. The Home/Store backgrounds use the generated decorative illustration; their live controls and type are built in Unity. The illustration is separate from the modeled in-game world.

| View | Wide | Tablet |
| --- | --- | --- |
| Home | [Open](main-menu-1280x720.png) | [Open](main-menu-1440x1080.png) |
| Store | [Open](store-1280x720.png) | [Open](store-1440x1080.png) |
| Settings | [Open](settings-1280x720.png) | [Open](settings-1440x1080.png) |
| Skirmish setup | [Open](skirmish-setup-1280x720.png) | [Open](skirmish-setup-1440x1080.png) |
| Match opening | [Open](match-start-1280x720.png) | [Open](match-start-1440x1080.png) |
| Developed settlement | [Open](settlement-1280x720.png) | [Open](settlement-1440x1080.png) |
| Research | [Open](research-1280x720.png) | [Open](research-1440x1080.png) |
| Returned online lobby | [Open](returned-lobby-1280x720.png) | [Open](returned-lobby-1440x1080.png) |

Additional native world captures: [Aven relay](aven-relay.png), [Serevin outpost](serevin-outpost.png), [river/ford with armies](river-gameplay.png). Visual review confirms continuous terrain color, distinct faction shapes, navigable-looking fords and a complete starting Hearth above the HUD. Fog privacy remains checked by simulation and presentation tests.

## Reproduce

From the repository root, build with `tools/Build-Unity.ps1 -Target Windows`. Run `tools/Smoke-AlphaProduct.ps1 -Width 1280 -Height 720` and repeat at 1440×1080. `tools/Smoke-Online.ps1` accepts the same dimensions and starts/stops its own disposable authority and clients; `-TimeoutSeconds 240` allows the full journey. The runner removes temporary credential configurations; no credentials are retained in this evidence directory.

`tools/Smoke-Player.ps1 -EvidenceLabel Alpha02` selects `-Scenario Faction`, `Technology`, `Offline`, `Economy` or `Combat`. Faction cases accept `-Faction aven|serevin`; offline cases additionally accept `-Mode Conquest|Dominion`. The exact cases are listed above; allow 300 seconds for Offline and 180 for other drivers. `tools/Verify-Alpha.ps1 -Stage All` includes the core suites, Windows build, one online pair and one product walkthrough; the other matrix cases are explicit additional checks.

The separate `tools/Profile-OfflinePlayer.ps1` benchmark is recorded as the twelfth runner entry and is not counted as a functional smoke. [Performance comparison](render-comparison.json) and [delivery report](../../../tasks/ALPHA_02_REPORT.md#rendered-performance) explain its serialized timing protocol and limits.
