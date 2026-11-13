<!-- README schema v2 -->
# 🐦‍⬛ Where The Crow Flies

![WHERE THE CROW FLIES](https://img.shields.io/badge/WHERE_THE_CROW_FLIES-EVERY_DEED_CARRIED_TO_THE_HALL-c9a227?style=for-the-badge&labelColor=1b1b1b)
![VERSION](https://img.shields.io/badge/VERSION-1.2.1-8b1e1e?style=for-the-badge&labelColor=1b1b1b)
![CLIENT SIDE](https://img.shields.io/badge/CLIENT--SIDE-PAIRS_WITH_THE_RAVEN%27S_CALL-1f4e79?style=for-the-badge&labelColor=1b1b1b)

> *Two ravens fly out over Midgard every morning and come home every night to whisper in the All-Father's ear. They cannot be everywhere. So a crow keeps to each Viking's shoulder, watches what the ravens miss, and flies it back to the hall before the mead is cold.*

### Your kills, your deaths, your crafts and your parries, carried to the hall's ledger.

**Where The Crow Flies** is Raven Iron's client companion to **TheRavensCall**, the server-side admin and chronicle mod. It is opt-in, and it goes on each player's game.

Valheim works out combat, creature deaths, damage, parries, building, crafting, foraging and fishing on the player's own machine, the one that owns that piece of the world, so the server never sees most of it. Where The Crow Flies is the scout: it watches those events where they happen and reports them to a server running TheRavensCall, so its Chronicle, Discord narration and web dashboard know what really happened. On any other server it keeps quiet and changes nothing.

---

![NEW IN 1.2.1](https://img.shields.io/badge/NEW_IN_1.2.1-VALHEIM_1.0.21-8b1e1e?style=for-the-badge&labelColor=1b1b1b)

**This is the Valheim 1.0.21 build of Where The Crow Flies.**

| You | It |
|---|---|
| **Play on Valheim 1.0.21** | Where The Crow Flies runs on it and reports exactly what it reported before |
| **Join a server** | update your game together with the server: Valheim 1.0.21 is network version 41 |

Nothing else is new in this version.

---

![BEFORE YOU INSTALL](https://img.shields.io/badge/BEFORE_YOU_INSTALL-READ_THIS-b8860b?style=for-the-badge&labelColor=1b1b1b)

| | If… | Then… |
|---|---|---|
| 🐦‍⬛ | your server runs **TheRavensCall** | install this on your own game. It reports what you do to that server, which is what it is for. Set `EnableReporting = false` if you would rather it did not. |
| 🌍 | your server does **not** run TheRavensCall | nothing is reported and nothing breaks. |
| 🛡️ | your server uses **AzuAntiCheat** or a mod whitelist | the server admin adds `WhereTheCrowFlies.dll` to the allowed plugins. |
| 🏷️ | the **title panel** is open | your character holds still: movement, attacks, the map and chat are paused. Close it (Esc) before a fight. |

---

![I](https://img.shields.io/badge/I-WHAT_THE_CROW_SEES-c9a227?style=for-the-badge&labelColor=1b1b1b)

**Everything you do that the server cannot see for itself.**

| | What it reports |
|---|---|
| ⚔️ **Kills** | creatures killed by a player, credited to the killer, with the creature's stars, boss and tamed flags, the killing blow's damage type, the biome and where it fell |
| 💀 **Deaths** | your own deaths with a cause: the boss or creature that did it, the player who killed you, drowning, a fall, burning, freezing, poison, or spirit or lightning damage |
| 🛡️ **Combat and defence** | damage dealt and taken, with creatures and players kept apart, hits, shield and weapon blocks, perfect parries and damage blocked |
| 🎣 **Fishing** | each fish you land, with its quality, weight and biome |
| 🔨 **Crafting** | items crafted and upgraded, how many, and at which station; repairs too |
| 🏠 **Building** | pieces placed and torn down, with their category and position |
| 🌿 **Foraging and farming** | berries and mushrooms picked, crops harvested |
| 🍖 **Food and meads** | food eaten, with its health, stamina and eitr, and potions and meads drunk |
| 🗿 **Rituals and travel** | boss altar summons, portal trips with the portal's tag, and Forsaken powers used |

<details>
<summary>📖 <b>Read the saga</b> — why a crow on every shoulder</summary>

In Valheim the machine that owns a zone or a character decides what happens there. A kill, a parry or a picked berry is settled on a player's own game and the server only sees the result, if that. TheRavensCall can only chronicle what it hears about, so Where The Crow Flies listens where the event happens and sends a short report to the server over Valheim's own routed messages. It credits the right player: a berry you pick is reported by your own game, under your name, not by whoever owns that patch of ground.

Damage, blocks and parries come thick and fast in a fight, so they are added up and sent in small batches, every ten seconds or sooner in a heavy fight, instead of one message per swing.

</details>

---

![II](https://img.shields.io/badge/II-THE_LEDGER-2e5e3a?style=for-the-badge&labelColor=1b1b1b)

**Every number Valheim keeps about you, on the server too.**

| | |
|---|---|
| 📊 **Every vanilla stat** | every one of Valheim's own player counters (distance travelled, jumps, arrows fired, trees chopped, mining hits, time in base, sleep and the rest), the changes sent every ten seconds |
| 📚 **Full backfill** | a complete snapshot of every stat, plus every skill you have raised (level and progress), the moment you spawn and every five minutes after |
| 👀 **Out of the way** | no HUD changes. The only window is the title panel, and only when you open it. |

<details>
<summary>📖 <b>Read the saga</b> — why a snapshot as well as the changes</summary>

The changes alone would only ever count what you did after installing the mod, and a lost message would be lost for good. The snapshot carries your true totals, the ones your own game shows, so the server's ledger starts complete and heals itself. Any changes still waiting are sent before each snapshot, so nothing is counted twice. How often the snapshot goes out is `FullSyncIntervalSeconds`; it always goes out when you spawn, whatever that is set to.

</details>

---

![III](https://img.shields.io/badge/III-YOUR_TITLE-5b2c83?style=for-the-badge&labelColor=1b1b1b)

**Choose which of your earned titles the hall shows with your name.**

On a server running TheRavensCall, the title you pick is shown with your name in its Discord narration, Chronicle log and web dashboard. Nothing changes on your in-game nameplate.

| You | It |
|---|---|
| **`/titles`** in chat, **`titles`** in the console (F5), or your `TitlePanelKey` | opens the title panel: every title you have earned, as a button |
| **Click a title** | sets it |
| **No title** | clears it |
| **Esc** or **Close** | closes the panel |
| **`/title`** | lists what you have earned and which one is active |
| **`/title Wolf Hunter`** | sets that title; titles of several words work |
| **`/title clear`** | shows no title, and it stays that way: titles you earn later are listed, not shown, until you pick one |

<details>
<summary>📖 <b>Read the saga</b> — who decides what you have earned</summary>

The server does. This mod only sends your request and shows whatever TheRavensCall answers, in the chat box or console you typed in, or in the panel. The panel asks the server when it opens and refreshes after every click, so it always shows the true state. The console commands are the same as the chat ones, without the leading `/`.

If nothing answers within a few seconds, you get a one-time hint naming the two likely causes: a TheRavensCall too old for titles, or a server that has turned off `AcceptClientReports`. Everything else in this mod keeps working either way.

</details>

---

![THE TOOLBOX](https://img.shields.io/badge/THE_TOOLBOX-QUICK_START_%C2%B7_COMMANDS_%C2%B7_HOW--TO-1b1b1b?style=for-the-badge&labelColor=8b1e1e)

**First ten minutes:** put `WhereTheCrowFlies.dll` in `BepInEx/plugins/` → join a server that runs TheRavensCall → play as you always do → type `/titles` to pick your title.

| Chat | Console (F5) | |
|---|---|---|
| `/titles` | `titles` | open the title panel (or press your `TitlePanelKey`) |
| `/title` | `title` | list your titles |
| `/title <name>` | `title <name>` | set one |
| `/title clear` | `title clear` | show no title |
| | `ravenscall …` | **admins:** TheRavensCall's own server command, typed from your console. It runs on the server; the output is in the server console. |

<details>
<summary>🛠️ <b>Install</b></summary>

1. Install **BepInExPack Valheim**.
2. Put `WhereTheCrowFlies.dll` in `BepInEx/plugins/`.
3. Start the game once. The settings appear in `BepInEx/config/com.raveniron.wherethecrowflies.cfg`.

</details>

<details>
<summary>🛠️ <b>Open the title panel with a key</b></summary>

Set `TitlePanelKey` in the config to the key you want. It is `None` by default, which means the commands only.

</details>

<details>
<summary>🛠️ <b>For server admins: an anti-cheat or a whitelist</b></summary>

If your server uses **AzuAntiCheat** or a client mod whitelist, add `WhereTheCrowFlies.dll` to the server's allowed plugins list.

</details>

---

![MULTIPLAYER](https://img.shields.io/badge/MULTIPLAYER-THE_CROW_FLIES_HOME-1f4e79?style=for-the-badge&labelColor=1b1b1b)

| Works with any server | Needs TheRavensCall on the server | Needs Where The Crow Flies on each player |
|---|---|---|
| joins vanilla servers and servers without TheRavensCall: its reports go unheard there, and nothing breaks | everything this mod reports · titles · the `ravenscall` admin command | each player's own reports: deaths, combat, crafts, building, harvests, food, stats, skills and titles. A player without it sends none of their own. |

**Older servers.** It still sends the older combat report alongside the full one, so an older TheRavensCall keeps hearing what it understands. A server can turn all player reports off with TheRavensCall's `AcceptClientReports`.

---

![COMPATIBILITY](https://img.shields.io/badge/COMPATIBILITY-PLAYS_WELL_WITH_OTHERS-2e5e3a?style=for-the-badge&labelColor=1b1b1b)

| Mod | |
|---|---|
| **TheRavensCall** (Raven Iron) | its partner: the server mod that receives the reports and answers title requests |
| **AzuAntiCheat · mod whitelists** | add `WhereTheCrowFlies.dll` to the server's allowed plugins |
| **Mods with their own `title` or `titles` command** | Where The Crow Flies leaves theirs alone and says so in the log. `TitlePanelKey` still opens the panel. |

---

![SETTINGS](https://img.shields.io/badge/SETTINGS-BEPINEX_CONFIG-b8860b?style=for-the-badge&labelColor=1b1b1b)

All in `BepInEx/config/com.raveniron.wherethecrowflies.cfg`. A client-side mod: every setting is your own.

<details>
<summary>🖥️ <b>Local</b> — yours alone</summary>

| Section | Settings |
|---|---|
| **General** | `EnableReporting` (send reports to servers running TheRavensCall; on by default) · `FullSyncIntervalSeconds` (seconds between full stat and skill snapshots; 300 by default) · `TitlePanelKey` (a key that opens the title panel; `None` by default) |

</details>

---

![CREDITS](https://img.shields.io/badge/CREDITS-THANK_YOU-1b1b1b?style=for-the-badge&labelColor=c9a227)

**Needs:** [BepInExPack Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/) `5.4.2360` · **pairs with** TheRavensCall on the server (`1.7.0` or newer for titles).

*Raven Iron's client companion mod for TheRavensCall.* **Vendored, and not ours:** the title panel's gilt frame is Wubarrk's VikingOS shared source (MIT), compiled in unchanged.

**Support Raven Iron.** Every Raven Iron mod is free, and stays free: all of it, always. Nothing is held back for patrons, and nothing ever will be. If you'd like to help cover server hosting and test hardware:

- **Website** — <https://ravenirongames.com/>
- **Patreon** — <https://www.patreon.com/cw/RavenIronGames>
- **Discord** — <https://discord.gg/AGKDEurAVa> — a channel per mod, and where the testing happens
