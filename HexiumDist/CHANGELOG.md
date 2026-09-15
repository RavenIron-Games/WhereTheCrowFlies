# Changelog

All notable changes to **WhereTheCrowFlies** will be documented in this file.

## [1.1.2] - 2026-09-15

### Fixed
- **Player deaths were never reported.** The death hook was a Harmony patch on `Character.OnDeath`, but `Player.OnDeath` is a full override that never calls base, so the patch only ever ran for creatures and `SendDeath` was dead code from the first release. Death narratives, `death_history`, PvP killer attribution and the Discord death post have all been silent on every server. Player deaths now have their own `Player.OnDeath` patch, which runs on the victim's own client only (the method itself returns early for every non-owner). Creature kills were never affected. No wire change: the V1 type-2 death packet is the one every TheRavensCall since 1.1.0 already credits; the V2 `Death` packet is additionally understood from TheRavensCall 1.2.0, which first registered the V2 receiver. **Pairs with any TheRavensCall 1.1.0 or newer**; 1.2.3 additionally binds every self-report to the sending peer, which a 1.1.2 client passes unchanged.

## [1.1.1] - 2026-09-10

### Fixed
- **Valheim 1.0.7 compatibility.** 1.0 turned `PlayerProfile.m_playerStats` from a single `PlayerStats` into an array of ten (one per achievement difficulty), so the 1.1.0 build failed to load at all on 1.0.7 (`MissingFieldException` at the first stat snapshot). The snapshot now reads bucket 0, the unconditional one that `IncrementStat` always writes and the exact successor of the pre-1.0 object; the per-difficulty buckets are skipped on purpose, because they undercount a session in which cheats were used.
- Rebuilt against the 1.0.7 client assemblies on BepInEx 5.4.2350; every game reference and all 15 Harmony targets verified by exact signature on both the client and the dedicated-server build. No wire-protocol change: `StatSnapshot` still sends `(short id, float value)` pairs, there are simply 205 of them now instead of 105 (1.0 added a hundred counters). **Pair with TheRavensCall 1.2.2 or newer** - older receivers cap a snapshot at 200 pairs and silently drop the last five.

### Changed
- Manifest dependency moved to `denikson-BepInExPack_Valheim-5.4.2350` (the Valheim 1.0 pack; older packs can take the chainloader down on 1.0's Unity 6).

## [1.1.0] - 2026-08-19

### Added
- **`StatSnapshot` (Event Type 11)**: Full absolute value of all 105 vanilla `PlayerStatType` counters, flushed on a configurable interval (`FullSyncIntervalSeconds`, default 5 minutes) and once immediately on spawn/join. Unlike `StatSync`'s deltas, this backfills a player's pre-existing totals automatically (no data earned before install, or lost to a dropped packet, is ever missing) — the receiver assigns rather than accumulates each value.
- **`SkillSnapshot` (Event Type 12)**: Full skill state (level 0-100 and progress toward the next level) for every `Skills.SkillType` the player has raised at least once, on the same interval/on-spawn cadence. Skills were previously not reported at all.
- **Immediate on-spawn backfill**: both new snapshot types also fire once the instant the local character spawns, instead of only waiting for the next scheduled resync, so a player who connects and disconnects quickly still backfills.
- **`FullSyncIntervalSeconds` config option** (default `300`, floor-clamped to `10`): the two new snapshot types run on their own clock, deliberately decoupled from the pre-existing 10-second combat/damage-batch cadence — a full snapshot is idempotent and not latency-sensitive, so it doesn't need to match that cadence.

### Fixed
- Closed the gap where a player's true lifetime stats (as shown on their own client) could permanently diverge from what the server ever received, since `StatSync` only reported deltas observed after this mod was installed.

## [1.0.1] - 2026-08-17

### Added & Enhanced
- **Full 360° Telemetry Coverage (`RavensCall_EventReport_V2`)**:
  - **Combat & Defense**: Added tracking for star levels (`m_level`), boss status, tamed flag, primary weapon/damage type, shield/weapon blocks, perfect timed parries, and damage mitigated. Segregated PvE vs PvP damage streams.
  - **Player Deaths**: Enhanced death cause resolution with 15+ environmental and combat vectors (PvP killer name, boss names, drowning, fall, lightning, smoke, burning, freezing, poison, tree crush).
  - **Crafting & Upgrades**: Intercepts item creation, quality upgrades, quantities, and crafting station origins.
  - **Building & Demolitions**: Records piece placements and structure demolitions with piece categories and coordinates.
  - **Harvesting & Farming**: Captures foraged pickables (berries, mushrooms, thistle), crop harvests, beehives, and sap collectors.
  - **Consumables & Survival**: Tracks food consumption with health/stamina/eitr values and potion/mead ingestions.
  - **World Events & Rituals**: Relays boss altar offerings and summons, portal traversals with destination tags, and Forsaken power activations.
  - **100% Vanilla PlayerStat Delta Sync**: Intercepts and accumulates all 105 vanilla `PlayerStatType` engine counters with compact 10-second delta flushes.
- **Backward Compatibility**: Dual-transmits `RavensCall_CombatReport_V1` alongside V2 reports to ensure zero regression on legacy servers.
- **Metadata & Links**: Updated project website URL to `https://ravenirongames.rglmobile1.workers.dev/`.

## [1.0.0] - 2026-08-16

### Initial Release
- **Lightweight Combat & Event Reporting**: Captures client-owned combat interactions (creature kills, player deaths, damage dealt/taken, fish catches) and sends them to dedicated servers running `TheRavensCall`.
- **Accurate Death Causes**: Resolves rich, client-side death causes (environmental damage, boss encounters, drowning, falls) for accurate server-side Chronicle and Discord narration.
- **Batched Damage Reporting**: Efficient 10-second / 500-damage accumulator that aggregates damage dealt and taken to prevent network congestion during high-intensity combat and raids.
- **Zero Configuration & Seamless Interop**: Requires no player configuration and safely no-ops if connected to servers without `TheRavensCall`.

