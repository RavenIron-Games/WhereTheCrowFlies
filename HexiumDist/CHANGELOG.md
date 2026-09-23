# Changelog

All notable changes to **WhereTheCrowFlies** will be documented in this file.

## [1.2.0] - 2026-09-22

### Added
- **`/title` command (chat) and `title` command (F5 console)**: pick which of your earned titles shows next to your
  name on servers running **TheRavensCall 1.7.0+**. `title` alone lists what you've earned and which one is active,
  `title <name>` (multi-word titles supported, e.g. `title Wolf Hunter`) sets it, `title clear` removes it. The
  server has the only say on which titles you've actually earned — this mod just sends the request and prints
  whatever TheRavensCall answers.
- **`RavensCall_EventReport_V2` Event Type 13 (`TitleRequest`)**: the wire packet `/title` sends — schema version 2,
  op `1` = list, `2` = set, `3` = clear. See `HANDOFF.md` §2.2 for the full layout.
- **`RavensCall_TitleReply_V1`**: a new server → client routed RPC, registered once per world session the same way
  TheRavensCall registers its own listeners. TheRavensCall sends the reply text directly to the requesting player;
  this mod prints it back into whichever window (chat box or F5 console) the command was typed in, prefixed
  `[WhereTheCrowFlies]`.
- **`ravenscall title <player> [<title>|clear]`**: the existing admin routing stub's description now mentions the
  matching admin subcommand TheRavensCall 1.7.0 adds — no wire change on this mod's side, the stub only ever routes
  the raw command text to the server.

### Compatibility
- Pairs with **TheRavensCall 1.7.0+** for `/title` to get an answer. On an older TheRavensCall, the request still
  sends (Event Type 13 is just another `RavensCall_EventReport_V2` payload) but nothing replies, so after 5 seconds
  this mod prints a one-time hint that title picking needs TheRavensCall 1.7.0 or newer. An older TheRavensCall with
  `LogCombatReports` enabled will additionally log an "unknown eventType 13" line server-side — harmless, just noise.
- Every other event type, and every server this mod already worked with, is unchanged.

## [1.1.3] - 2026-09-15

### Fixed
- **Harvests were credited to the zone owner, not the picker.** The pickable hook was a prefix on `Pickable.RPC_Pick`, which runs only on the client that owns the pickable's zone, so every berry another player picked in that zone was reported under the owner's name, and a second player picking an already-picked bush produced a phantom harvest. The hook is now a postfix on `Pickable.Interact`, which runs on the picking player's own client, and it skips a pickable that was already picked, plus any repeat of the same pickable within two seconds, because a non-owner's picked flag only updates when the owner's reply lands and holding Use re-runs the interaction every 0.2 s. The amount uses the same drop-scaling formula the owner uses; the rare skill-bonus yield is rolled inside the interaction on the picker's own client into a local a postfix cannot read, so it is not counted (the owner adds whatever value the picker sent, unvalidated).
- **Failed crafts were counted, multi-crafts were undercounted.** `InventoryGui.DoCrafting` returns early (max quality, missing requirements, inventory full, missing DLC, upgrader resource missing) with the recipe still selected, and it fires when the craft bar completes, so "inventory filled while the bar ran" is ordinary play; the old postfix reported all of those as crafts and sent the recipe amount, ignoring multi-craft and the station bonus. The item is now counted at its target quality before and after: zero difference means nothing was made, and the difference is the real amount. An upgrader that fails or breaks the item no longer reports an upgrade.
- **Absolute stats double counted every full-sync cycle.** The full snapshot never cleared the pending delta window, and both timers reset to zero instead of subtracting their interval, so they drifted and the snapshot landed inside a delta window; the next delta flush was then added on top of the snapshot on the server. Pending deltas now go out before every snapshot (the spawn backfill included), and both clocks subtract their interval.
- **`ravenscall season start | end` can now be typed from a client.** A routing stub registers the name flagged server-only and remote, so the game sends it to the server, which checks the admin list and runs TheRavensCall's real command (any TheRavensCall that registers the command, confirmed back to 1.2.2; the server-side flags added in 1.2.4 do not gate a routed command). Non-admins get "You are not admin" from the server.
- **Docs said 105 vanilla stats.** Valheim 1.0 has about 205; the handoff and README now say so.

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

