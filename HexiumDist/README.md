<div align="center">

# 🐦‍⬛ Where The Crow Flies

![Valheim Mod](https://img.shields.io/badge/Valheim-Client_Companion_Mod-orange.svg)
[![Deployment](https://img.shields.io/badge/Install-Client_Only-blue.svg)]()
[![Framework](https://img.shields.io/badge/Requires-BepInEx-red.svg)]()
[![Publisher](https://img.shields.io/badge/RavenIron-Release-8B6F1F.svg)]()
[![Version](https://img.shields.io/badge/Version-1.2.0-lightgrey.svg)]()

**RavenIron's client companion mod for The Raven's Call: effortlessly witnesses and relays player combat, defense, kills, deaths, crafting, building, foraging, consumption, rituals, and 100% of vanilla stats to the hall's ledger.**

</div>

---

## 🐦‍⬛ What Is WhereTheCrowFlies?

**WhereTheCrowFlies** is an opt-in client companion mod designed to pair with **TheRavensCall** (the server-side admin and chronicle mod).

In Valheim's peer-to-peer multiplayer simulation model, combat calculations, creature deaths, damage, parries, building, crafting, foraging, and fishing are simulated locally on the client that owns the local zone or character. **WhereTheCrowFlies** acts as the client-side scout—observing all gameplay events with zero client lag and securely reporting them back to dedicated servers running **TheRavensCall** via routed RPC.

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
- **Out Of The Way**: No HUD changes and no performance penalty during normal play — the only interface is the optional title panel you open yourself with `/titles`.
- **Safe Everywhere**: Connects safely to vanilla servers or servers without `TheRavensCall` with silent no-op dispatch.

---

## 📦 Installation

1. Install **BepInEx for Valheim**.
2. Download and extract **`WhereTheCrowFlies-v1.2.0.zip`**.
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
```

---

## 🏷️ Picking your title

On a server running **TheRavensCall 1.7.0+**, choose which of your earned titles the server shows with your name
in its Discord narration, Chronicle log and web dashboard — nothing changes on your in-game nameplate. Two ways
to do it:

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

**Vendored, and not ours:** the panel's gilt frame is Wubarrk's VikingOS shared source (`Libs/SharedUI/GiltFrameTheme.cs`,
MIT), compiled in unchanged.

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
