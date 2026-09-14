# The BarrkBOT contract — what your telemetry turns into

This mod does not talk to BarrkBOT. It talks to **TheRavensCall**, which folds
your payloads into `BepInEx/config/TheRavensCall/BarrkBOT_data1.json`, which
BarrkBOT reads from the local filesystem (moved off G-Portal/FTP to TheWall on
22 Aug 2026) and reads back to people in Discord.

So the chain is: **your RPC field → TheRavensCall's JSON key → an answer a member
reads.** A field renamed at this end silently zeroes a feature two programs
away, and nothing in between will error.

See `/home/rohan/WubarrkCODING/TheRavensCall/BARRKBOT_CONTRACT.md` for the file
side of the same contract, and the 20 Aug incident that prompted both.

## What BarrkBOT does with what you send

As of v1.1.0 he reads and reports:

- **`vanilla_stats`** — the character's own lifetime counters. He uses
  **`EnemyKills` and `Deaths` as the authoritative lifetime figures**, in
  preference to what the server observed, because the server undercounts badly
  (Thorium: 13 observed against 136 real). He surfaces every non-zero stat,
  exempting `Cheats` (console use — legitimate admin activity that reads as an
  accusation in a public channel) and `WorldLoads` (meaningless engine noise).
- **`skill_levels` / `skill_progress`** — reported as a ranked list with progress
  to the next level.
- **The activity counters** — `builds_placed`, `items_crafted`,
  `resources_harvested`, `consumables_eaten`, `bosses_summoned`,
  `guardian_powers_used` and the rest.
- **`blocks_session` / `parries_session` / `damage_blocked_session`** — labelled
  as session figures that reset on logout.

## The thing to be careful about

**Only players running this mod report anything**, and BarrkBOT has to say that
correctly. On 20 Aug exactly one of 35 tracked players had any of it. If he
reported "0 builds placed" for the other 34 he would be lying about people who
had simply not played yet — so an unreported record is returned as *unknown*,
never as zero.

That distinction depends on the fields being **absent or empty** when there is no
report, not present-and-zero. If a future version starts emitting zeroed
structures for players who never reported, BarrkBOT will read that as fact and
start telling members that people who have never logged in since the update have
built nothing and have no skills. **Absent must stay absent.**

## If you change the wire protocol

1. **Bump `schemaVersion`** rather than reshaping a payload in place — the
   handoff already specifies this, and it is the reason a legacy server survives
   a client upgrade.
2. **Note it here, in `HANDOFF.md`, and in TheRavensCall's contract file**, since
   a field only reaches BarrkBOT if that mod also folds it into the export.
3. **Resolving modded skill names would be a real win.** They arrive keyed by
   numeric ID (`"3968": 10`, `"26767": 42.9`) because the server cannot resolve
   them, and BarrkBOT currently drops them rather than saying "your 3968 skill is
   level 10" out loud. If either mod can map them, they become answerable.

## Checking it end to end

With the client mod running, play for a minute, then in Discord:

```
ADMIN COMMAND: force a re-ingest from ftp
ADMIN COMMAND: tell me about the player <your character name>
```

The first names the exact files read and how old they are; the second should come
back with skills and lifetime counters. If the second says the detail is "not
recorded yet" while you are demonstrably playing, the break is between this mod
and TheRavensCall, not in BarrkBOT.
