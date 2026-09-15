using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using WhereTheCrowFlies.Patches;

namespace WhereTheCrowFlies
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.raveniron.wherethecrowflies";
        public const string PluginName = "WhereTheCrowFlies";
        public const string PluginVersion = "1.1.3";

        public static ManualLogSource Log;
        public static ConfigEntry<bool> EnableReporting;
        public static ConfigEntry<float> FullSyncIntervalSeconds;

        private readonly Harmony _harmony = new Harmony(PluginGUID);

        private void Awake()
        {
            Log = Logger;

            EnableReporting = Config.Bind(
                "General",
                "EnableReporting",
                true,
                "Send owner-side combat reports, world telemetry, crafting, building, harvesting, consumption, and player stats to servers running TheRavensCall."
            );

            FullSyncIntervalSeconds = Config.Bind(
                "General",
                "FullSyncIntervalSeconds",
                300f,
                "How often (in seconds) to broadcast a full absolute snapshot of all vanilla player stats and skills (StatSnapshot/SkillSnapshot). " +
                "These are idempotent full-state resyncs, not time-sensitive deltas, so this can be turned up if 10-second combat/damage batching " +
                "feels like enough network chatter on its own — a full snapshot also always fires immediately on spawn regardless of this setting."
            );

            try
            {
                _harmony.PatchAll();
                Log.LogInfo($"[{PluginName}] All patches applied.");
            }
            catch (System.Exception ex)
            {
                Log.LogError($"[{PluginName}] Harmony PatchAll failed: {ex.Message}");
            }

            gameObject.AddComponent<TelemetryTicker>();
            Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
        }

        private void OnDestroy()
        {
            try
            {
                _harmony?.UnpatchSelf();
            }
            catch { }
        }
    }

    internal class TelemetryTicker : MonoBehaviour
    {
        private void Update()
        {
            TelemetryAccumulator.Tick(Time.deltaTime);
        }
    }
}
