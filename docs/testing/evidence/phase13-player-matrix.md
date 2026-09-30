# Phase 13 packaged verification matrix

All **nine checks passed** against the same Windows development package, built with Unity **6000.3.23f1**. The [build report](phase13-build.txt) records zero errors, zero warnings, **164,137,326 bytes** and **14.086756 seconds**. Application build GUID: `cdf8183365404135975644fd3c935a58`.

The two online reports independently record identical player executable SHA256 `56C28AFBA054092826B7A8A80BA3767DAA87967F182C0C562EB2F98799175B7A` and authority DLL SHA256 `EA9E22988C9E9C4E3B0DA844F0958A5E6987769561C6619E9DB5AAD8CD2BDBC7`. The executable hash identifies that file, not the entire Unity data directory. The offline and faction/technology reports include the same application GUID. The older combat/economy report schemas omit it; those checks ran from the same unchanged build output, without an intervening build.

## Results

| Check | Resolution | Result and evidence |
| --- | --- | --- |
| Online private 1v1, two standalone clients | 1280×720 | [PASS](phase13-online-1280.json), 30.04 seconds; guest process terminated at tick 151 and fresh process reconnected at tick 520; final tick 534. |
| Online private 1v1, two standalone clients | 1440×1080 | [PASS](phase13-online-1440.json), 30.30 seconds; guest process terminated at tick 150 and fresh process reconnected at tick 518; final tick 533. |
| Offline Aven / Conquest | 1280×720 | [PASS](phase13-offline-aven-conquest-1280.txt), natural winner 1 at tick 8396 / 419.8 simulated seconds; 71 peak units; fresh restart verified. |
| Offline Serevin / Dominion | 1440×1080 | [PASS](phase13-offline-serevin-dominion-1440.txt), natural winner 2 at tick 21333 / 1066.65 simulated seconds; 61 peak units; fresh restart verified. |
| Aven faction drill | 1280×720 | [PASS](phase13-faction-aven-1280.txt), nine visible button dispatches; physically gathered resources, faction mechanic, unique action and research verified. |
| Serevin faction drill | 1440×1080 | [PASS](phase13-faction-serevin-1440.txt), twelve visible button dispatches; unique action/research and relocated outpost health verified. |
| Eras and technology | 1440×1080 | [PASS](phase13-technology-1440.txt), all four Eras and nine shared research items; exact costs, rejected prerequisites, existing/new troop upgrades. |
| Combat | 1280×720 | [PASS](phase13-combat-1280.txt), accepted attack, damage, projectiles, all three common roles attacking, three deaths and removed views. |
| Economy | 1440×1080 | [PASS](phase13-economy-1440.txt), physical gathering, construction, production and the trained soldier fighting. |

Each online run uses real HTTP, SQLite and the dedicated C# authority at ordinary 20 Hz. Both clients move a unit, train a Tender and deliver resources. Their local Worlds are read-only replicas, and hidden opponents are absent from the initial observations. The driver terminates the guest process, requires the host to observe disconnection and at least 20 additional authority ticks while that process is absent, then allows a new guest process to sign in. The guest recovers the same match and issues a valid subsequent command. Host surrender produces the correct recipient-normalized winner, final-state statistics and persisted account history.

Each run records **19 native PNG captures and nine visible button dispatches** across the host, guest and guest relogin. Both reports verify the Settings modal pauses its local practice World. Test credential configurations were deleted and owned processes were stopped. The reports contain no credentials. `EvidenceWriteRetries` counts recoverable Windows evidence-file sharing conflicts; both final runs completed with empty failure fields.

## Selected native captures

- [Settings and guide, 1280×720](phase13-settings-1280.png)
- [Private lobby, 1280×720](phase13-online-lobby-1280.png)
- [Online gameplay, 1440×1080](phase13-online-gameplay-1440.png)
- [Fresh-process reconnection, 1440×1080](phase13-online-reconnected-1440.png)
- [Server-owned result, 1440×1080](phase13-online-result-1440.png)

The online JSON files list all original capture paths under ignored `TestResults/`. The selected captures above are retained with this report.

## Reproduce

From the repository root, after building the authority and Windows player:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Smoke-Online.ps1 -Width 1280 -Height 720 -TimeoutSeconds 240
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Smoke-Online.ps1 -Width 1440 -Height 1080 -TimeoutSeconds 240
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Smoke-Player.ps1 -EvidenceLabel Phase13 -Scenario Offline -Faction aven -Mode Conquest -Width 1280 -Height 720 -TimeoutSeconds 300
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Smoke-Player.ps1 -EvidenceLabel Phase13 -Scenario Offline -Faction serevin -Mode Dominion -Width 1440 -Height 1080 -TimeoutSeconds 300
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Smoke-Player.ps1 -EvidenceLabel Phase13 -Scenario Faction -Faction aven -Width 1280 -Height 720
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Smoke-Player.ps1 -EvidenceLabel Phase13 -Scenario Faction -Faction serevin -Width 1440 -Height 1080
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Smoke-Player.ps1 -EvidenceLabel Phase13 -Scenario Technology -Width 1440 -Height 1080
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Smoke-Player.ps1 -EvidenceLabel Phase13 -Scenario Combat -Width 1280 -Height 720
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Smoke-Player.ps1 -EvidenceLabel Phase13 -Scenario Economy -Width 1440 -Height 1080
```

The root separately ran service verification, Unity verification with a final PlayMode rerun, the final build, and these packaged checks. `Verify-Alpha.ps1 -Stage All` provides the serial pipeline for a future complete invocation; it was not executed as one combined invocation for this evidence. `-Stage Manifest` only inventories artifacts and marks its result `InventoryOnly`.

These are automated functional checks. Offline/practice drivers accelerate simulation; online checks do not. A natural offline outcome means ordinary rules produced the winner, not that a human played the match. Desktop captures at phone/tablet aspect ratios do not establish physical touch comfort, displayed FPS, WAN reliability or production host capacity. [Current acceptance gates](../../tasks/CURRENT_PHASE.md) retain those limits and the existing balance/navigation findings.
