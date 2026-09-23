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
        public const string PluginVersion = "1.2.0";

        public static ManualLogSource Log;
        public static ConfigEntry<bool> EnableReporting;
        public static ConfigEntry<float> FullSyncIntervalSeconds;
        public static ConfigEntry<KeyCode> TitlePanelKey;

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

            TitlePanelKey = Config.Bind(
                "General",
                "TitlePanelKey",
                KeyCode.None,
                "Key that opens the title panel (the same panel the /titles command opens). None = command only. " +
                "Read through the game's own input layer (ZInput), so it works with Valheim 1.0's input system."
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
        private int _onGuiThrows;

        private void Update()
        {
            TelemetryAccumulator.Tick(Time.deltaTime);
            Patches.TitlePicker.Tick(Time.deltaTime);
            Patches.TitlePanel.Tick();
        }

        // The one OnGUI in the mod: the title panel draws here and nowhere
        // else (ValkyriesCargo Core/CargoTick.cs's "one OnGUI" pattern — a
        // single draw call with its own throw cap, so a bad frame degrades
        // instead of spamming the log).
        private void OnGUI()
        {
            try
            {
                Patches.TitlePanel.Draw();
            }
            catch (System.Exception ex)
            {
                if (_onGuiThrows++ < 3) Plugin.Log?.LogError($"[{Plugin.PluginName}] OnGUI threw: {ex}");
            }
        }
    }
}
