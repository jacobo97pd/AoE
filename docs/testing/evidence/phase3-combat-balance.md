# Shipped combat counter checks

Unity: 6000.3.23f1

Rules SHA256: `FF8AC4FBDD56020366D92E5826626DB6FBC72CF52A846F04732050F471EBA662`

Open terrain; 7 m between front units; both sides receive attack orders at tick 0. Each military unit costs 80 total resources, uses one population and trains in 160 ticks. Resource types retain different economic value, so this is a nominal cost comparison, not proof of competitive balance. Every scenario swaps player/spawn sides. Two units of the disadvantaged type must overcome one favored unit. Ticks advance directly; no performance measurement. Projectiles finish before the result is recorded.

| Blue | Red | Expected winner | Actual winner | Seconds | Survivors / health | Pass |
| --- | --- | --- | --- | --- | --- | --- |
| 1 reedguard | 1 strider | 1 | 1 | 6.65 | 1 / 34 | True |
| 1 strider | 1 reedguard | 2 | 2 | 6.65 | 1 / 34 | True |
| 1 reedguard | 2 strider | 2 | 2 | 5.50 | 2 / 120 | True |
| 2 strider | 1 reedguard | 1 | 1 | 5.50 | 2 / 120 | True |
| 1 stringwarden | 1 reedguard | 1 | 1 | 5.40 | 1 / 22 | True |
| 1 reedguard | 1 stringwarden | 2 | 2 | 5.40 | 1 / 22 | True |
| 1 stringwarden | 2 reedguard | 2 | 2 | 3.75 | 2 / 132 | True |
| 2 reedguard | 1 stringwarden | 1 | 1 | 3.75 | 2 / 132 | True |
| 1 strider | 1 stringwarden | 1 | 1 | 5.80 | 1 / 56 | True |
| 1 stringwarden | 1 strider | 2 | 2 | 5.80 | 1 / 56 | True |
| 1 strider | 2 stringwarden | 2 | 2 | 6.35 | 1 / 70 | True |
| 2 stringwarden | 1 strider | 1 | 1 | 6.35 | 1 / 70 | True |

Passed: True
