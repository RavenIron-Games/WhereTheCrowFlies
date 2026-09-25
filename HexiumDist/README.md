<div align="center">

# 🐦‍⬛ Where The Crow Flies

![Valheim Mod](https://img.shields.io/badge/Valheim-Client_Companion_Mod-orange.svg)
[![Deployment](https://img.shields.io/badge/Install-Client_Only-blue.svg)]()
[![Framework](https://img.shields.io/badge/Requires-BepInEx-red.svg)]()
[![Publisher](https://img.shields.io/badge/RavenIron-Release-8B6F1F.svg)]()
[![Version](https://img.shields.io/badge/Version-1.3.0-lightgrey.svg)]()

**RavenIron's client companion mod for The Raven's Call: effortlessly witnesses and relays player combat, defense, kills, deaths, crafting, building, foraging, consumption, rituals, and 100% of vanilla stats to the hall's ledger.**

</div>

---

## 🐦‍⬛ What Is WhereTheCrowFlies?

**WhereTheCrowFlies** is an opt-in client companion mod designed to pair with **TheRavensCall** (the server-side admin and chronicle mod).

In Valheim's peer-to-peer multiplayer simulation model, combat calculations, creature deaths, damage, parries, building, crafting, foraging, and fishing are simulated locally on the client that owns the local zone or character. **WhereTheCrowFlies** acts as the client-side scout—observing all gameplay events with zero client lag and securely reporting them back to dedicated servers running **TheRavensCall** via routed RPC.

**Hosting the world yourself?** On a listen server, or a single-player world, the game you play in is also the server. Install **TheRavensCall 1.8.0+** in that same game and your own play counts too, and `/title` works for you as the host. TheRavensCall then counts the damage you deal with its own hook and takes the damage you take from this mod, so each hit counts once. Without TheRavensCall there, nothing is recorded for you and nothing breaks.

---

## ⚙️ Features

- **Full-Coverage Kill & Death Tracking**: Reports creature kills with star level, boss status, tamed flag, weapon damage type, biome, and coordinates. Reports player deaths with 15+ rich computed causes (bosses, monsters, PvP opponents, drowning, falls, burning, freezing, poison, spirit and lightning damage).
- **Combat & Defense Telemetry**: Segregates PvE vs PvP damage dealt/taken, tracks hit counts, shields/weapon blocks, perfect timed parries, and total mitigated damage.
- **Smart 10-Second Batching**: Aggregates continuous high-frequency telemetry (damage, defense, stat deltas) into compact 10-second batches, eliminating network congestion during massive raids.
- **Crafting, Upgrading & Repairs**: Captures crafted items, quality upgrades, quantities, and crafting station origins.
- **Building & Demolitions**: Records piece placements and structure demolitions with piece categories and coordinates.
- **Harvesting, Foraging & Agriculture**: Relays berry/mushroom foraging and farm crop harvests.
- **Survival & Consumables**: Tracks food items eaten, health/stamina/eitr gains, and potion/mead consumption.
- **World Rituals & Exploration**: Captures boss altar offerings/summons, portal traversals with tags, and Forsaken power activations.
- **100% Vanilla PlayerStat Delta Sync**: Intercepts every vanilla `PlayerStatType` counter (~205 on Valheim 1.0) (distances traveled, jumps, arrows fired, skeleton summons, time in base, sleep, tree chops, mining hits, etc.) and synchronizes deltas to the server.
- **Full Stat & Skill Backfill**: Broadcasts a full absolute snapshot of all ~205 stats plus every raised `Skills.SkillType` (level and progress) on spawn and on a configurable interval (default 5 minutes) — so pre-existing totals a player already earned show up immediately, not just future deltas.
- **Titles On Nameplates**: On a server running TheRavensCall 1.8.0+, other players' active titles show on a line under their name (`ShowNameplateTitles`, on by default).
- **Out Of The Way**: No HUD of its own and no performance penalty during normal play — the only things it shows are the optional title panel you open yourself with `/titles` and, on a TheRavensCall 1.8.0+ server, the title line under other players' names.
- **Safe Everywhere**: Connects safely to vanilla servers or servers without `TheRavensCall` with silent no-op dispatch.

---

## 📦 Installation

1. Install **BepInEx for Valheim**.
2. Download and extract **`WhereTheCrowFlies-v1.3.0.zip`**.
3. Place `WhereTheCrowFlies.dll` into your `Valheim/BepInEx/plugins/` directory.

---

## 🔧 Configuration

A configuration file is generated at `BepInEx/config/com.raveniron.wherethecrowflies.cfg` upon first launch:

```ini
[General]

## Send owner-side combat reports, world telemetry, crafting, building, harvesting, consumption, and player stats to servers running TheRavensCall.
# Setting type: Boolean
# Default value: true
EnableReporting = true

## How often (in seconds) to broadcast a full absolute snapshot of all vanilla player stats and skills (StatSnapshot/SkillSnapshot). These are idempotent full-state resyncs, not time-sensitive deltas, so this can be turned up if 10-second combat/damage batching feels like enough network chatter on its own — a full snapshot also always fires immediately on spawn regardless of this setting.
# Setting type: Single
# Default value: 300
FullSyncIntervalSeconds = 300

## Key that opens the title panel (the same panel the /titles command opens). None = command only. Read through the game's own input layer (ZInput), so it works with Valheim 1.0's input system.
# Setting type: KeyCode
# Default value: None
TitlePanelKey = None

## Show other players' active TheRavensCall titles on a line under their name on their nameplate. Needs TheRavensCall 1.8.0 or newer on the server. Off = no TheRavensCall title line (other mods' lines, like Ragnarok's Wrath's, are not affected).
# Setting type: Boolean
# Default value: true
ShowNameplateTitles = true
```

---

## 🏷️ Picking your title

On a server running **TheRavensCall 1.7.0+**, choose which of your earned titles the server shows with your name
in its Discord narration, Chronicle log and web dashboard — and, on **TheRavensCall 1.8.0+**, under your name on
other players' screens (see "Titles on nameplates" below). Two ways to do it:

- **The title panel**: `/titles` (chat) or `titles` (F5 console) opens a small window listing everything you've
  earned. Click one to set it, "No title" to clear it, Esc or Close to leave. It asks the server on open and
  refreshes after every click, so it always shows the true state. You can also open it with a key of your own
  choosing — set `TitlePanelKey` in the config above (`None` by default, command only). While it's open your
  character holds still — movement, attacks, the map and chat are paused, the same way the game's own sign-text
  dialog pauses them — so close it (Esc) before a fight.
- **The `title` command**, with no window: `/title` (list what you've earned), `/title Wolf Hunter` (set —
  multi-word titles work), `/title clear` (shows no title, and stays that way: titles you earn later are listed,
  not shown, until you pick one). F5 console: same commands without the leading `/`.

The server decides which titles you've actually earned; this mod only sends the request and prints (or shows in
the panel) whatever TheRavensCall answers. Against an older TheRavensCall, a server with `AcceptClientReports`
turned off, or no TheRavensCall at all, nothing answers and you'll see a one-time hint after a few seconds that
names both possible causes — everything else in this mod keeps working normally.

On a world you host yourself (a listen server, or single player), both work too when your game also runs
**TheRavensCall 1.8.0+**; without it, nothing answers and the hint after a few seconds says what the host needs.
While your character is not in the world (after a death, until the respawn), `/title` says so instead of asking.

**Vendored, and not ours:** the panel's gilt frame is Wubarrk's VikingOS shared source (`Libs/SharedUI/GiltFrameTheme.cs`,
MIT), compiled in unchanged.

---

## 📛 Titles on nameplates

On a server running **TheRavensCall 1.8.0+**, every other player's active title shows on its own line directly
under their name, on the nameplate the game draws over them (when they're close by; the game hides it while they
crouch). The line is smaller than the name and a cool blue-grey. It changes as soon as their active title does (they
pick one, clear it, or earn their first), and when you join you get every online player's current title at once.

- **You never see your own nameplate**: the game draws none over your own character. To check yours, ask a second
  player running this mod to look at you.
- **The looking player needs WhereTheCrowFlies 1.3.0+.** The line is drawn by the viewer's game, so players without
  it see no TheRavensCall title line. On an older TheRavensCall, or none, nothing is sent and no TheRavensCall
  title line shows.
- **Ragnarok's Wrath**: its world title, in gold, already sits under the nameplate. It is set up so that both show,
  this mod's line directly under the name and Ragnarok's Wrath's line under it.
- **No title line instead**: set `ShowNameplateTitles = false` in the config above.
- Titles are matched by character name, the way TheRavensCall keeps them: two characters with the same name show
  the same title.

---

## 🛡️ Server Admin Note

If your server uses **AzuAntiCheat** or a client mod whitelist, add `WhereTheCrowFlies.dll` to your server's allowed plugins list.

---

## 💜 Support Raven Iron

Every Raven Iron mod is free, and stays free — all of it, always. Nothing is held
back for patrons, and nothing ever will be.

If you'd like to help cover server hosting and test hardware:

- **Website** — <https://ravenirongames.com>
- **Patreon** — <https://www.patreon.com/cw/RavenIronGames>
- **Discord** — <https://discord.gg/AGKDEurAVa> — a channel per mod, and where the
  testing happens
