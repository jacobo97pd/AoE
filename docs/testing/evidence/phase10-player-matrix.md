# Phases 7-10 final packaged functional matrix

Recorded UTC: 2026-09-09T23:25:15.0651291Z

Build GUID: d1b7b1cbc28740e994945476895e1295

Result: 19/19 PASS. All reports were freshly generated after this Windows build. Every report providing a build GUID agrees. The three legacy combat/economy reports do not emit GUIDs; their fresh timestamps and the unchanged delivered assembly hashes identify this run.

Presentation DLL SHA256: 117595599B81292F1FD19C535457ABB787A4ED98465558719F98A4989CD4BD91
Simulation DLL SHA256: 00E8D96C11DE844B96823F9F7772DC6A40148C1CE8BEBB62E73D50A253FE09D2

All player logs were checked for runtime exceptions/errors and shader errors. All 19 captures exist. These accelerated scripted checks exercise ordinary commands and UI button dispatches, not physical touch, manual play, or rendered performance.

| Scenario | Viewport | Result | Evidence |
| --- | --- | --- | --- |
| Offline / aven / Conquest | 1280x720 | PASS | [Report](phase7-offline-aven-Conquest-1280x720.txt) |
| Offline / aven / Dominion | 1280x720 | PASS | [Report](phase7-offline-aven-Dominion-1280x720.txt) |
| Faction / aven | 1280x720 | PASS | [Report](phase10-faction-aven-1280x720.txt) |
| Offline / serevin / Conquest | 1280x720 | PASS | [Report](phase7-offline-serevin-Conquest-1280x720.txt) |
| Offline / serevin / Dominion | 1280x720 | PASS | [Report](phase7-offline-serevin-Dominion-1280x720.txt) |
| Faction / serevin | 1280x720 | PASS | [Report](phase10-faction-serevin-1280x720.txt) |
| Technology | 1280x720 | PASS | [Report](phase10-technology-1280x720.txt) |
| Combat | 1280x720 | PASS | [Report](phase10-combat-1280x720.txt) |
| Art gallery | 1280x720 | PASS | [Report](phase9-art-1280x720.txt) |
| Offline / aven / Conquest | 1440x1080 | PASS | [Report](phase7-offline-aven-Conquest-1440x1080.txt) |
| Offline / aven / Dominion | 1440x1080 | PASS | [Report](phase7-offline-aven-Dominion-1440x1080.txt) |
| Faction / aven | 1440x1080 | PASS | [Report](phase10-faction-aven-1440x1080.txt) |
| Offline / serevin / Conquest | 1440x1080 | PASS | [Report](phase7-offline-serevin-Conquest-1440x1080.txt) |
| Offline / serevin / Dominion | 1440x1080 | PASS | [Report](phase7-offline-serevin-Dominion-1440x1080.txt) |
| Faction / serevin | 1440x1080 | PASS | [Report](phase10-faction-serevin-1440x1080.txt) |
| Technology | 1440x1080 | PASS | [Report](phase10-technology-1440x1080.txt) |
| Combat | 1440x1080 | PASS | [Report](phase10-combat-1440x1080.txt) |
| Art gallery | 1440x1080 | PASS | [Report](phase9-art-1440x1080.txt) |
| Economy | 1440x1080 | PASS | [Report](phase10-economy-1440x1080.txt) |

Offline results are natural, frozen on completion and followed by a fresh restart. Both viewport runs agree: Aven/Conquest 419.8 s; Serevin/Conquest 609.2 s; Aven/Dominion 959.9 s; Serevin/Dominion 1066.65 s. The local-first packaged driver differs from the alternating-order headless natural-match runner.

Visual inspection covers final phone/tablet gameplay, fog, minimap, objective/menu/results, faction controls, research and the original art gallery. The public beacon font and fog render before camera-space UI, including the opaque result panel. Synthetic input and screenshots cannot establish mobile comfort or human fun.

Copied text smoke reports omit trailing whitespace in empty Failure fields; raw player reports remain in TestResults. No result values were changed.
