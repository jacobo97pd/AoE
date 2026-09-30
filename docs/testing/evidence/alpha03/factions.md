# Shipped faction counter checks

Unity: 6000.3.23f1

Rules SHA256: `B4FF84B570F0A18B9CFCE017669BC4F50EA31347C63CCAE282BBD865AD40BA5E`

Unmodified shipped unit definitions and explicit Aven/Serevin player assignments. Faction passives apply; no research, charter, relay or Reposition is activated. Authored spawns bypass resource payment so the displayed costs describe nominal investment, not a simulated build order. The Ashrunner costs more than either common unit in these checks; this is a counter-behavior regression, not proof of competitive faction balance.

Open 64 by 48 cell terrain; 1 m cells; front positions (28.5, 23.5) m and (35.5, 23.5) m, 7 m apart. A second unit starts 1 m along the Z axis. Both sides receive explicit attack orders against the opposing front unit before tick 0. Each case swaps player IDs, factions and spawn sides. The favored type should win one-on-one; two disadvantaged units should overcome one favored unit. The deadline is 90 simulation seconds, including any remaining projectile flight after a side is eliminated. A result with projectiles still in flight fails. Ticks advance directly; no performance measurement.

| Unit | Faction | Food | Wood | Metal | Stone | Nominal total | Population | Train ticks |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| reedguard | aven | 60 | 20 | 0 | 0 | 80 | 1 | 160 |
| stringwarden | aven | 30 | 50 | 0 | 0 | 80 | 1 | 160 |
| ashrunner | serevin | 80 | 20 | 0 | 0 | 100 | 1 | 160 |

| Case | Player 1 / faction | Player 2 / faction | Expected winner | Actual winner | Orders accepted (P1 / P2) | Seconds | Survivors / health (P1; P2) | Projectiles left | Pass |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 1 reedguard / aven | 1 ashrunner / serevin | 1 | 1 | True / True | 5.40 | 1 / 64; 0 / 0 | 0 | True |
| 2 | 1 ashrunner / serevin | 1 reedguard / aven | 2 | 2 | True / True | 5.40 | 0 / 0; 1 / 64 | 0 | True |
| 3 | 1 reedguard / aven | 2 ashrunner / serevin | 2 | 2 | True / True | 9.10 | 0 / 0; 1 / 32 | 0 | True |
| 4 | 2 ashrunner / serevin | 1 reedguard / aven | 1 | 1 | True / True | 9.10 | 1 / 32; 0 / 0 | 0 | True |
| 5 | 1 ashrunner / serevin | 1 stringwarden / aven | 1 | 1 | True / True | 6.95 | 1 / 25; 0 / 0 | 0 | True |
| 6 | 1 stringwarden / aven | 1 ashrunner / serevin | 2 | 2 | True / True | 6.95 | 0 / 0; 1 / 25 | 0 | True |
| 7 | 1 ashrunner / serevin | 2 stringwarden / aven | 2 | 2 | True / True | 4.35 | 0 / 0; 2 / 101 | 0 | True |
| 8 | 2 stringwarden / aven | 1 ashrunner / serevin | 1 | 1 | True / True | 4.35 | 2 / 101; 0 / 0 | 0 | True |

Winner 0 means neither side has an exclusive surviving force (mutual elimination or deadline reached with both sides alive).

Cases: 8
Passed: True
