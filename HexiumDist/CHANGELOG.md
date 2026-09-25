# Changelog

All notable changes to **WhereTheCrowFlies** will be documented in this file.

## [1.3.0] - TBD

### Added
- **Titles on nameplates.** On a server running **TheRavensCall 1.8.0+**, the nameplate the game draws over another
  player (close by, and not while they crouch) gets a second line directly under their name: their active title,
  smaller than the name and in a cool blue-grey. It changes as soon as their active title does (they pick one, clear
  it, or earn their first), and when you join you get every online player's current title at once. You never see
  your own nameplate (the game draws none over your own character); other players see yours. The line is drawn by
  the looking player's game: players without WhereTheCrowFlies 1.3.0+ see no TheRavensCall title line.
- **`ShowNameplateTitles` config** (section `General`, default `true`): `false` hides the TheRavensCall title line.
- **Works alongside Ragnarok's Wrath.** Ragnarok's Wrath already adds its own world title, in gold, under the
  nameplate. It is set up so that both show, this mod's line directly under the name and Ragnarok's Wrath's line
  under it.
- **`RavensCall_TitleSync_V1`**: a new server → everybody routed RPC that this mod receives (schema 1: a count, then a
  player name and that player's active title per entry, `""` = no title). It is accepted only from the server, and
  every title is cleaned before it reaches a nameplate: no rich-text tags, no `$` localisation keys, no control
  characters, line or paragraph separators, or backslashes, 64 characters at most. See `HANDOFF.md` §2.3b.

### Fixed
- **Damage in multiplayer was mostly missing, and PvP damage was always zero.** A hit is worked out on the
  attacker's machine and applied on the machine that owns the target, but the damage report only counted a hit
  when both were the same machine. Playing alone that is always true; in a group, any creature another player
  "owned" (usually whoever reached the area first) dropped out of your damage dealt and taken, and damage between
  players never counted at all. Now your damage dealt is counted on your own machine for every hit you make, and
  your damage taken is counted on your own machine for every hit that lands on you.
- **Damage taken is now the damage that reached you**, after armor, resistances, blocking and the world's difficulty
  settings, and it includes burning and poison ticks. Hits you dodge, or that the game rejects (PvP off), no longer
  count. Damage dealt is still the hit's damage before the target's armor and resistances, which your machine
  can't see; hits on a target that is already dead, or on a player with PvP off, no longer count.
- **Damage blocked reported the damage that got through the block, not the damage it stopped.** A clean parry of a
  big hit read close to 0, and a weak block read high. It now reports what the block actually stopped, and a block
  that breaks (out of stamina or staggered) no longer counts as a block (unless the shield itself reduces
  that kind of damage; that reduction still counts as blocked).
- **A boss summon could be counted more than once.** Pressing the altar again while the boss was still on its way
  (when another player's game was running the altar's area), or a summon that failed for lack of room, each
  counted as a summon. A summon is now counted once, when the boss spawn has really started.

### Changed
- **The DLL no longer carries the build machine's folder path.** Every build through 1.2.0 embedded the absolute
  path of its debug-symbols file, including the Windows user folder it was built in. The build now maps its source
  folder to a neutral `/_/` path, so the DLL names no folder on the build machine, and a build of the same commit
  comes out byte-identical in any clone of the GitHub repository (Windows, Git's default line endings, same .NET SDK).
- **The `title` command's help text** no longer says your title never reaches the in-game nameplate: on
  TheRavensCall 1.8.0+ it shows under your name for other players running this mod.

### Compatibility
- Nothing this mod sends has changed: the same reports with the same fields, so no server update is needed for them.
  The one wire addition is the `RavensCall_TitleSync_V1` receiver above. Pairs with **TheRavensCall 1.8.0+** for
  titles on nameplates, **TheRavensCall 1.7.0+** for `/title` and the title panel, **TheRavensCall 1.2.2+** for
  everything else. An older TheRavensCall never sends the new packet, so no TheRavensCall title line shows.
- Titles are matched by character name, the way TheRavensCall keeps them: two characters with the same name show
  the same title.
- Damage dealt and taken will be higher than before in group play: that is the damage that was missing. Damage
  taken for the same fight will be lower than before, because it is now counted after armor and blocking.

## [1.2.0] - 2026-09-23

### Added
- **`/title` command (chat) and `title` command (F5 console)**: pick which of your earned titles the server shows
  with your name in its Discord narration, Chronicle log and web dashboard on servers running **TheRavensCall
  1.7.0+** — nothing changes on your in-game nameplate. `title` alone lists what you've earned and which one is
  active, `title <name>` (multi-word titles supported, e.g. `title Wolf Hunter`) sets it, `title clear` shows no
  title (and on TheRavensCall 1.7.0 it stays that way: titles you earn later are listed, not shown, until you
  pick one). The server has the only say on which
  titles you've actually earned — this mod just sends the request and prints whatever TheRavensCall answers.
- **`RavensCall_EventReport_V2` Event Type 13 (`TitleRequest`)**: the wire packet `/title` sends — schema version 2,
  op `1` = list, `2` = set, `3` = clear. See `HANDOFF.md` §2.2 for the full layout.
- **`RavensCall_TitleReply_V1`**: a new server → client routed RPC, registered once per world session the same way
  TheRavensCall registers its own listeners. TheRavensCall sends the reply text directly to the requesting player;
  this mod prints it back into whichever window (chat box or F5 console) the command was typed in, prefixed
  `[WhereTheCrowFlies]`. The reply now also carries the requesting player's full earned-title list and active
  title, so the title panel below is always fresh after any op with no second round trip.
- **The title panel (`/titles` chat, `titles` F5 console, or the new `TitlePanelKey` config)**: a small window, on
  the family's shared gilt-frame theme (vendored from ValkyriesCargo, MIT, by Wubarrk), listing every title
  you've earned as a button — click one to set it, "No title" to clear it, Esc or Close to leave. It asks the
  server on open and refreshes after every click over the same wire the `title` command uses. While it's open
  your character holds still — movement, attacks, the map and chat are paused, the same way the game's own
  sign-text dialog pauses them.
- **`ravenscall title <player> [<title>|clear]`**: the existing admin routing stub's description now mentions the
  matching admin subcommand TheRavensCall 1.7.0 adds — no wire change on this mod's side, the stub only ever routes
  the raw command text to the server.

### Compatibility
- Pairs with **TheRavensCall 1.7.0+** for `/title` to get an answer, and needs `AcceptClientReports` on (it disables
  the player `/title` path the same way it disables every other client report; the admin's `ravenscall title` still
  works). On an older TheRavensCall, or a 1.7.0+ server with `AcceptClientReports=false`, the request still sends
  (Event Type 13 is just another `RavensCall_EventReport_V2` payload) but nothing replies, so after 5 seconds this
  mod prints a one-time hint naming both possible causes. An older TheRavensCall with `LogCombatReports` enabled will
  additionally log an "unknown eventType 13" line server-side — harmless, just noise.
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

