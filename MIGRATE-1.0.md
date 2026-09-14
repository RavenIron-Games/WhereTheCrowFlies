# WhereTheCrowFlies — Valheim 1.0.7 migration

> **Verified 2026-09-11 on Valheim 1.0.12 (build 25253764 client / 25253791 server, network version 40) - WhereTheCrowFlies 1.1.1, no change.**
> The 1.1.1 DLL built on 2026-09-10 against the 1.0.7 refs (md5 `18352a20ea322062d9f5917bf1fbab9a`, identical in
> `bin/`, `HexiumDist/plugins/` and the `wtcf` test profile) was re-checked unmodified against 1.0.12. Nothing was
> rebuilt, bumped or re-packaged: `WhereTheCrowFlies-v1.1.1.zip` is still the release. assembly_utils,
> assembly_guiutils, gui_framework, assembly_postprocessing and Splatform are IL-identical 1.0.7 -> 1.0.12; the
> whole delta is in assembly_valheim (37 types, 55 body changes, 5 methods added, 1 removed, 6 fields added,
> 1 removed, 1 const changed). Ground truth used: `MigrationStation/diffs/{client,server}_assembly_valheim_1.0.7_to_1.0.12.{md,ildiff.txt}`
> and the decompile diffs `libs-Tools/GAME-SNAPSHOT-1.0.7-build25185596/DECOMPILED/` vs `libs-Tools/1.0/DECOMPILED/`.
>
> **Touchpoints (all 15 Harmony targets + every engine member the DLL references; no transpiler, no AccessTools /
> reflection strings, no console command, no `Version.*`, no ZDO item-data read/write, no cooking/smelting/fermenting,
> no landing / terrain / admin-list code - greps over `Plugin.cs` and `Patches/CombatReporter.cs` returned nothing for
> any of those).**
>
> | Where | Target / member | 1.0.12 delta | Verdict |
> |---|---|---|---|
> | `CombatReporter.cs:711` | `Character.OnDeath()` (prefix) | body-changed, 336 -> 336 IL tokens: the single `ldsfld PlayerProfile::s_bypassCheatChecks` became `call get_s_bypassCheatChecks()` (ildiff line 127, client and server identical); decompile of `Character.OnDeath` (1.0.7 line 3069 / 1.0.12 line 3076) is textually identical. Prefix reads `m_lastHit`, `GetLevel`, `IsBoss`, `IsTamed`, `IsOwner` - none changed. | unaffected |
> | `CombatReporter.cs:957` | `InventoryGui.DoCrafting(Player)` (postfix) | body-changed, 859 -> 859 tokens: same one-instruction `ldsfld` -> `call get_` swap (ildiff line 365); decompile (1.0.7 line 51545 / 1.0.12 line 51584) identical. Postfix reads `m_craftRecipe`, `m_craftUpgradeItem`, `GetCurrentCraftingStation()` - unchanged. `DoCrafting` calls `HaveRequirements(..., discover: false, ...)`, so the 1.0.12 `HaveRequirementItems` discover fix (upgrader mismatch) only changes the recipe list, never what this postfix sees. | unaffected |
> | `CombatReporter.cs:792` | `Character.Damage(HitData)` | not in the changed-member list; decompile identical. (`Character.ApplyDamage` changed by the same `ldsfld` -> `call` swap; not patched.) | unaffected |
> | `CombatReporter.cs:850` | `Humanoid.BlockAttack(HitData, Character)` | unchanged (1.0.7 2873 / 1.0.12 2873, one override in Player, identical) | unaffected |
> | `CombatReporter.cs:879` | `FishingFloat.Catch(Fish, Character)` | unchanged (127630 / 127720, identical); postfix reads the in-memory `ItemDrop.m_itemData` (`m_quality`, `GetWeight()`), never the ZDO, so the `ItemDrop.SaveToZDO` guard flip (`index < 0` -> `index > -1`, item-stand slots) does not reach it | unaffected |
> | `CombatReporter.cs:912` | `Player.PlacePiece(Piece, Vector3, Quaternion, bool doAttack = true, bool cheated = false)` | unchanged (12497 / 12504, identical). `Player.TryPlacePiece` changed by the `ldsfld` -> `call` swap only (ildiff line 480); not patched | unaffected |
> | `CombatReporter.cs:933` | `Player.RemovePiece()` | unchanged (12359 / 12366) | unaffected |
> | `CombatReporter.cs:983` | `InventoryGui.OnRepairPressed()` | unchanged | unaffected |
> | `CombatReporter.cs:1008` | `Pickable.RPC_Pick(long, int)` | unchanged (71087 / 71137; the `(long)` overload also still exists, refcheck resolves the by-name patch to the 2-arg one as before) | unaffected |
> | `CombatReporter.cs:1042` | `Player.ConsumeItem(Inventory, ItemData, bool checkWorldLevel = false)` (override of Humanoid) | unchanged (15457 / 15464); `CookingStation.m_spawnFullDurability` only touches spawned-food durability, the postfix reads `m_shared.m_food/m_foodStamina/m_foodEitr` | unaffected |
> | `CombatReporter.cs:1071` | `OfferingBowl.InitiateSpawnBoss(Vector3, bool)` | unchanged (136412 / 136509) | unaffected |
> | `CombatReporter.cs:1094` | `TeleportWorld.Teleport(Player)` | unchanged | unaffected |
> | `CombatReporter.cs:1117` | `Player.StartGuardianPower()` | unchanged (15697 / 15704) | unaffected |
> | `CombatReporter.cs:1144` | `PlayerProfile.IncrementStat(PlayerStatType, float = 1, bool cheated = false)` | unchanged (106667 / 106748, identical): bucket 0 is still written unconditionally, buckets 1 and difficulty-index only when `Achievements.CanGetAchievements(cheated)`; postfix binds `stat`/`amount` by name and fires on every call regardless of `cheated` | unaffected |
> | `CombatReporter.cs:1171` | `Player.OnSpawned(bool)` | unchanged (11898 / 11905) | unaffected |
> | `CombatReporter.cs:639` | `PlayerProfile.m_playerStats[0].m_stats` | `PlayerProfile` lost the `s_bypassCheatChecks` FIELD and gained a `get_s_bypassCheatChecks()` PROPERTY (`.cctor` no longer zeroes it); `m_playerStats` (`PlayerStats[10]`, 1.0.12 line 106061) and the nested `PlayerStats` class are identical. The DLL's #Strings heap has no `s_bypassCheatChecks`; refcheck confirms no reference. `PlayerStatType` still ends `DeathByAshlandsLava, Count, None` - same 205 counters, wire protocol unchanged (TheRavensCall 1.2.2+ still required) | unaffected |
> | `CombatReporter.cs:653-667` | `Skills.GetSkillList()`, `Skill.m_info/m_level`, `Skill.GetLevelPercentage()` | `Skills` not in the changed-type list | unaffected |
> | `CombatReporter.cs:83-85, 125..448` | `ZNet.instance`, `ZNet.GetConnectionStatus()`, `ZRoutedRpc.instance.InvokeRoutedRPC`, `ZPackage` writers | `ZNet` changes are `OpenServer`/`SendPeerInfo`/`RPC_PeerInfo`/`DelayThenRegisterCoroutine` (network-version literal 39 -> 40) and `ListContainsId` (`flag |=`); the routed-RPC transport is untouched. The mod never prints or compares a game/network version | unaffected |
> | `CombatReporter.cs:92-97, 772, 1127` | `Player.GetCurrentBiome`, `EnvMan.GetCurrentBiome`, `WorldGenerator.GetBiome`, `Localization.Localize`, `Utils.GetPrefabName` | unchanged (`Utils` lives in the IL-identical assembly_utils) | unaffected |
> | `CombatReporter.cs:753-789` | death-cause heuristic (`m_lastHit == null` -> "a fall" / "drowning") | `Character.UpdateGroundContact` was restructured (m_onLand block, Deep North snow object) but the fall-damage branch (`num > 4f` -> `HitData` with `m_hitType = Fall` -> `Damage(hitData)`) is unchanged, so `m_lastHit` is populated exactly as before | unaffected |
> | (none) | `Terminal.ConsoleCommand` | the mod registers no console command, so the 1.0.12 gating change (HideBehindDevCommands commands now execute without devcommands) does not apply | n/a |
> | (none) | game interfaces / new non-defaulted parameters / ctor reshapes | the 37 changed types are all classes (no interface in the asmdiff); "Type-only signature changes" is empty, the 5 added methods are private `ZDOMan`/`AchievementUnlockPopup` helpers plus the `PlayerProfile` getter; only `CookingStation`/`PresentManager` instance ctors changed and only in field initialisers - the mod constructs no game type other than `ZPackage()` | n/a |
>
> **Behaviour note (not a mod change).** On 1.0.7 `s_bypassCheatChecks` was a `static bool = false` that nothing
> assigned; on 1.0.12 it is `Player.m_localPlayer` unique key `bypasscheatchecks == "1"`, settable by the new
> `yesiuseddevcommandsbutiwantmyachievementsanyway` command (which, being `hideBehindDevCommands:true`, is now
> executable without devcommands). When a player turns it on, `IncrementStat` additionally writes the per-difficulty
> buckets for cheated actions; the snapshot reads bucket 0, which is written in every case, so the numbers this mod
> reports do not move. Peers on 1.0.7 and 1.0.12 cannot connect to each other (network version 39 vs 40) - that is
> the game, not this mod, and needs no mod update.
>
> **Evidence.**
> - refcheck 1.0.12 (Mono.Cecil, exact signature + Harmony target resolution): CLIENT `RESULT: OK` (1029 references,
>   15 Harmony targets) `scratchpad/refcheck-1012/wtcf-WhereTheCrowFlies-client.txt`; SERVER `RESULT: OK`
>   (1029 / 15) `scratchpad/refcheck-1012/wtcf-WhereTheCrowFlies-server.txt`; `SUMMARY.txt` line 18 `client=0 server=0`.
> - Boot on the real 1.0.12 Linux dedicated server (`Valheim version: l-1.0.12 (network version 40)`), profile
>   `~/valheim-testbed/profiles/wtcf`, BepInEx 5.4.2350: **BOOTED after 25 s** - `scratchpad/boot-1012/wtcf.txt`;
>   `~/valheim-testbed/profiles/wtcf/BepInEx/LogOutput.log` has `Loading [WhereTheCrowFlies 1.1.1]`,
>   `[WhereTheCrowFlies] All patches applied.`, `WhereTheCrowFlies 1.1.1 loaded.` and no Harmony / MissingMethod /
>   MissingField / TypeLoad line. `diff` against the preserved 1.0.7 run
>   (`~/valheim-testbed/profiles/wtcf/logs-1.0.7-run/LogOutput.log`, `l-1.0.7 (network version 39)`) is empty; the
>   server-console errors are the same headless "Could not find video decode shader pass" noise in both.
> - Scratchpad root for the two `scratchpad/...` paths above:
>   `/tmp/claude-1000/-home-rohan-WubarrkCODING/7e501e8a-0a25-46f2-aaca-99a485eceb49/scratchpad`.
>
> **Still open (unchanged from 2026-09-10).** Real client playtest of a stat snapshot against TheRavensCall 1.2.2 -
> this is a client mod and the server boot only proves that every patch applies on the 1.0.12 assemblies.


> **Applied 2026-09-10 - WhereTheCrowFlies 1.1.1.** One source change: `TelemetryAccumulator.FlushStatSnapshot`
> (`Patches/CombatReporter.cs`) read `profile.m_playerStats.m_stats`; on 1.0.7 `PlayerProfile.m_playerStats` is
> `PlayerStats[10]` (one bucket per achievement difficulty), so the 1.1.0 DLL could not even compile against the
> 1.0.7 refs and the shipped build dies with `MissingFieldException` at its first snapshot. It now reads
> `m_playerStats[0]` - the unconditional bucket that `IncrementStat`/`SetStat` always write (the others are only
> written when achievements are allowed, i.e. not cheated), which is the exact successor of the single pre-1.0
> object. Rebuilt against the 1.0.7 refs in `libs-Tools` on BepInEx 5.4.2350. Version 1.1.0 -> 1.1.1 in
> lockstep (`Plugin.cs` `PluginVersion`, `HexiumDist/manifest.json`, README badge and zip name), CHANGELOG entry
> added, manifest dependency moved to `denikson-BepInExPack_Valheim-5.4.2350`.
>
> **Evidence.**
> - Build: `dotnet build WhereTheCrowFlies.csproj -c Release` -> **0 errors**.
> - refcheck (Mono.Cecil, exact-signature): CLIENT `RESULT: OK` (1029 references, 15 Harmony targets);
>   SERVER `RESULT: OK` (1029 references, 15 Harmony targets).
> - Boot-check on the real 1.0.7 Linux dedicated server, profile `~/valheim-testbed/profiles/wtcf`, port 2701:
>   **BOOTED after 25 s**. Key lines: `[Info : BepInEx] Loading [WhereTheCrowFlies 1.1.1]`,
>   `[WhereTheCrowFlies] All patches applied.`, `WhereTheCrowFlies 1.1.1 loaded.` No Harmony / MissingMethod /
>   MissingField / TypeLoad lines; the only errors are vanilla's headless "Could not find video decode shader pass"
>   noise. This is a CLIENT mod: the server boot proves that every patch applies on the 1.0.7 assemblies, not
>   that the telemetry paths run - those need a real client session.
>
> **Behaviour on 1.0.7.** `PlayerStatType` grew from 105 to 205 counters; the snapshot sends all of them as the
> same `(short id, float value)` pairs, so the wire protocol (`RavensCall_EventReport_V2`, schema 2) is unchanged.
> **TheRavensCall must be 1.2.2 or newer**: its receiver capped a snapshot at 200 pairs and silently dropped the
> tail. `IncrementStat` gained `bool cheated = false`; the postfix binds `stat` and `amount` by name and is
> unaffected. `Player.PlacePiece` gained `bool cheated`; still one overload, so the by-name patch binds, and its
> postfix parameters (`piece`, `pos`, `rot`, `doAttack`) all still exist.
>
> **Deviations / notes.** The repo has no fold script; the zip was made with the shared deterministic packer
> (same five entries as 1.1.0: manifest, README, CHANGELOG, icon, `plugins/WhereTheCrowFlies.dll`). A byte-identical
> copy of this project sits in `PAUSED/WhereTheCrowFlies/` from before it was un-paused; it is now stale.
>
> **Open.** Real client playtest of a stat snapshot against TheRavensCall 1.2.2 (expect 205 `vanilla_stats` keys
> in `BarrkBOT_data1.json`, the new ones named after the 1.0 enum entries).

## Audit table (every Harmony target and engine member, checked 2026-09-10 against `libs-Tools/1.0/DECOMPILED/`)

| Where | Target / member | 1.0.7 status |
|---|---|---|
| `CombatReporter.cs` | `Character.OnDeath` (private, by name) | present, one overload |
| `CombatReporter.cs` | `Character.Damage(HitData hit)` | present, unchanged |
| `CombatReporter.cs` | `Humanoid.BlockAttack(HitData, Character)` (by name) | present, one overload |
| `CombatReporter.cs` | `FishingFloat.Catch` (private, by name) | present, one overload |
| `CombatReporter.cs` | `Player.PlacePiece(Piece, Vector3, Quaternion, bool doAttack, bool cheated = false)` (by name) | present; gained `cheated`, still one overload; postfix params `piece`/`pos`/`rot`/`doAttack` exist |
| `CombatReporter.cs` | `Player.RemovePiece` (by name) | present, one overload |
| `CombatReporter.cs` | `InventoryGui.DoCrafting` / `OnRepairPressed` (by name) | present, one overload each |
| `CombatReporter.cs` | `Pickable.RPC_Pick` (by name) | present, one overload |
| `CombatReporter.cs` | `Player.ConsumeItem(Inventory, ItemData, bool checkWorldLevel = true)` | present, one overload |
| `CombatReporter.cs` | `OfferingBowl.InitiateSpawnBoss` (by name) | present, one overload |
| `CombatReporter.cs` | `TeleportWorld.Teleport(Player)` | present, one overload |
| `CombatReporter.cs` | `Player.StartGuardianPower()` | present, one overload |
| `CombatReporter.cs` | `PlayerProfile.IncrementStat(PlayerStatType stat, float amount = 1f, bool cheated = false)` | present; gained `cheated`; postfix binds `stat`, `amount` by name |
| `CombatReporter.cs` | `Player.OnSpawned` (by name) | present, one overload |
| `CombatReporter.cs` | **`PlayerProfile.m_playerStats`** | **CHANGED**: `PlayerStats` -> `PlayerStats[10]`; fixed (bucket 0) |
| `CombatReporter.cs` | `PlayerProfile.PlayerStats.m_stats` (`Dictionary<PlayerStatType, float>`) | present; 205 keys now |
| `CombatReporter.cs` | `Skills.GetSkillList()`, `Skills.Skill.m_info/m_level`, `Skill.GetLevelPercentage()` | present, unchanged |
| `CombatReporter.cs` | `ZRoutedRpc.InvokeRoutedRPC(string, params object[])`, `ZNet.instance`, `ZPackage` writers | present, unchanged |
| `CombatReporter.cs` | `Player.m_localPlayer`, `GetPlayerName()`, `Game.instance.GetPlayerProfile()` | present, unchanged |

Catalogue items that do **not** apply to this mod: no sector API, no `Vector2i`/`Vector2s`, no `VisEquipment`,
no `Inventory.Load` patch, no `PieceTable`, no `Terminal.ConsoleCommand`, no `Hoverable` implementations,
no `SetCreator`, no `IsTeleportable`, no `Version.*`, no save paths.
