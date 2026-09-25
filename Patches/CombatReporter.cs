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
        public const byte TitleRequest = 13;
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

    // Sub-operation for EventType.TitleRequest — mirrors TheRavensCall's
    // HandleTitleRequest switch (Saga.cs) exactly, wire-for-wire.
    public static class TitleOp
    {
        public const byte List = 1;
        public const byte Set = 2;
        public const byte Clear = 3;
    }

    #endregion

    #region Telemetry Sender

    internal static class TelemetrySender
    {
        private const string RpcNameV1 = "RavensCall_CombatReport_V1";
        private const string RpcNameV2 = "RavensCall_EventReport_V2";

        // Server -> client only; the client never sends on this name. Kept
        // here (not private) so the ZNet.Awake registration patch below and
        // TitlePicker can both reference the one literal.
        internal const string RpcNameTitleReply = "RavensCall_TitleReply_V1";

        // The one "may this report go out" gate, for every sender below and
        // for TitlePicker.Send. A server process reads Connected no matter
        // what (GetConnectionStatus returns Connected whenever IsServer(),
        // decomp/ZNet.cs:2954-2958), so through 1.2.x this check let any
        // server send, a dedicated one included. Since 1.3.0 the server case
        // is decided on its own: a game that hosts the world (a listen
        // server, or single player) reports, and a dedicated server never
        // does (IsDedicated() is false only in the game client's build,
        // decomp/ZNet.cs:2445-2448 is that build). A pure client reports
        // once it is connected, as before.
        //
        // The host needs no character of its own here. Between a death and
        // the respawn it has none (Game._RequestRespawn destroys it,
        // decomp/Game.cs:655-664, and UpdateRespawn spawns the next one,
        // :760-770), and a damage batch flushed in that window must still go
        // out: FlushCombatEntry clears the entry whether it was sent or not.
        // TheRavensCall 1.8.0+ takes such a report as the host's through the
        // host's open session, and refuses it without a word when there is
        // none. The senders that read the local player check for it
        // themselves, and /title asks for it (IsListenHost, TitlePicker.Send).
        internal static bool IsConnected()
        {
            if (ZNet.instance == null || ZRoutedRpc.instance == null) return false;
            if (ZNet.instance.IsServer()) return !ZNet.instance.IsDedicated();
            return ZNet.GetConnectionStatus() == ZNet.ConnectionStatus.Connected;
        }

        // A listen host (1.3.0): the game running the world with a player of
        // its own in it right now. A single-player world is the same case
        // with nobody else on. Its reports use the same routed RPCs with the
        // no-target overload, which on a server targets the routed
        // instance's own id (GetServerPeerID returns m_id,
        // decomp/ZRoutedRpc.cs:107-112) and so runs the handler in this
        // process and sends nothing on the network (:130-137).
        // TheRavensCall 1.8.0+ installed on the host takes such a report as
        // the host's own. Without TheRavensCall there is no handler, and
        // HandleRoutedRPC drops the call without a word (:189-197), so
        // nothing breaks. /title needs the character (its request carries
        // the character's name), so TitlePicker.Send asks for this; the
        // telemetry gate above does not. A dedicated server fails both of
        // the last two checks: see IsConnected, and a headless server never
        // spawns a local player.
        internal static bool IsListenHost()
        {
            return ZNet.instance != null &&
                   ZNet.instance.IsServer() &&
                   !ZNet.instance.IsDedicated() &&
                   Player.m_localPlayer != null;
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

        // Player-initiated title pick/list/clear request. Unlike every other
        // V2 sender above, this one isn't fed by a Harmony hook — it's called
        // directly from TitlePicker.HandleCommand — and it never dual-sends a
        // V1 packet: there is no V1 equivalent for this event type.
        public static void SendTitleRequest(byte op, string title)
        {
            try
            {
                if (!IsConnected() || Player.m_localPlayer == null) return;

                var pkg = new ZPackage();
                pkg.Write(2); // schemaVersion 2
                pkg.Write(EventType.TitleRequest);
                pkg.Write(Player.m_localPlayer.GetPlayerName());
                pkg.Write(op);
                pkg.Write(title ?? "");
                ZRoutedRpc.instance.InvokeRoutedRPC(RpcNameV2, pkg);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] SendTitleRequest failed: {ex.Message}");
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
                // Subtract the interval instead of resetting to zero, so this
                // clock and the full-sync clock below stop drifting apart by a
                // frame per tick (review 2026-09-15). A long hitch resets to
                // zero rather than firing a burst of catch-up flushes.
                _timer -= FlushIntervalSeconds;
                if (_timer >= FlushIntervalSeconds) _timer = 0f;

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
                _fullSyncTimer -= fullSyncInterval;
                if (_fullSyncTimer >= fullSyncInterval) _fullSyncTimer = 0f;
                // EnableReporting covers player stats too (its description),
                // and the spawn backfill already checks it; this clock sent
                // the snapshot regardless until 1.3.0. On a listen host that
                // snapshot also tells TheRavensCall 1.8.0 this crow reports
                // the host's damage taken, which it never would with
                // reporting off.
                if (!Plugin.EnableReporting.Value) return;
                // Pending deltas go out BEFORE the absolute snapshot. The
                // snapshot already contains everything accumulated so far; a
                // delta sent after it would be added on top by the server and
                // double count until the next snapshot (review 2026-09-15).
                if (_statDeltas.Count > 0) FlushStatDeltas();
                FlushStatSnapshot();
                FlushSkillSnapshot();
            }
        }

        // Public entry point for an out-of-band backfill send (player spawn),
        // independent of the 10s Tick() cadence — see Patch_PlayerOnSpawned.
        public static void FlushFullBackfill()
        {
            if (_statDeltas.Count > 0) FlushStatDeltas(); // same ordering rule as Tick
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

    #region Console routing

    // The server-only "ravenscall" command (TheRavensCall) cannot be typed
    // from a client unless the client's own Terminal knows the name: an
    // unknown command is "not a recognized command" locally and never leaves
    // the machine. This stub registers the name flagged onlyServer +
    // remoteCommand, so Terminal.TryRunCommand routes it through
    // ZNet.RemoteCommand to the server, where RPC_RemoteCommand checks the
    // admin list and TheRavensCall's real command runs (review 2026-09-15).
    // On a listen host TheRavensCall registers the real command on this same
    // method; Harmony order between the two is not fixed, so the stub stands
    // down if the name is already taken rather than assuming it loses.
    [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
    internal static class Patch_ConsoleRouting
    {
        private static void Postfix()
        {
            try
            {
                if (Terminal.commands != null && Terminal.commands.ContainsKey("ravenscall")) return;
                new Terminal.ConsoleCommand("ravenscall",
                    "Server admin command (TheRavensCall): ravenscall season start [name] | ravenscall season end | ravenscall title <player> [<title>|clear]. Runs on the server; needs admin.",
                    args => args.Context?.AddString("[WhereTheCrowFlies] ravenscall runs on the server; the output is in the server console."),
                    onlyServer: true, remoteCommand: true);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] console routing stub failed: {ex.Message}");
            }
        }
    }

    // The player-facing title commands. Both are normal (not onlyServer, not
    // remoteCommand, not isCheat) commands: Chat.InputText sends anything
    // starting with '/' through Terminal.TryRunCommand with the slash
    // stripped, so this same registration answers both `/title …` in chat and
    // `title …` at the F5 console (decomp/Chat.cs InputText, decomp/Terminal.cs
    // TryRunCommand) — and the same for `/titles` / `titles`. Registered in
    // the same InitTerminal postfix as the ravenscall stub above, each
    // guarded independently so one being claimed by a foreign mod never
    // stops the other from registering.
    [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
    internal static class Patch_TitleCommand
    {
        // InitTerminal's body self-guards with a static flag, but the Harmony
        // postfix still runs on every call (Console and Chat both derive from
        // Terminal, and Chat is recreated per world session), so from the
        // second call onward Terminal.commands already contains these names —
        // registered by this same patch, not a foreign mod. Track our own
        // registrations instead of inferring them from the dictionary, so the
        // warnings below only fire for a genuine foreign owner (review
        // 2026-09-22).
        private static bool _registered;
        private static bool _titlesRegistered;

        private static void Postfix()
        {
            try
            {
                if (!_registered)
                {
                    if (Terminal.commands != null && Terminal.commands.ContainsKey("title"))
                    {
                        Plugin.Log?.LogWarning("[WhereTheCrowFlies] a 'title' console command is already registered by another mod; the title picker will not run.");
                    }
                    else
                    {
                        new Terminal.ConsoleCommand("title",
                            "Pick which of your earned titles the server shows with your name in its Discord narration, Chronicle log and web dashboard (TheRavensCall 1.7.0+), and under your name on other players' screens (TheRavensCall 1.8.0+, for players running WhereTheCrowFlies 1.3.0+): title (list yours) | title <name> | title clear",
                            TitlePicker.HandleCommand,
                            optionsFetcher: TitlePicker.GetTabOptions,
                            alwaysRefreshTabOptions: true);
                        _registered = true;
                    }
                }

                if (!_titlesRegistered)
                {
                    if (Terminal.commands != null && Terminal.commands.ContainsKey("titles"))
                    {
                        Plugin.Log?.LogWarning("[WhereTheCrowFlies] a 'titles' console command is already registered by another mod; the title panel will not open from chat/console (the TitlePanelKey config still will).");
                    }
                    else
                    {
                        new Terminal.ConsoleCommand("titles",
                            "Open the title panel (TheRavensCall 1.7.0+): pick which earned title the server shows with your name.",
                            args =>
                            {
                                try
                                {
                                    TitlePanel.Open(args.Context);
                                }
                                catch (Exception ex)
                                {
                                    Plugin.Log?.LogWarning($"[WhereTheCrowFlies] titles command failed: {ex.Message}");
                                    TitlePanel.Close("error");
                                }
                            });
                        _titlesRegistered = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] title command registration failed: {ex.Message}");
            }
        }
    }

    #endregion

    #region Title Picker

    // Registers the server -> client title reply once per world session.
    // ZNet.Awake is where TheRavensCall itself registers its own routed RPCs
    // (Saga.cs ~400, "Per-world-session registration — ZRoutedRpc.instance is
    // new every time ZNet.Awake runs"): decomp/ZNet.cs Awake() shows
    // `m_routedRpc = new ZRoutedRpc(m_isServer)` is recreated on every Awake,
    // on both the client and the server/listen host, so a postfix here fires
    // exactly once per fresh ZRoutedRpc instance and never collides with a
    // stale registration from a previous session (ZRoutedRpc.Register uses
    // Dictionary.Add, which throws on a genuine duplicate key on the *same*
    // instance — a new instance each Awake avoids that).
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    internal static class Patch_RegisterTitleReply
    {
        private static void Postfix()
        {
            try
            {
                ZRoutedRpc.instance?.Register<ZPackage>(TelemetrySender.RpcNameTitleReply, TitlePicker.RPC_OnTitleReply);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] title reply RPC registration failed: {ex.Message}");
            }
        }
    }

    // Client side of the title picker: sends the `title` console command as
    // a TitleRequest (EventType 13) and prints TheRavensCall's
    // RavensCall_TitleReply_V1 answer back into whichever Terminal the
    // player typed in. Silent no-op against a server that doesn't answer
    // (older TheRavensCall, or a vanilla server) apart from the one 5-second
    // hint below — same "safe everywhere" contract as every other event type.
    internal static class TitlePicker
    {
        private const float ReplyTimeoutSeconds = 5f;

        // Only one request is ever pending: a new command overwrites both of
        // these, which is exactly "a new one restarts the timer" from the spec.
        private static Terminal _pendingContext;
        private static float _pendingSince = -1f;

        private static List<string> _tabOptions = new List<string>();

        // Factored out of HandleCommand (review 2026-09-22, SPEC-title-ui.md)
        // so the title panel (Patches/TitlePanel.cs) can send the exact same
        // connectivity-checked, timer-armed request a typed `title` command
        // sends, without duplicating either the "not connected" local line or
        // the pending-timeout bookkeeping. `context` is remembered as where
        // the reply should print (a chat box, an F5 console, or null for a
        // key-opened panel — see Print below). On failure the local line is
        // already printed (and echoed into the panel if it is open — see
        // Print/OnLocal) before this returns false.
        internal static bool Send(byte op, string title, Terminal context)
        {
            _pendingContext = context;

            if (!TelemetrySender.IsConnected())
            {
                // Not in a world yet (or a dedicated server). A world this
                // game hosts itself (a listen server, or single player) sends
                // like any client since 1.3.0: the request is handled in this
                // same process, and TheRavensCall 1.8.0+ installed here
                // answers it (see TelemetrySender.IsListenHost).
                const string msg = "Titles are kept by the server; join a server running TheRavensCall 1.7.0 or newer, or host your world with TheRavensCall 1.8.0 or newer installed.";
                Print(context, msg);
                if (TitlePanel.IsOpen) TitlePanel.OnLocal(msg);
                return false;
            }

            if (ZNet.instance.IsServer() && !TelemetrySender.IsListenHost())
            {
                // This game hosts the world, but its character is not in it
                // right now (between a death and the respawn, or still
                // loading in). The request carries the character's name, so
                // there is nothing to send, and the line above would tell the
                // host to set up what is already running. A pure client is
                // left as before.
                const string msg = "Your character is not in the world right now; try again once you have respawned.";
                Print(context, msg);
                if (TitlePanel.IsOpen) TitlePanel.OnLocal(msg);
                return false;
            }

            // Armed BEFORE the send: on a listen host TheRavensCall answers
            // inside this very call (the request and the reply are both
            // handled in this process), and that reply clears the timer.
            // Arming it afterwards would bring the "no answer" hint 5 seconds
            // after a reply that already came.
            _pendingSince = Time.realtimeSinceStartup;
            TelemetrySender.SendTitleRequest(op, title);
            return true;
        }

        public static void HandleCommand(Terminal.ConsoleEventArgs args)
        {
            try
            {
                byte op;
                string title = "";
                if (args.Length < 2)
                {
                    op = TitleOp.List;
                }
                else if (string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
                {
                    op = TitleOp.Clear;
                }
                else
                {
                    op = TitleOp.Set;
                    title = (args.ArgsAll ?? "").Trim(); // multi-word titles, e.g. "title Wolf Hunter"
                }

                Send(op, title, args.Context);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] title command failed: {ex.Message}");
            }
        }

        public static void RPC_OnTitleReply(long sender, ZPackage pkg)
        {
            try
            {
                // Routed RPCs are relayed client-to-client by the server without
                // checking the method name, so any modded peer could target
                // another player's client with a forged reply. Only accept this
                // packet from the server itself, checked before anything else so
                // a spoofed packet cannot even suppress the real timeout (review
                // 2026-09-22). The rule is NameplateTitles.FromServer, shared
                // with the title sync: on a pure client the server's routed id
                // is ZDOMan.GetSessionID(), the same value it sends as its uid
                // in PeerInfo, so GetServerPeer().m_uid is the right thing to
                // compare against (decomp/ZNet.cs Awake + SendPeerInfo). On a
                // listen host (1.3.0) the reply is TheRavensCall's, in this
                // same process: sent to the host's own uid, it is handled
                // locally with sender == ZNet.GetUID() (decomp/ZRoutedRpc.cs
                // :123-132), and GetServerPeer() is null on a server
                // (decomp/ZNet.cs:3005-3009). Vanilla routing does
                // carry m_senderPeerID inside the packet and relays it unchanged,
                // so a peer that hand-crafts the whole RoutedRPCData can still
                // claim the server's id; all that buys is one printed line under
                // this prefix — the same reach as a chat message — because this
                // handler only prints the line and repaints the player's own title
                // panel / tab-completion list — it never writes game state or sends
                // anything back.
                if (!NameplateTitles.FromServer(sender)) return;

                _pendingSince = -1f; // any reply — even one we fail to parse below — cancels the pending timer
                if (pkg == null || pkg.Size() == 0)
                {
                    if (TitlePanel.IsOpen) TitlePanel.OnLocal("The server answered, but its reply could not be read.");
                    return;
                }

                int schemaVersion = pkg.ReadInt();
                if (schemaVersion != 1)
                {
                    if (TitlePanel.IsOpen) TitlePanel.OnLocal("The server answered, but its reply could not be read.");
                    return;
                }

                byte kind = pkg.ReadByte(); // 1 = ok/informational, 2 = refused — both print the same way
                string text = pkg.ReadString();

                // Schema 1 grew a trailing earned-title list + active title
                // (SPEC-title-ui.md: count, title x count, active) so the
                // panel is fresh after every op with no second round trip.
                // Every reply TheRavensCall 1.7.0+ sends carries it, but this
                // stays tolerant of the package simply ending after `text` —
                // a reply from anything else registered under this RPC name
                // is then treated as carrying no list, rather than thrown on.
                List<string> titles = null;
                string active = null;
                if (pkg.GetPos() < pkg.Size())
                {
                    int count = Mathf.Clamp(pkg.ReadInt(), 0, 256); // defensive clamp; the server's real count is a handful
                    titles = new List<string>(count);
                    for (int i = 0; i < count; i++) titles.Add(pkg.ReadString());
                    active = pkg.GetPos() < pkg.Size() ? pkg.ReadString() : "";
                }

                Print(_pendingContext, text);
                // The structured list replaces the old "Your titles:" text
                // parse for tab completion too; a reply with no list (the
                // tolerant fallback above) leaves the existing cache alone
                // rather than clearing it.
                if (titles != null) _tabOptions = titles;
                TitlePanel.OnReply(kind, text, titles, active);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] title reply handling failed: {ex.Message}");
                if (TitlePanel.IsOpen) TitlePanel.OnLocal("The server answered, but its reply could not be read.");
            }
        }

        // Called from TelemetryTicker.Update (Plugin.cs) every frame.
        public static void Tick(float dt)
        {
            try
            {
                if (_pendingSince < 0f) return;
                if (Time.realtimeSinceStartup - _pendingSince < ReplyTimeoutSeconds) return;

                _pendingSince = -1f;
                // On a world this game hosts, the answer would come from this
                // same process, and only TheRavensCall 1.8.0+ answers its host
                // (1.7.x ignores a request that no connection delivered).
                bool hosting = ZNet.instance != null && ZNet.instance.IsServer();
                string hint = hosting
                    ? "No answer — a world you host needs TheRavensCall 1.8.0 or newer installed, with AcceptClientReports enabled."
                    : "No answer from the server — it needs TheRavensCall 1.7.0 or newer, with AcceptClientReports enabled.";
                Print(_pendingContext, hint);
                if (TitlePanel.IsOpen) TitlePanel.OnLocal(hint);
            }
            catch (Exception ex)
            {
                _pendingSince = -1f;
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] title timeout hint failed: {ex.Message}");
            }
        }

        public static List<string> GetTabOptions()
        {
            return _tabOptions;
        }

        private static void Print(Terminal context, string text)
        {
            string line = "[WhereTheCrowFlies] " + text;
            // `context != null` is Unity's overload: a Chat destroyed by a world
            // reload reads as null and falls through to the live instance.
            if (context == null) context = (Terminal)Chat.instance ?? Console.instance;
            if (context == null) return;
            context.AddString(line);
            // Chat.Update hides the window m_hideDelay (10 s) after the last
            // message; Terminal.AddString does not reset that clock, so a
            // reply landing after the window hid would sit unseen until the
            // next chat line. Same reset OnNewChatMessage does (decomp/Chat.cs).
            if (context is Chat chat) chat.m_hideTimer = 0f;
        }
    }

    #endregion

    #region Harmony Patches: Combat & Deaths

    // Character.OnDeath is virtual and Player overrides it WITHOUT calling
    // base (Player.OnDeath in the 1.0.12 decompile), so a patch on
    // Character.OnDeath only ever runs for creatures. The player branch that
    // used to live in this Prefix never fired once: no player death was ever
    // reported to the server (review 2026-09-15). Player deaths now have
    // their own target, Patch_PlayerDeath below.
    [HarmonyPatch(typeof(Character), "OnDeath")]
    internal static class Patch_CharacterDeath
    {
        private static void Prefix(Character __instance)
        {
            try
            {
                if (!Plugin.EnableReporting.Value) return;
                if (__instance == null || !__instance.IsOwner()) return;
                if (__instance is Player) return; // Patch_PlayerDeath (unreachable here anyway, see above)

                Vector3 pos = __instance.transform.position;
                string biome = TelemetrySender.GetBiomeName(pos);

                // Creature Died
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

        internal static string ComputeDeathCause(Player victim, out string killerPlayer)
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

    // The player half of the death report. Player.OnDeath is the method that
    // actually runs when a player dies; it returns early on every client but
    // the owner's, so the victim's own client is the only reporter.
    [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
    internal static class Patch_PlayerDeath
    {
        private static void Prefix(Player __instance)
        {
            try
            {
                if (!Plugin.EnableReporting.Value) return;
                if (__instance == null || !__instance.IsOwner()) return;

                Vector3 pos = __instance.transform.position;
                string biome = TelemetrySender.GetBiomeName(pos);
                string cause = Patch_CharacterDeath.ComputeDeathCause(__instance, out string killerPlayer);
                TelemetrySender.SendDeath(__instance.GetPlayerName(), cause, pos, killerPlayer, biome);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] Player.OnDeath report failed: {ex.Message}");
            }
        }
    }

    // Damage is counted in two places, one per side of the hit (review
    // 2026-09-24). Character.Damage does NOT run on the target's ZDO owner:
    // it runs on the machine that produced the hit (Attack.DoMeleeAttack,
    // Aoe and Projectile all call it on the attacker's client), sets the weak
    // spot and forwards the hit to the owner as RPC_Damage. The owner then
    // applies armor, resistances, the block, the difficulty scale and the
    // PvP/dodge/dead rejections, and ApplyDamage takes the health off.
    // The old single prefix on Character.Damage gated on IsOwner(), so it only
    // counted hits where the attacker also owned the target: PvP was always 0,
    // and in a group most creature damage (dealt and taken) was lost.
    //
    //  - Dealt: Character.Damage on the attacker's own client, for hits whose
    //    attacker is the local player. This is the damage the hit carries
    //    before the target's armor and resistances (the attacker's machine
    //    cannot see those); hits the owner would reject outright (a dead
    //    target, a player with PvP off) are left out.
    //  - Taken: Character.ApplyDamage on the local player's own client (a
    //    player always owns their own character), i.e. after armor, the block
    //    and the rest, and including burning/poison ticks.
    //
    // Every batch is a self-report: TheRavensCall only credits a batch whose
    // name is the sending peer's own character (VerifySelf), so neither side
    // may count damage for anyone but the local player.
    //
    // On a listen host (1.3.0) both sides count every hit of the host
    // player's own, the same as for any client (see Patch_RPCDamageSender
    // for why nothing is left out there). TheRavensCall 1.8.0 ignores the
    // damage dealt in the host's batches: it credits the host's damage
    // dealt from its own Character.Damage patch, which sees every hit the
    // host deals.
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    internal static class Patch_Damage
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            try
            {
                if (!Plugin.EnableReporting.Value || hit == null || __instance == null) return;
                var local = Player.m_localPlayer;
                if (local == null || __instance == local) return;

                var attacker = hit.GetAttacker();
                if (attacker == null || attacker != local) return;

                // Hits the owner's RPC_Damage throws away without applying.
                if (__instance.IsDead() || __instance.GetHealth() <= 0f) return;
                bool victimIsPlayer = __instance is Player;
                if (victimIsPlayer && !hit.m_ignorePVP && !__instance.IsPVPEnabled()) return;

                float totalDamage = hit.GetTotalDamage();
                if (totalDamage <= 0f) return;

                string attName = local.GetPlayerName();
                if (victimIsPlayer)
                    TelemetryAccumulator.AddCombatDamage(attName, 0f, totalDamage, 0f, 0f, 0f); // PvP dealt
                else
                    TelemetryAccumulator.AddCombatDamage(attName, totalDamage, 0f, 0f, 0f, 0f); // PvE dealt
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] Damage report failed: {ex.Message}");
            }
        }
    }

    // Remembers which routed peer sent the hit RPC_Damage is applying right
    // now, so Patch_ApplyDamage can tell a hit the server's own process
    // produced. On a listen host that is the host player's swings and every
    // creature the host owns; TheRavensCall's own Character.Damage patch
    // already credits the victim's damage taken for those (Saga.cs
    // Patch_Damage, server process only), so counting them here too would
    // double them. A dedicated server produces no such hits in practice, and
    // the same rule is still right if it ever does.
    //
    // On the listen host itself (1.3.0) this is false for every hit, on
    // purpose. There the hits the host's process produces arrive with
    // sender == ZNet.GetUID() (Character.Damage -> ZNetView.InvokeRPC to the
    // owner, handled locally: decomp/Character.cs:2232-2239,
    // ZNetView.cs:331-334, ZRoutedRpc.cs:120-133), but TheRavensCall 1.8.0
    // splits the host's damage by direction (Saga.cs Patch_Damage). The
    // damage the host DEALS it always credits from its own Character.Damage
    // patch, and ignores in these batches. The damage the host TAKES it
    // takes from these batches once the host's WhereTheCrowFlies has sent
    // telemetry in that world session, and its patch then leaves the host
    // out as a victim, so every hit the host takes must be counted here, as
    // on any client; leaving the host's own hits out would lose them. Until
    // that first telemetry report (and with EnableReporting off, which sends
    // none) the patch stays the host's only source both ways.
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class Patch_RPCDamageSender
    {
        private static HitData _currentHit;
        private static long _currentSender;

        // RPC_Damage can nest: a block-charge shield's BlockAttack starts an
        // attack whose Character.Damage on a creature this machine owns runs
        // that creature's RPC_Damage synchronously, inside the outer one. So
        // the prefix saves the outer slot and the finalizer puts it back,
        // instead of clearing it, or the outer ApplyDamage would lose its
        // sender (fix review 2026-09-24).
        private static void Prefix(long sender, HitData hit, out KeyValuePair<HitData, long> __state)
        {
            __state = new KeyValuePair<HitData, long>(_currentHit, _currentSender);
            _currentHit = hit;
            _currentSender = sender;
        }

        private static Exception Finalizer(Exception __exception, KeyValuePair<HitData, long> __state)
        {
            _currentHit = __state.Key;
            _currentSender = __state.Value;
            return __exception;
        }

        internal static bool FromServerProcess(HitData hit)
        {
            if (hit == null || !ReferenceEquals(hit, _currentHit)) return false;
            if (ZNet.instance == null || ZNet.instance.IsServer()) return false; // listen host: see the class comment
            long serverPeer = ZNet.instance.GetServerPeer()?.m_uid ?? 0L;
            return serverPeer != 0L && _currentSender == serverPeer;
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    internal static class Patch_ApplyDamage
    {
        // ApplyDamage returns early without a word (dead, teleporting, in a
        // cutscene, 0.1 damage or less). It stores the hit in m_lastHit only
        // once it really takes the health off, so "m_lastHit became this hit"
        // is the signal that the damage landed.
        private static void Prefix(Character __instance, HitData hit, out bool __state)
        {
            __state = false;
            try
            {
                if (!Plugin.EnableReporting.Value || hit == null || __instance == null) return;
                if (__instance != Player.m_localPlayer) return;
                __state = !ReferenceEquals(__instance.m_lastHit, hit);
            }
            catch { }
        }

        private static void Postfix(Character __instance, HitData hit, bool __state)
        {
            try
            {
                if (!__state || !ReferenceEquals(__instance.m_lastHit, hit)) return;
                if (!(__instance is Player victim)) return;
                if (Patch_RPCDamageSender.FromServerProcess(hit)) return;

                float taken = hit.GetTotalDamage(); // after armor, block, resistances and the damage-taken rate
                if (taken <= 0f) return;

                string vicName = victim.GetPlayerName();
                var attacker = hit.GetAttacker();
                if (attacker is Player)
                    TelemetryAccumulator.AddCombatDamage(vicName, 0f, 0f, 0f, taken, 0f); // PvP taken
                else if (attacker != null)
                    TelemetryAccumulator.AddCombatDamage(vicName, 0f, 0f, taken, 0f, 0f); // from a creature
                else
                    TelemetryAccumulator.AddCombatDamage(vicName, 0f, 0f, 0f, 0f, taken); // environment, burning/poison ticks
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] Damage taken report failed: {ex.Message}");
            }
        }
    }

    // BlockAttack runs on the blocker's own client (inside the owner's
    // RPC_Damage). It applies the shield's damage modifiers and then
    // hit.BlockDamage(blocked), which scales the hit DOWN in place, so reading
    // the hit in the postfix gives the damage that got through, not what was
    // blocked (review 2026-09-24). The prefix keeps the blockable damage
    // before the block; the difference is what the block stopped. It also
    // returns true when the guard breaks (out of stamina or staggered) and
    // BlockDamage is skipped; such a hit stops nothing and is not a block.
    [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
    internal static class Patch_BlockAttack
    {
        private static void Prefix(HitData hit, out float __state)
        {
            __state = -1f;
            try { if (hit != null) __state = hit.GetTotalBlockableDamage(); } catch { }
        }

        private static void Postfix(Humanoid __instance, HitData hit, bool __result, float __state)
        {
            try
            {
                if (!Plugin.EnableReporting.Value || !__result || __state < 0f) return;
                if (!(__instance is Player player) || player != Player.m_localPlayer) return;

                var blocker = player.GetCurrentBlocker();
                if (blocker == null || hit == null) return;

                float blockedDamage = __state - hit.GetTotalBlockableDamage();
                if (blockedDamage <= 0f) return; // guard broken: nothing was stopped

                bool isParry = blocker.m_shared.m_timedBlockBonus > 1f && player.m_blockTimer != -1f && player.m_blockTimer < 0.25f;

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

    // DoCrafting has five early returns that leave m_craftRecipe set and
    // produce nothing (max quality, missing requirements, inventory full,
    // missing DLC, upgrader resource missing), and it fires when the craft
    // timer completes, seconds after the click, so "inventory filled while
    // the bar ran" is ordinary play. The old Postfix counted every one of
    // those as a craft, and sent recipe.m_amount, ignoring multi-craft and
    // the station bonus (review 2026-09-15). Count the produced item at its
    // target quality before and after: the difference is the real amount,
    // and zero means nothing happened (or an upgrader failed or broke).
    [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
    internal static class Patch_Crafting
    {
        private sealed class CraftState
        {
            public string SharedName;
            public int TargetQuality;
            public int CountBefore;
            public bool IsUpgrade;
        }

        private static void Prefix(InventoryGui __instance, Player player, out CraftState __state)
        {
            __state = null;
            try
            {
                if (!Plugin.EnableReporting.Value || player == null || player != Player.m_localPlayer) return;
                if (__instance == null || __instance.m_craftRecipe == null || __instance.m_craftRecipe.m_item == null) return;
                var shared = __instance.m_craftRecipe.m_item.m_itemData.m_shared;
                bool isUpgrade = __instance.m_craftUpgradeItem != null;
                int target = isUpgrade ? __instance.m_craftUpgradeItem.m_quality + 1 : 1;
                __state = new CraftState
                {
                    SharedName = shared.m_name,
                    TargetQuality = target,
                    CountBefore = player.GetInventory().CountItems(shared.m_name, target),
                    IsUpgrade = isUpgrade,
                };
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] DoCrafting prefix failed: {ex.Message}");
            }
        }

        private static void Postfix(InventoryGui __instance, Player player, CraftState __state)
        {
            try
            {
                if (__state == null || player == null || __instance == null || __instance.m_craftRecipe == null) return;
                int produced = player.GetInventory().CountItems(__state.SharedName, __state.TargetQuality) - __state.CountBefore;
                if (produced <= 0) return;

                var recipe = __instance.m_craftRecipe;
                string itemName = recipe.m_item != null ? TelemetrySender.CleanName(recipe.m_item.gameObject.name) : "Unknown";
                byte action = __state.IsUpgrade ? CraftingAction.Upgraded : CraftingAction.Crafted;
                int amount = __state.IsUpgrade ? 1 : produced;
                string stationName = player.GetCurrentCraftingStation() != null ? TelemetrySender.CleanName(player.GetCurrentCraftingStation().gameObject.name) : "Hand";

                TelemetrySender.SendCrafting(player.GetPlayerName(), itemName, player.transform.position, action, __state.TargetQuality, amount, stationName);
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

    // Reported from the PICKER's own client, in Pickable.Interact, not from
    // the zone owner's RPC_Pick. RPC_Pick runs only on the client that owns
    // the pickable's zone, so the old prefix credited every berry another
    // player picked in that zone to the owner, and fired again for a bush
    // that was already picked (review 2026-09-15). Interact runs on the
    // interacting player's client and always ends in the RPC, so the pick is
    // attributed to the player who made it, and the server's self-report
    // binding accepts it. The amount is the formula RPC_Pick uses, minus the
    // skill bonus yield: Interact rolls it here on the picker and passes it
    // to RPC_Pick as a local, which no postfix can read (decompile
    // 71368-71381). A non-owner's m_picked only flips when the owner's
    // RPC_SetPicked reply lands, and Player.Update re-runs Interact every
    // 0.2 s while Use is held, so a per-instance two-second debounce keeps
    // one pick from being reported two to four times. Two seconds drops no
    // real event: a pickable respawns after minutes, or is destroyed.
    [HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact))]
    internal static class Patch_Pickable
    {
        private static readonly Dictionary<int, float> _lastPickReport = new Dictionary<int, float>();
        private const float PickDebounceSeconds = 2f;

        private static void Prefix(Pickable __instance, out bool __state)
        {
            // "Already picked" is read BEFORE the call: when the picker is
            // also the owner, RPC_Pick runs synchronously inside Interact and
            // flips m_picked before any Postfix could look at it.
            __state = __instance != null && __instance.m_picked;
        }

        private static void Postfix(Pickable __instance, Humanoid character, bool __state)
        {
            try
            {
                if (!Plugin.EnableReporting.Value || __instance == null || __state) return;
                if (!(character is Player player) || player != Player.m_localPlayer) return;
                // Mirror Interact's own early returns, which happen before the RPC.
                if (__instance.m_nview == null || !__instance.m_nview.IsValid() || __instance.m_enabled == 0) return;
                if (__instance.m_tarPreventsPicking)
                {
                    var floating = __instance.GetComponent<Floating>();
                    if (floating != null && floating.IsInTar()) return;
                }

                string itemName = __instance.GetHoverName();
                if (string.IsNullOrEmpty(itemName) && __instance.m_itemPrefab != null)
                {
                    itemName = TelemetrySender.CleanName(__instance.m_itemPrefab.name);
                }
                if (string.IsNullOrEmpty(itemName)) itemName = "Pickable";

                byte sourceType = __instance.m_harvestable ? HarvestSourceType.Crop : HarvestSourceType.Foraged;
                int amount = (__instance.m_dontScale || Game.instance == null || __instance.m_itemPrefab == null)
                    ? __instance.m_amount
                    : Mathf.Max(__instance.m_minAmountScaled, Game.instance.ScaleDrops(__instance.m_itemPrefab, __instance.m_amount));
                amount = Mathf.Max(1, amount);

                // Debounce per pickable instance (see the class comment).
                int key = __instance.GetInstanceID();
                float now = Time.realtimeSinceStartup;
                if (_lastPickReport.TryGetValue(key, out float last) && now - last < PickDebounceSeconds) return;
                foreach (var k in new List<int>(_lastPickReport.Keys))
                    if (now - _lastPickReport[k] > PickDebounceSeconds) _lastPickReport.Remove(k);
                _lastPickReport[key] = now;

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

    // InitiateSpawnBoss only ASKS the altar's owner to spawn (RPC_SpawnBoss);
    // the owner refuses when a spawn is already queued or there is no room,
    // and a client that does not own the altar cannot see the queue, so each
    // repeat press during the spawn delay used to count another summon
    // (review 2026-09-24). The owner answers the requester with
    // RPC_BossSpawnInitiated only after SpawnBoss really started, so that is
    // the one summon to report. m_interactUser is set by Interact/UseItem on
    // the requester's own machine just before the request.
    [HarmonyPatch(typeof(OfferingBowl), "RPC_BossSpawnInitiated")]
    internal static class Patch_OfferingBowl
    {
        private static void Prefix(OfferingBowl __instance)
        {
            try
            {
                if (!Plugin.EnableReporting.Value || __instance == null) return;
                var player = Player.m_localPlayer;
                if (player == null || __instance.m_interactUser != player) return;

                Vector3 point = __instance.transform.position;
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
