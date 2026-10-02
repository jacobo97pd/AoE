# Offline AI behaviour

The offline controller (`Assets/Game/Simulation/OfflineAi.cs`) plays by the shipped rules: it observes
through fog, submits ordinary commands and is refused by the ordinary validation. It thinks every
`ThinkInterval` ticks and may submit only `CommandsPerThink` commands per think (4 at Normal). That budget
is the scarce resource in this file. Anything that wastes it — an order the rules refuse, or an order
re-issued to units that already have it — is taken directly out of that controller's economy and army.

## Rejected orders are now counted by reason

`OfflineAiStatistics.Rejections` counts refusals by `CommandRejection`, and
`Assets/Game/Editor/OfflineMatchVerification.cs` prints them per player in
`TestResults/offline-matches.md`. Before this existed only a total was reported, which is why the defect
below survived unnoticed: a number with no reason attached looks like ordinary friction.

`tools/Verify-Unity.ps1 -Stage Offline` now **fails** when a controller with at least 200 orders has more
than `OfflineMatchVerification.MaximumRejectedPercent` (10%) of them refused. Ordinary play sits near 2%.

## The retreat loop (found and fixed 2026-09-16)

A wounded soldier below a quarter health and away from home is sent back with
`MoveCommand(..., NearKnownPoint(home, unit.Position))`. Two details made that a permanent loop:

- `NearKnownPoint` returns a ring **four to eight cells** out from the Hearth, while "away from home" was a
  literal 6,000 mm. A unit that retreated successfully arrived *outside* the circle that defines home, so
  it qualified to retreat again, forever. Nothing heals these units — only `grove_guardian` and `war_troll`
  regenerate in `greybox.json`, and neither playable faction sets `HomeHealingPerSecond` — so the health
  half of the test never stops being true either.
- The 400-tick cooldown was written **only when the command was accepted**. `NearKnownPoint` screens
  candidates against owned building footprints and known resources, but not against
  `navigation.CanOccupy`, which is what `MovementSystem.Submit` actually enforces. Once a base is built up,
  a large unit's retreat point routinely fails that clearance test — and a refusal that does not throttle is
  a refusal repeated on every think for the rest of the match.

Measured on the four natural AI matches, worst case:

| | before | after |
| --- | --- | --- |
| Orders refused | 2,426 of 5,529 (44%) | 1 of 876 (0.1%) |
| Refusal reason | `DestinationBlocked` ×2,426 | `NoPath` ×1 |
| Conquest/aven result | player 1 loses with 0 units left | player 1 wins with 45 |
| Gather orders, Dominion | 144 | 162–170 |

The winner also stopped being decided by player slot — player 2 had won both Conquest cases and player 1
both Dominion cases — and started being decided by faction: aven wins both Conquest cases, serevin both
Dominion. The map was symmetric all along; the defect was not.

## Arrival tolerance

`MoveSquad` skips units that are already at the goal, but its tolerance was 1,500 mm while
`FormationPlanner` seats a large squad several thousand millimetres across. An arrived, idle squad was
therefore neither "close enough" nor "still moving", so the order was re-issued every think — and
`MovementSystem.Submit` calls `CombatSystem.CancelAttack` on every unit it re-plans. A squad holding a
contested beacon had its attacks cancelled repeatedly. `MoveSquad` now takes an `arrival` argument and
`DirectDominion` passes the beacon radius, because standing anywhere inside the ring holds the beacon.

## The naval realm (2026-10-02)

Only a match of the naval realm on a map with deep water runs the fleet code; every other match keeps the land plan,
and the six tick scenarios hash identically with it.

- **Dock and hull.** The dock comes after the first Muster Hall, at six workers; the AI trains the one hull its
  faction may sail (sloop, frigate or galleon).
- **Ships answer ships.** The fleet is `FleetTarget` warships (2/3/4 by difficulty) and one transport, plus one
  warship for each enemy hull seen in the last three minutes, up to `FleetTarget` more.
- **Escort.** While the loaded transport sails to its landing, the free warships sail to the water off that beach
  (`NearestWater` of the landing point) and fight only enemy hulls or what stands within ten metres of the beach; the
  escort ends with the landing. Enemy hulls in view still come first.
- **Landing party.** The transport fills its hold up to ten, so a galleon carries ten, a sloop six and a frigate five.
- **Beast Lodge.** A faction raises one only when the lodge trains something it may recruit. This reproduces every
  earlier decision for the creature factions, the mountain clans and the desert pair, and stops the English, the
  pirates and the navies paying for an empty lodge.

Measured on Hard (`tools/Verify-SimulationTicks.ps1 --scenario naval-pirates-english,naval-english-spanish,naval-spanish-pirates`):
the three pairings end in Conquest at 9.5, 7.8 and 11.9 minutes, and repeated runs hash identically. Escorts formed in
every pairing. Across three difficulties and both seat orders (18 matches) every match ended in Conquest within 20
minutes; the pirates won 7 of 12 against the navies and the navies split 3–3. The land armies still decide Conquest;
the fleets decide the coast. See [NAVAL_SLICE.md](../design/NAVAL_SLICE.md).

## Known, deliberate, not defects

- `workerDestinations` is written before the gather is sent and cleared on success. This reads like an
  inverted memo but is a *pending intent*: keep aiming this worker at the chosen node across thinks until
  an order lands. Measurement shows no gather refusals in ordinary play, so it was left alone.
- Production stopping while stock piles up is `WorkerTarget` + `ArmyCap` working, not a refusal problem.
- `Peak majority hold: 9600` is `ContinuousHoldTicks` from the map — it means the win condition was met.

## Beacon targeting: a plausible fix that measured as nothing

`DirectDominion` sorts the objectives by distance from home and only ever assigns two squads, so on paper
each side is stuck on the two beacons nearest its own base and a side that loses the central crossing can
never assemble two of three. Re-sorting them by what is actually still reachable — beacons you already
hold, then unclaimed ground, then beacons in enemy hands, nearest home inside each tier — was tried and
produced runs **identical to the previous ones**, down to the individual death counts. The ordering it
produces is the ordering distance already produced: the winner takes the crossing and its own terrace, and
the loser's own terrace stays unclaimed and nearest, so it was first in the list either way. The change was
reverted rather than kept as logic nothing exercises.

What the same report says the losing side actually does:

| Dominion, losing side | | winning side |
| --- | --- | --- |
| Units trained | **188** | 48 |
| Left standing at the end | 11 units / 6 workers | 49 units / 16 workers |

It is not going to the wrong beacon. It is pouring four times as much production into an army it then
loses, while the other side keeps a large economy and holds. That is a balance and strategy question about
`AssaultPopulation`, `RegroupBelow` and when the controller commits, not a defect to fix in the targeting.
