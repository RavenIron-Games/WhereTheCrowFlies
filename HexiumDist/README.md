<div align="center">

# 🐦‍⬛ Where The Crow Flies

![Valheim Mod](https://img.shields.io/badge/Valheim-Client_Companion_Mod-orange.svg)
[![Deployment](https://img.shields.io/badge/Install-Client_Only-blue.svg)]()
[![Framework](https://img.shields.io/badge/Requires-BepInEx-red.svg)]()
[![Publisher](https://img.shields.io/badge/RavenIron-Release-8B6F1F.svg)]()
[![Version](https://img.shields.io/badge/Version-1.1.3-lightgrey.svg)]()

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
- **Zero In-Game Overhead**: Runs completely in the background without UI or performance penalty.
- **Safe Everywhere**: Connects safely to vanilla servers or servers without `TheRavensCall` with silent no-op dispatch.

---

## 📦 Installation

1. Install **BepInEx for Valheim**.
2. Download and extract **`WhereTheCrowFlies-v1.1.3.zip`**.
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
```

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
