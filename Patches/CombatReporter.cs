using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WhereTheCrowFlies.Patches
{
    #region Event Constants & Wire Protocol Types

    public static class EventType
    {
        public const byte Kill = 1;
        public const byte Death = 2;
        public const byte DamageDefenseBatch = 3;
        public const byte FishCatch = 4;
        public const byte Building = 5;
        public const byte Crafting = 6;
        public const byte Harvesting = 7;
        public const byte Consumables = 8;
        public const byte WorldEvent = 9;
        public const byte StatSync = 10;
        // StatSync (10) only ever carries deltas accumulated *after* this mod
        // was loaded, so anything a player already earned before install (or
        // during any tick the client was disconnected) never reaches the
        // server and totals silently drift from the client's true save data.
        // StatSnapshot/SkillSnapshot below broadcast full absolute state on
        // every flush instead, so the server always has ground truth within
        // one flush interval, no matter what it missed.
        public const byte StatSnapshot = 11;
        public const byte SkillSnapshot = 12;
    }

    // One entry in a SkillSnapshot payload: a single Skills.SkillType the
    // player has touched at least once (untouched skills are omitted, same
    // as the engine's own Skills.GetSkillList()).
    internal struct SkillEntry
    {
        public short SkillId;
        public float Level;      // 0..100, floored (matches Skills.GetSkillLevel)
        public float Progress;   // 0..1 fraction of XP toward next level
    }

    public static class BuildingAction
    {
        public const byte Placed = 1;
        public const byte Removed = 2;
        public const byte Repaired = 3;
    }

    public static class CraftingAction
    {
        public const byte Crafted = 1;
        public const byte Upgraded = 2;
        public const byte Repaired = 3;
    }

    public static class HarvestSourceType
    {
        public const byte Foraged = 1;
        public const byte Crop = 2;
        public const byte Beehive = 3;
        public const byte SapCollector = 4;
    }

    public static class ConsumableType
    {
        public const byte Food = 1;
        public const byte PotionOrMead = 2;
        public const byte Other = 3;
    }

    #endregion

    #region Telemetry Sender

    internal static class TelemetrySender
    {
        private const string RpcNameV1 = "RavensCall_CombatReport_V1";
        private const string RpcNameV2 = "RavensCall_EventReport_V2";

        private static bool IsConnected()
        {
            return ZNet.instance != null &&
                   ZRoutedRpc.instance != null &&
                   ZNet.GetConnectionStatus() == ZNet.ConnectionStatus.Connected;
        }

        public static string GetBiomeName(Vector3 pos)
        {
            try
            {
                if (Player.m_localPlayer != null)
                    return Player.m_localPlayer.GetCurrentBiome().ToString();
                if (EnvMan.instance != null)
                    return EnvMan.instance.GetCurrentBiome().ToString();
                if (WorldGenerator.instance != null)
                    return WorldGenerator.instance.GetBiome(pos).ToString();
            }
            catch { }
            return "Unknown";
        }

        public static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Replace("(Clone)", "").Trim();
        }

        #region V1 Legacy Sender

        public static void SendV1(byte eventType, Vector3 position, string prefabOrCause, string attackerName, float dmgDealt = 0f, float dmgTaken = 0f)
        {
            try
            {
                if (!IsConnected()) return;

                var pkg = new ZPackage();
                pkg.Write(1); // schemaVersion 1
                pkg.Write(eventType);
                pkg.Write(prefabOrCause ?? "");
                pkg.Write(attackerName ?? "");
                pkg.Write(position);
                pkg.Write(dmgDealt);
                pkg.Write(dmgTaken);
                ZRoutedRpc.instance.InvokeRoutedRPC(RpcNameV1, pkg);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] SendV1 failed: {ex.Message}");
            }
        }

        #endregion

        #region V2 High-Coverage Senders

        public static void SendKill(string attackerName, string victimPrefab, Vector3 pos, int level, bool isBoss, bool isTamed, string weaponOrDmgType, string biome)
        {
            try
            {
                if (!IsConnected()) return;

                // 1. Emit V2 rich kill event
                var pkg = new ZPackage();
                pkg.Write(2); // schemaVersion 2
                pkg.Write(EventType.Kill);
                pkg.Write(attackerName ?? "");
                pkg.Write(victimPrefab ?? "");
                pkg.Write(pos);
                pkg.Write(level);
                pkg.Write(isBoss);
                pkg.Write(isTamed);
                pkg.Write(weaponOrDmgType ?? "");
                pkg.Write(biome ?? "");
                ZRoutedRpc.instance.InvokeRoutedRPC(RpcNameV2, pkg);

                // 2. Emit V1 backward-compatibility packet
                SendV1(1, pos, victimPrefab, attackerName);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] SendKill failed: {ex.Message}");
            }
        }

        public static void SendDeath(string victimName, string cause, Vector3 pos, string killerPlayer, string biome)
        {
            try
            {
                if (!IsConnected()) return;

                // 1. Emit V2 rich death event
                var pkg = new ZPackage();
                pkg.Write(2);
                pkg.Write(EventType.Death);
                pkg.Write(victimName ?? "");
                pkg.Write(cause ?? "");
                pkg.Write(pos);
                pkg.Write(killerPlayer ?? "");
                pkg.Write(biome ?? "");
                ZRoutedRpc.instance.InvokeRoutedRPC(RpcNameV2, pkg);

                // 2. Emit V1 backward-compatibility packet
                SendV1(2, pos, cause, victimName);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] SendDeath failed: {ex.Message}");
            }
        }

        public static void SendDamageDefenseBatch(string playerName, Vector3 pos,
            float dealtCreatures, float dealtPlayers,
            float takenCreatures, float takenPlayers, float takenEnv,
            int hitsDealt, int hitsTaken, int blocks, int parries, float dmgBlocked)
        {
            try
            {
                if (!IsConnected()) return;

                // 1. Emit V2 rich batch
                var pkg = new ZPackage();
                pkg.Write(2);
                pkg.Write(EventType.DamageDefenseBatch);
                pkg.Write(playerName ?? "");
                pkg.Write(pos);
                pkg.Write(dealtCreatures);
                pkg.Write(dealtPlayers);
                pkg.Write(takenCreatures);
                pkg.Write(takenPlayers);
                pkg.Write(takenEnv);
                pkg.Write(hitsDealt);
                pkg.Write(hitsTaken);
                pkg.Write(blocks);
                pkg.Write(parries);
                pkg.Write(dmgBlocked);
                ZRoutedRpc.instance.InvokeRoutedRPC(RpcNameV2, pkg);

                // 2. Emit V1 backward-compatibility packet (sum of total dealt and total taken)
                float totalDealt = dealtCreatures + dealtPlayers;
                float totalTaken = takenCreatures + takenPlayers + takenEnv;
                SendV1(3, pos, "", playerName, totalDealt, totalTaken);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] SendDamageDefenseBatch failed: {ex.Message}");
            }
        }

        public static void SendFishCatch(string playerName, string fishPrefab, Vector3 pos, int quality, float weight, string biome)
        {
            try
            {
                if (!IsConnected()) return;

                // 1. Emit V2 rich catch
                var pkg = new ZPackage();
                pkg.Write(2);
                pkg.Write(EventType.FishCatch);
                pkg.Write(playerName ?? "");
                pkg.Write(fishPrefab ?? "");
                pkg.Write(pos);
                pkg.Write(quality);
                pkg.Write(weight);
                pkg.Write(biome ?? "");
                ZRoutedRpc.instance.InvokeRoutedRPC(RpcNameV2, pkg);

                // 2. Emit V1 backward-compatibility packet
                SendV1(4, pos, fishPrefab, playerName);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] SendFishCatch failed: {ex.Message}");
            }
        }

        public static void SendBuilding(string playerName, string piecePrefab, Vector3 pos, byte action, string category)
        {
            try
            {
                if (!IsConnected()) return;

                var pkg = new ZPackage();
                pkg.Write(2);
                pkg.Write(EventType.Building);
                pkg.Write(playerName ?? "");
                pkg.Write(piecePrefab ?? "");
                pkg.Write(pos);
                pkg.Write(action);
                pkg.Write(category ?? "");
                ZRoutedRpc.instance.InvokeRoutedRPC(RpcNameV2, pkg);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] SendBuilding failed: {ex.Message}");
            }
        }

        public static void SendCrafting(string playerName, string itemPrefab, Vector3 pos, byte action, int quality, int amount, string stationName)
        {
            try
            {
                if (!IsConnected()) return;

                var pkg = new ZPackage();
                pkg.Write(2);
                pkg.Write(EventType.Crafting);
                pkg.Write(playerName ?? "");
                pkg.Write(itemPrefab ?? "");
                pkg.Write(pos);
                pkg.Write(action);
                pkg.Write(quality);
                pkg.Write(amount);
                pkg.Write(stationName ?? "");
                ZRoutedRpc.instance.InvokeRoutedRPC(RpcNameV2, pkg);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] SendCrafting failed: {ex.Message}");
            }
        }

        public static void SendHarvesting(string playerName, string resourceName, Vector3 pos, byte sourceType, int amount)
        {
            try
            {
                if (!IsConnected()) return;

                var pkg = new ZPackage();
                pkg.Write(2);
                pkg.Write(EventType.Harvesting);
                pkg.Write(playerName ?? "");
                pkg.Write(resourceName ?? "");
                pkg.Write(pos);
                pkg.Write(sourceType);
                pkg.Write(amount);
                ZRoutedRpc.instance.InvokeRoutedRPC(RpcNameV2, pkg);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] SendHarvesting failed: {ex.Message}");
            }
        }

        public static void SendConsumable(string playerName, string itemPrefab, Vector3 pos, byte itemType, float health, float stamina, float eitr)
        {
            try
            {
                if (!IsConnected()) return;

                var pkg = new ZPackage();
                pkg.Write(2);
                pkg.Write(EventType.Consumables);
                pkg.Write(playerName ?? "");
                pkg.Write(itemPrefab ?? "");
                pkg.Write(pos);
                pkg.Write(itemType);
                pkg.Write(health);
                pkg.Write(stamina);
                pkg.Write(eitr);
                ZRoutedRpc.instance.InvokeRoutedRPC(RpcNameV2, pkg);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] SendConsumable failed: {ex.Message}");
            }
        }

        public static void SendWorldEvent(string playerName, string eventName, Vector3 pos, string targetOrDetails, string biome)
        {
            try
            {
                if (!IsConnected()) return;

                var pkg = new ZPackage();
                pkg.Write(2);
                pkg.Write(EventType.WorldEvent);
                pkg.Write(playerName ?? "");
                pkg.Write(eventName ?? "");
                pkg.Write(pos);
                pkg.Write(targetOrDetails ?? "");
                pkg.Write(biome ?? "");
                ZRoutedRpc.instance.InvokeRoutedRPC(RpcNameV2, pkg);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] SendWorldEvent failed: {ex.Message}");
            }
        }

        public static void SendStatSync(string playerName, Vector3 pos, List<KeyValuePair<short, float>> statPairs)
        {
            try
            {
                if (!IsConnected() || statPairs == null || statPairs.Count == 0) return;

                var pkg = new ZPackage();
                pkg.Write(2);
                pkg.Write(EventType.StatSync);
                pkg.Write(playerName ?? "");
                pkg.Write(pos);
                pkg.Write(statPairs.Count);
                foreach (var pair in statPairs)
                {
                    pkg.Write(pair.Key);
                    pkg.Write(pair.Value);
                }
                ZRoutedRpc.instance.InvokeRoutedRPC(RpcNameV2, pkg);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] SendStatSync failed: {ex.Message}");
            }
        }

        // Full absolute value of all vanilla PlayerStatType counters (not a
        // delta — see EventType.StatSnapshot). Same wire shape as SendStatSync
        // (statId/value pairs) so a receiver can reuse the same unpack loop
        // and just assign instead of accumulate.
        public static void SendStatSnapshot(string playerName, Vector3 pos, List<KeyValuePair<short, float>> statPairs)
        {
            try
            {
                if (!IsConnected() || statPairs == null || statPairs.Count == 0) return;

                var pkg = new ZPackage();
                pkg.Write(2);
                pkg.Write(EventType.StatSnapshot);
                pkg.Write(playerName ?? "");
                pkg.Write(pos);
                pkg.Write(statPairs.Count);
                foreach (var pair in statPairs)
                {
                    pkg.Write(pair.Key);
                    pkg.Write(pair.Value);
                }
                ZRoutedRpc.instance.InvokeRoutedRPC(RpcNameV2, pkg);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] SendStatSnapshot failed: {ex.Message}");
            }
        }

        // Full absolute skill state (level + progress-to-next-level) for every
        // Skills.SkillType the player has touched at least once. Untouched
        // skills are omitted entirely, same as the engine's own
        // Skills.GetSkillList() — there is nothing meaningful to report for a
        // skill at level 0 with zero accumulator.
        public static void SendSkillSnapshot(string playerName, Vector3 pos, List<SkillEntry> skills)
        {
            try
            {
                if (!IsConnected() || skills == null || skills.Count == 0) return;

                var pkg = new ZPackage();
                pkg.Write(2);
                pkg.Write(EventType.SkillSnapshot);
                pkg.Write(playerName ?? "");
                pkg.Write(pos);
                pkg.Write(skills.Count);
                foreach (var s in skills)
                {
                    pkg.Write(s.SkillId);
                    pkg.Write(s.Level);
                    pkg.Write(s.Progress);
                }
                ZRoutedRpc.instance.InvokeRoutedRPC(RpcNameV2, pkg);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] SendSkillSnapshot failed: {ex.Message}");
            }
        }

        #endregion
    }

    #endregion

    #region Telemetry Accumulators

    internal static class TelemetryAccumulator
    {
        private class DamageDefenseEntry
        {
            public float DealtCreatures;
            public float DealtPlayers;
            public float TakenCreatures;
            public float TakenPlayers;
            public float TakenEnv;
            public int HitsDealt;
            public int HitsTaken;
            public int Blocks;
            public int Parries;
            public float DmgBlocked;

            public bool HasData => (DealtCreatures > 0f || DealtPlayers > 0f ||
                                    TakenCreatures > 0f || TakenPlayers > 0f || TakenEnv > 0f ||
                                    HitsDealt > 0 || HitsTaken > 0 || Blocks > 0 || Parries > 0 || DmgBlocked > 0f);

            public void Clear()
            {
                DealtCreatures = 0f;
                DealtPlayers = 0f;
                TakenCreatures = 0f;
                TakenPlayers = 0f;
                TakenEnv = 0f;
                HitsDealt = 0;
                HitsTaken = 0;
                Blocks = 0;
                Parries = 0;
                DmgBlocked = 0f;
            }
        }

        private static readonly Dictionary<string, DamageDefenseEntry> _combatAcc = new Dictionary<string, DamageDefenseEntry>();
        private static readonly Dictionary<short, float> _statDeltas = new Dictionary<short, float>();

        private const float FlushIntervalSeconds = 10f;
        private const float FlushCombatThreshold = 500f;
        private static float _timer;

        // Full-state resync (StatSnapshot/SkillSnapshot) runs on its own,
        // independently-configurable clock — see Plugin.FullSyncIntervalSeconds.
        // These are idempotent snapshots, not latency-sensitive deltas, so
        // they don't need to share the 10s combat/damage-batch cadence above.
        private static float _fullSyncTimer;

        public static void AddCombatDamage(string playerName, float dealtCreature, float dealtPlayer, float takenCreature, float takenPlayer, float takenEnv)
        {
            if (string.IsNullOrEmpty(playerName)) return;
            if (!_combatAcc.TryGetValue(playerName, out var e))
            {
                e = new DamageDefenseEntry();
                _combatAcc[playerName] = e;
            }

            e.DealtCreatures += dealtCreature;
            e.DealtPlayers += dealtPlayer;
            e.TakenCreatures += takenCreature;
            e.TakenPlayers += takenPlayer;
            e.TakenEnv += takenEnv;

            if (dealtCreature > 0f || dealtPlayer > 0f) e.HitsDealt++;
            if (takenCreature > 0f || takenPlayer > 0f || takenEnv > 0f) e.HitsTaken++;

            float totalActivity = e.DealtCreatures + e.DealtPlayers + e.TakenCreatures + e.TakenPlayers + e.TakenEnv;
            if (totalActivity >= FlushCombatThreshold)
            {
                FlushCombatEntry(playerName, e);
            }
        }

        public static void AddBlockDefense(string playerName, bool isParry, float dmgBlocked)
        {
            if (string.IsNullOrEmpty(playerName)) return;
            if (!_combatAcc.TryGetValue(playerName, out var e))
            {
                e = new DamageDefenseEntry();
                _combatAcc[playerName] = e;
            }

            if (isParry) e.Parries++;
            else e.Blocks++;
            e.DmgBlocked += dmgBlocked;
        }

        public static void AddStatDelta(short statId, float delta)
        {
            if (delta == 0f) return;
            if (!_statDeltas.TryGetValue(statId, out var current))
            {
                _statDeltas[statId] = delta;
            }
            else
            {
                _statDeltas[statId] = current + delta;
            }
        }

        public static void Tick(float dt)
        {
            _timer += dt;
            if (_timer >= FlushIntervalSeconds)
            {
                _timer = 0f;

                // 1. Flush Combat & Defense Batches
                foreach (var kv in _combatAcc)
                {
                    if (kv.Value.HasData)
                    {
                        FlushCombatEntry(kv.Key, kv.Value);
                    }
                }

                // 2. Flush Vanilla PlayerStat Deltas
                if (_statDeltas.Count > 0)
                {
                    FlushStatDeltas();
                }
            }

            // 3. Full-state resync: absolute stat totals + skills, on its own
            // (configurable, default 60s) clock. Delta-only accounting above
            // means anything already earned before this mod loaded — or any
            // tick a disconnect swallowed — never reaches the server, and
            // server-side totals silently drift from what the player's own
            // save/UI shows (this is the "123 kills already banked but the
            // server doesn't know" report). Re-broadcasting ground truth
            // periodically is self-healing and backfills pre-existing data
            // automatically: the very first flush after a player connects
            // already carries their full lifetime totals, so the receiver
            // just needs to assign (=) rather than accumulate (+=) these two
            // event types. Kept off the 10s combat-batch clock since a full
            // snapshot is idempotent and not latency-sensitive the way a
            // damage batch is — no need to pay that cadence for it too.
            float fullSyncInterval = Plugin.FullSyncIntervalSeconds != null
                ? Mathf.Max(10f, Plugin.FullSyncIntervalSeconds.Value)
                : 60f;
            _fullSyncTimer += dt;
            if (_fullSyncTimer >= fullSyncInterval)
            {
                _fullSyncTimer = 0f;
                FlushStatSnapshot();
                FlushSkillSnapshot();
            }
        }

        // Public entry point for an out-of-band backfill send (player spawn),
        // independent of the 10s Tick() cadence — see Patch_PlayerOnSpawned.
        public static void FlushFullBackfill()
        {
            FlushStatSnapshot();
            FlushSkillSnapshot();
        }

        private static void FlushStatSnapshot()
        {
            if (Player.m_localPlayer == null || Game.instance == null) return;
            var profile = Game.instance.GetPlayerProfile();
            if (profile == null) return;

            string playerName = Player.m_localPlayer.GetPlayerName();
            Vector3 pos = Player.m_localPlayer.transform.position;

            // Valheim 1.0 turned PlayerProfile.m_playerStats from one PlayerStats into an
            // array of ten, one per achievement difficulty. Index 0 is the unconditional
            // bucket: IncrementStat/SetStat always write it, the others only when
            // achievements are enabled (not cheated, matching difficulty). It is the exact
            // successor of the pre-1.0 single object, so the snapshot reads it and nothing
            // else - the per-difficulty buckets would undercount a cheated session.
            //
            // Iterate m_stats directly rather than Enum.GetValues(typeof(PlayerStatType)):
            // the enum's trailing Count sentinel is not a real stat and is never added as a
            // key by PlayerStats' own constructor, so this can't leak it. 1.0 grew the enum
            // from 105 to 205 counters; they ride along as extra (id, value) pairs.
            var stats = profile.m_playerStats != null && profile.m_playerStats.Length > 0 ? profile.m_playerStats[0] : null;
            if (stats == null || stats.m_stats == null) return;
            var list = new List<KeyValuePair<short, float>>(stats.m_stats.Count);
            foreach (var kv in stats.m_stats)
            {
                list.Add(new KeyValuePair<short, float>((short)kv.Key, kv.Value));
            }

            TelemetrySender.SendStatSnapshot(playerName, pos, list);
        }

        private static void FlushSkillSnapshot()
        {
            if (Player.m_localPlayer == null) return;
            var skills = Player.m_localPlayer.GetSkills();
            if (skills == null) return;

            string playerName = Player.m_localPlayer.GetPlayerName();
            Vector3 pos = Player.m_localPlayer.transform.position;

            var list = new List<SkillEntry>();
            foreach (var skill in skills.GetSkillList())
            {
                if (skill?.m_info == null) continue;
                list.Add(new SkillEntry
                {
                    SkillId = (short)skill.m_info.m_skill,
                    Level = skill.m_level,
                    Progress = skill.GetLevelPercentage()
                });
            }

            TelemetrySender.SendSkillSnapshot(playerName, pos, list);
        }

        private static void FlushCombatEntry(string playerName, DamageDefenseEntry e)
        {
            Vector3 pos = Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : Vector3.zero;
            TelemetrySender.SendDamageDefenseBatch(playerName, pos,
                e.DealtCreatures, e.DealtPlayers,
                e.TakenCreatures, e.TakenPlayers, e.TakenEnv,
                e.HitsDealt, e.HitsTaken, e.Blocks, e.Parries, e.DmgBlocked);
            e.Clear();
        }

        private static void FlushStatDeltas()
        {
            if (Player.m_localPlayer == null) return;
            string playerName = Player.m_localPlayer.GetPlayerName();
            Vector3 pos = Player.m_localPlayer.transform.position;

            var list = new List<KeyValuePair<short, float>>(_statDeltas.Count);
            foreach (var kv in _statDeltas)
            {
                if (kv.Value != 0f)
                {
                    list.Add(kv);
                }
            }
            _statDeltas.Clear();

            if (list.Count > 0)
            {
                TelemetrySender.SendStatSync(playerName, pos, list);
            }
        }
    }

    #endregion

    #region Harmony Patches: Combat & Deaths

    [HarmonyPatch(typeof(Character), "OnDeath")]
    internal static class Patch_CharacterDeath
    {
        private static void Prefix(Character __instance)
        {
            try
            {
                if (!Plugin.EnableReporting.Value) return;
                if (__instance == null || !__instance.IsOwner()) return;

                Vector3 pos = __instance.transform.position;
                string biome = TelemetrySender.GetBiomeName(pos);

                // Case 1: Player Died
                if (__instance is Player victim)
                {
                    string killerPlayer = "";
                    string cause = ComputeDeathCause(victim, out killerPlayer);
                    TelemetrySender.SendDeath(victim.GetPlayerName(), cause, pos, killerPlayer, biome);
                    return;
                }

                // Case 2: Creature Died
                var hit = __instance.m_lastHit;
                var attacker = hit?.GetAttacker() as Player;
                if (attacker == null) return; // Unattributed kill dropped

                string attackerName = attacker.GetPlayerName();
                string prefab = TelemetrySender.CleanName(__instance.gameObject.name);
                int level = __instance.GetLevel();
                bool isBoss = __instance.IsBoss();
                bool isTamed = __instance.IsTamed();
                string dmgType = hit != null ? hit.m_damage.GetMajorityDamageType().ToString() : "Combat";

                TelemetrySender.SendKill(attackerName, prefab, pos, level, isBoss, isTamed, dmgType, biome);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] OnDeath report failed: {ex.Message}");
            }
        }

        private static string ComputeDeathCause(Player victim, out string killerPlayer)
        {
            killerPlayer = "";
            try
            {
                var hit = victim.m_lastHit;
                bool inWater = false;
                try { inWater = victim.IsSwimming() || victim.InWater(); } catch { }
                if (hit == null) return inWater ? "drowning" : "a fall";

                var attackerChar = hit.GetAttacker();
                if (attackerChar != null)
                {
                    if (attackerChar is Player pvpKiller)
                    {
                        killerPlayer = pvpKiller.GetPlayerName();
                        return $"combat with {pvpKiller.GetPlayerName()}";
                    }

                    string locName = Localization.instance != null ? Localization.instance.Localize(attackerChar.m_name) : attackerChar.m_name;
                    return attackerChar.IsBoss() ? locName : $"a {locName}";
                }

                var dmg = hit.m_damage;
                if (dmg.m_fire > 0f) return "burning";
                if (dmg.m_frost > 0f) return "freezing";
                if (dmg.m_poison > 0f) return "poison";
                if (dmg.m_spirit > 0f) return "spirit damage";
                if (dmg.m_lightning > 0f) return "lightning damage";
                if (dmg.m_blunt > 0f || dmg.m_slash > 0f || dmg.m_pierce > 0f) return inWater ? "drowning" : "combat";
                return inWater ? "drowning" : "a fall";
            }
            catch
            {
                return "unknown causes";
            }
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    internal static class Patch_Damage
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            try
            {
                if (!Plugin.EnableReporting.Value || hit == null || __instance == null) return;
                if (!__instance.IsOwner()) return;

                var attacker = hit.GetAttacker();
                float totalDamage = hit.GetTotalDamage();
                if (totalDamage <= 0f) return;

                // Dealt by local player
                if (attacker is Player playerAttacker)
                {
                    string attName = playerAttacker.GetPlayerName();
                    if (__instance is Player)
                    {
                        // PvP damage dealt
                        TelemetryAccumulator.AddCombatDamage(attName, 0f, totalDamage, 0f, 0f, 0f);
                    }
                    else
                    {
                        // PvE damage dealt to creature
                        TelemetryAccumulator.AddCombatDamage(attName, totalDamage, 0f, 0f, 0f, 0f);
                    }
                }

                // Taken by local victim
                if (__instance is Player victimPlayer)
                {
                    string vicName = victimPlayer.GetPlayerName();
                    if (attacker is Player)
                    {
                        // PvP damage taken
                        TelemetryAccumulator.AddCombatDamage(vicName, 0f, 0f, 0f, totalDamage, 0f);
                    }
                    else if (attacker != null)
                    {
                        // PvE damage taken from monster
                        TelemetryAccumulator.AddCombatDamage(vicName, 0f, 0f, totalDamage, 0f, 0f);
                    }
                    else
                    {
                        // Environmental damage taken
                        TelemetryAccumulator.AddCombatDamage(vicName, 0f, 0f, 0f, 0f, totalDamage);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] Damage report failed: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
    internal static class Patch_BlockAttack
    {
        private static void Postfix(Humanoid __instance, HitData hit, Character attacker, bool __result)
        {
            try
            {
                if (!Plugin.EnableReporting.Value || !__result) return;
                if (!(__instance is Player player) || player != Player.m_localPlayer) return;

                var blocker = player.GetCurrentBlocker();
                if (blocker == null || hit == null) return;

                bool isParry = blocker.m_shared.m_timedBlockBonus > 1f && player.m_blockTimer != -1f && player.m_blockTimer < 0.25f;
                float blockedDamage = hit.GetTotalBlockableDamage();

                TelemetryAccumulator.AddBlockDefense(player.GetPlayerName(), isParry, blockedDamage);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] Block report failed: {ex.Message}");
            }
        }
    }

    #endregion

    #region Harmony Patches: Fishing

    [HarmonyPatch(typeof(FishingFloat), "Catch")]
    internal static class Patch_FishCatch
    {
        private static void Postfix(Fish fish, Character owner)
        {
            try
            {
                if (!Plugin.EnableReporting.Value || fish == null) return;
                if (!(owner is Player p) || p != Player.m_localPlayer) return;

                var itemDrop = fish.GetComponent<ItemDrop>();
                string slug = itemDrop?.m_itemData?.m_shared?.m_name ?? fish.gameObject.name;
                slug = TelemetrySender.CleanName(slug);
                if (string.IsNullOrEmpty(slug)) return;

                int quality = itemDrop != null ? itemDrop.m_itemData.m_quality : 1;
                float weight = itemDrop != null ? itemDrop.m_itemData.GetWeight() : 1f;
                Vector3 pos = p.transform.position;
                string biome = TelemetrySender.GetBiomeName(pos);

                TelemetrySender.SendFishCatch(p.GetPlayerName(), slug, pos, quality, weight, biome);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] FishCatch report failed: {ex.Message}");
            }
        }
    }

    #endregion

    #region Harmony Patches: Building & Deconstruction

    [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
    internal static class Patch_PlayerPlacePiece
    {
        private static void Postfix(Player __instance, Piece piece, Vector3 pos, Quaternion rot, bool doAttack)
        {
            try
            {
                if (!Plugin.EnableReporting.Value || piece == null) return;
                if (__instance != Player.m_localPlayer) return;

                string pieceName = TelemetrySender.CleanName(piece.gameObject.name);
                string category = piece.m_category.ToString();
                TelemetrySender.SendBuilding(__instance.GetPlayerName(), pieceName, pos, BuildingAction.Placed, category);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] PlacePiece report failed: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Player), "RemovePiece")]
    internal static class Patch_PlayerRemovePiece
    {
        private static void Postfix(Player __instance, bool __result)
        {
            try
            {
                if (!Plugin.EnableReporting.Value || !__result) return;
                if (__instance != Player.m_localPlayer) return;

                // Record piece demolition
                TelemetrySender.SendBuilding(__instance.GetPlayerName(), "Piece", __instance.transform.position, BuildingAction.Removed, "Structure");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] RemovePiece report failed: {ex.Message}");
            }
        }
    }

    #endregion

    #region Harmony Patches: Crafting & Repairs

    [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
    internal static class Patch_Crafting
    {
        private static void Postfix(InventoryGui __instance, Player player)
        {
            try
            {
                if (!Plugin.EnableReporting.Value || player == null || player != Player.m_localPlayer) return;
                if (__instance == null || __instance.m_craftRecipe == null) return;

                var recipe = __instance.m_craftRecipe;
                string itemName = recipe.m_item != null ? TelemetrySender.CleanName(recipe.m_item.gameObject.name) : "Unknown";
                byte action = __instance.m_craftUpgradeItem != null ? CraftingAction.Upgraded : CraftingAction.Crafted;
                int quality = __instance.m_craftUpgradeItem != null ? (__instance.m_craftUpgradeItem.m_quality + 1) : 1;
                int amount = recipe.m_amount;
                string stationName = player.GetCurrentCraftingStation() != null ? TelemetrySender.CleanName(player.GetCurrentCraftingStation().gameObject.name) : "Hand";

                TelemetrySender.SendCrafting(player.GetPlayerName(), itemName, player.transform.position, action, quality, amount, stationName);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] DoCrafting report failed: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "OnRepairPressed")]
    internal static class Patch_Repair
    {
        private static void Postfix(InventoryGui __instance)
        {
            try
            {
                if (!Plugin.EnableReporting.Value) return;
                var player = Player.m_localPlayer;
                if (player == null) return;

                string stationName = player.GetCurrentCraftingStation() != null ? TelemetrySender.CleanName(player.GetCurrentCraftingStation().gameObject.name) : "Hand";
                TelemetrySender.SendCrafting(player.GetPlayerName(), "RepairedEquipment", player.transform.position, CraftingAction.Repaired, 1, 1, stationName);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] OnRepairPressed report failed: {ex.Message}");
            }
        }
    }

    #endregion

    #region Harmony Patches: Harvesting & Foraging

    [HarmonyPatch(typeof(Pickable), "RPC_Pick")]
    internal static class Patch_Pickable
    {
        private static void Prefix(Pickable __instance, long sender, int bonus)
        {
            try
            {
                if (!Plugin.EnableReporting.Value || __instance == null) return;
                var player = Player.m_localPlayer;
                if (player == null) return;

                string itemName = __instance.GetHoverName();
                if (string.IsNullOrEmpty(itemName) && __instance.m_itemPrefab != null)
                {
                    itemName = TelemetrySender.CleanName(__instance.m_itemPrefab.name);
                }
                if (string.IsNullOrEmpty(itemName)) itemName = "Pickable";

                byte sourceType = __instance.m_harvestable ? HarvestSourceType.Crop : HarvestSourceType.Foraged;
                int amount = Mathf.Max(1, __instance.m_amount + bonus);

                TelemetrySender.SendHarvesting(player.GetPlayerName(), itemName, __instance.transform.position, sourceType, amount);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] Pickable report failed: {ex.Message}");
            }
        }
    }

    #endregion

    #region Harmony Patches: Consumables (Food & Potions)

    [HarmonyPatch(typeof(Player), nameof(Player.ConsumeItem))]
    internal static class Patch_ConsumeItem
    {
        private static void Postfix(Player __instance, Inventory inventory, ItemDrop.ItemData item, bool checkWorldLevel, bool __result)
        {
            try
            {
                if (!Plugin.EnableReporting.Value || !__result || item == null) return;
                if (__instance != Player.m_localPlayer) return;

                string itemName = item.m_dropPrefab != null ? TelemetrySender.CleanName(item.m_dropPrefab.name) : item.m_shared.m_name;
                byte itemType = item.m_shared.m_food > 0f ? ConsumableType.Food : ConsumableType.PotionOrMead;
                float health = item.m_shared.m_food;
                float stamina = item.m_shared.m_foodStamina;
                float eitr = item.m_shared.m_foodEitr;

                TelemetrySender.SendConsumable(__instance.GetPlayerName(), itemName, __instance.transform.position, itemType, health, stamina, eitr);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] ConsumeItem report failed: {ex.Message}");
            }
        }
    }

    #endregion

    #region Harmony Patches: World Events (Boss Summons, Portals, Guardian Powers)

    [HarmonyPatch(typeof(OfferingBowl), "InitiateSpawnBoss")]
    internal static class Patch_OfferingBowl
    {
        private static void Prefix(OfferingBowl __instance, Vector3 point, bool removeItemsFromInventory)
        {
            try
            {
                if (!Plugin.EnableReporting.Value || __instance == null) return;
                var player = Player.m_localPlayer;
                if (player == null) return;

                string bossName = __instance.m_bossPrefab != null ? TelemetrySender.CleanName(__instance.m_bossPrefab.name) : "UnknownBoss";
                string biome = TelemetrySender.GetBiomeName(point);

                TelemetrySender.SendWorldEvent(player.GetPlayerName(), "BossSummoned", point, bossName, biome);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] Boss summon report failed: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Teleport))]
    internal static class Patch_TeleportWorld
    {
        private static void Prefix(TeleportWorld __instance, Player player)
        {
            try
            {
                if (!Plugin.EnableReporting.Value || player == null || player != Player.m_localPlayer) return;
                if (__instance == null || !__instance.TargetFound()) return;

                string tag = __instance.GetText();
                Vector3 pos = player.transform.position;
                string biome = TelemetrySender.GetBiomeName(pos);

                TelemetrySender.SendWorldEvent(player.GetPlayerName(), "PortalTraversed", pos, tag, biome);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] Portal report failed: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.StartGuardianPower))]
    internal static class Patch_GuardianPower
    {
        private static void Postfix(Player __instance, bool __result)
        {
            try
            {
                if (!Plugin.EnableReporting.Value || !__result) return;
                if (__instance != Player.m_localPlayer || __instance.m_guardianSE == null) return;

                string powerName = Utils.GetPrefabName(__instance.m_guardianSE.name);
                Vector3 pos = __instance.transform.position;
                string biome = TelemetrySender.GetBiomeName(pos);

                TelemetrySender.SendWorldEvent(__instance.GetPlayerName(), "GuardianPowerUsed", pos, powerName, biome);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] Guardian power report failed: {ex.Message}");
            }
        }
    }

    #endregion

    #region Harmony Patches: 100% Vanilla PlayerStat Delta Sync

    [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStat))]
    internal static class Patch_PlayerProfileIncrementStat
    {
        private static void Postfix(PlayerProfile __instance, PlayerStatType stat, float amount)
        {
            try
            {
                if (!Plugin.EnableReporting.Value || amount == 0f) return;
                if (Game.instance == null || Game.instance.GetPlayerProfile() != __instance) return;

                TelemetryAccumulator.AddStatDelta((short)stat, amount);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] StatIncrement delta capture failed: {ex.Message}");
            }
        }
    }

    #endregion

    #region Harmony Patches: Immediate Backfill On Spawn

    // Fires one StatSnapshot + SkillSnapshot immediately when the local
    // character spawns into the world, instead of waiting for the first 10s
    // accumulator tick. Without this, a player who logs in and disconnects
    // inside that window never backfills their pre-existing totals at all.
    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    internal static class Patch_PlayerOnSpawned
    {
        private static void Postfix(Player __instance)
        {
            try
            {
                if (!Plugin.EnableReporting.Value || __instance != Player.m_localPlayer) return;
                TelemetryAccumulator.FlushFullBackfill();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] OnSpawned backfill failed: {ex.Message}");
            }
        }
    }

    #endregion
}
