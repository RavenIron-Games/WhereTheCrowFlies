using System;
using System.Collections.Generic;
using HarmonyLib;
using SharedUI;
using UnityEngine;

namespace WhereTheCrowFlies.Patches
{
    #region Title Panel

    // The in-game title picker (1.2.0): a small IMGUI window, drawn on the
    // family's shared gilt-frame theme (Libs/SharedUI/GiltFrameTheme.cs,
    // vendored byte-for-byte from ValkyriesCargo — see that file's own
    // header), that lists the player's earned titles as buttons and sets or
    // clears the active one over the same TitleRequest / RavensCall_TitleReply_V1
    // wire the `title` command already speaks (TitlePicker.Send, in
    // CombatReporter.cs). Opened by the `titles` console command (Patch_TitleCommand,
    // CombatReporter.cs) or the configurable Plugin.TitlePanelKey; closed by
    // Escape, the key again, a vanilla window opening over it, or the player
    // dying/disconnecting. See SPEC-title-ui.md ("Client / TitlePanel behaviour")
    // for the design this implements.
    internal static class TitlePanel
    {
        private static readonly Color Gold = new Color(0.80f, 0.62f, 0.26f, 1f);
        private static readonly int WindowId = "WhereTheCrowFlies_TitlePanel".GetHashCode();

        private static bool _open;
        private static int _gateUntilFrame = -1;
        private static bool _rectInitialised;
        private static Rect _rect;
        private static Vector2 _scroll;

        private static string _status = "";
        private static List<string> _titles = new List<string>();
        private static string _active = "";
        private static bool _haveList;
        private static Terminal _context;

        // The gate patch below (Patch_TitlePanelInputGate) reads this from
        // Update-phase code only — never from Draw/OnGUI — the same
        // discipline UIFocus's gotcha 3 demands (SPEC-title-ui.md, "Update
        // runs before OnGUI"), applied here directly rather than through
        // UIFocus itself: the panel needs exactly one gate (the sign
        // dialog's, via TextInput.IsVisible), not the cursor/input-block
        // token pair UIFocus exists to arbitrate between several windows.
        // Stays true for one extra frame after Close so the same ESC that
        // closed the panel cannot also open the pause menu in Menu.Update,
        // which may run after this class's Tick in the same frame (SPEC's
        // "ESC" bullet; MonoBehaviour Update order is undefined).
        public static bool GateActive => _open || Time.frameCount <= _gateUntilFrame;

        public static bool IsOpen => _open;

        public static void Open(Terminal context)
        {
            _context = context;
            _status = "Asking the server…";
            _haveList = false; // keep the previous list on screen until the reply lands
            _open = true;
            // TitlePicker.Send prints (and, since _open is already true here,
            // also echoes into this panel via OnLocal) its own "not
            // connected" line when it returns false — nothing further to do
            // here on that path; the panel simply shows that line with no
            // title buttons, because _haveList never becomes true.
            TitlePicker.Send(TitleOp.List, "", context);
        }

        public static void Close(string why)
        {
            if (!_open) return;
            _open = false;
            _gateUntilFrame = Time.frameCount + 1;
            Plugin.Log?.LogInfo($"[WhereTheCrowFlies] title panel closed: {why}");
        }

        // Called from TelemetryTicker.Update (Plugin.cs), every frame —
        // Update-phase, so the input gate above is always current before any
        // system that reads TextInput.IsVisible() runs this frame.
        public static void Tick()
        {
            try
            {
                KeyCode key = Plugin.TitlePanelKey.Value;
                if (key != KeyCode.None && ZInput.GetKeyDown(key))
                {
                    if (!_open)
                    {
                        // The game's own gate: TakeInput() reads false while
                        // chat/console/a menu/the inventory already has focus
                        // (decomp/Player.cs:2671), so the panel key does
                        // nothing on top of any of those instead of stealing
                        // the keystroke from them.
                        if (Player.m_localPlayer != null && Player.m_localPlayer.TakeInput()) Open(null);
                    }
                    else
                    {
                        Close("key");
                    }
                    return;
                }

                if (!_open) return;

                if (ZInput.GetKeyDown(KeyCode.Escape)) { Close("escape"); return; }
                if (InventoryGui.IsVisible() || Menu.IsVisible() || Minimap.IsOpen() || StoreGui.IsVisible()) { Close("a vanilla window opened"); return; }
                if (Player.m_localPlayer == null || Player.m_localPlayer.IsDead() || ZNet.instance == null) { Close("player gone"); return; }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] title panel tick failed: {ex.Message}");
                Close("error");
            }
        }

        // Called from TelemetryTicker.OnGUI (Plugin.cs), the mod's one OnGUI
        // (ValkyriesCargo Core/CargoTick.cs's "one OnGUI" pattern), itself
        // wrapped there in a 3-throw-capped try/catch; this method guards
        // itself too, matching every other public entry point in this file.
        public static void Draw()
        {
            if (!_open) return;
            try
            {
                GiltFrameTheme.EnsureBuilt(Gold);

                if (!_rectInitialised)
                {
                    float w = GiltFrameTheme.S(380f);
                    float h = Mathf.Min(GiltFrameTheme.S(480f), Screen.height - 40f);
                    _rect = new Rect(Mathf.Round((Screen.width - w) * 0.5f), Mathf.Round((Screen.height - h) * 0.5f), w, h);
                    _rectInitialised = true;
                }

                _rect = GUI.Window(WindowId, _rect, DrawWindow, GUIContent.none, GUIStyle.none);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] title panel draw failed: {ex.Message}");
                Close("error");
            }
        }

        private static void DrawWindow(int id)
        {
            Rect win = new Rect(0f, 0f, _rect.width, _rect.height);
            GiltFrameTheme.DrawWindow(win, "YOUR TITLES");

            Rect body = GiltFrameTheme.Body(win);
            GUILayout.BeginArea(body);
            GUILayout.BeginVertical();

            GUILayout.Label(_status, GiltFrameTheme.Note);
            GUILayout.Space(GiltFrameTheme.S(6f));

            if (_haveList && _titles.Count > 0)
            {
                _scroll = GUILayout.BeginScrollView(_scroll);
                foreach (string title in _titles)
                {
                    bool isActive = string.Equals(title, _active, StringComparison.Ordinal);
                    string label = isActive ? title + " ✓" : title;
                    if (GUILayout.Button(label, isActive ? GiltFrameTheme.Primary : GiltFrameTheme.Button) && !isActive)
                    {
                        TitlePicker.Send(TitleOp.Set, title, _context);
                        _status = "Asking the server…";
                    }
                }
                GUILayout.EndScrollView();

                GUILayout.Space(GiltFrameTheme.S(6f));
                GUI.enabled = _active != "";
                if (GUILayout.Button("No title", GiltFrameTheme.Button))
                {
                    TitlePicker.Send(TitleOp.Clear, "", _context);
                    _status = "Asking the server…";
                }
                GUI.enabled = true;
            }
            else if (_haveList)
            {
                GUILayout.Label("You have earned no titles yet — they come from creature kills and bosses.", GiltFrameTheme.Note);
            }

            GUILayout.Space(GiltFrameTheme.S(8f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Close", GiltFrameTheme.Button, GUILayout.Width(GiltFrameTheme.S(80f)))) Close("close button");
            GUILayout.Label("Esc closes · /title works in chat too", GiltFrameTheme.Note);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();

            // Nothing above changes _open except the Close button (a click,
            // which is fine to act on inside OnGUI — SPEC-title-ui.md); the
            // drag region is the title band only, same as the sibling panels.
            GUI.DragWindow(new Rect(0f, 0f, win.width, GiltFrameTheme.Band + GiltFrameTheme.TitleHeight));
        }

        // Called from TitlePicker.RPC_OnTitleReply (CombatReporter.cs) for
        // every reply — list, set, clear, even a refusal — so the panel is
        // always fresh after any op with no second round trip.
        public static void OnReply(byte kind, string text, List<string> titles, string active)
        {
            _status = text;
            _titles = titles;
            _active = active;
            _haveList = true;
        }

        // A local-only line (not connected, or the 5-second no-answer hint)
        // echoed in from TitlePicker while the panel is open; a server reply
        // always goes through OnReply instead.
        public static void OnLocal(string text)
        {
            if (!_open) return;
            _status = text;
        }
    }

    // Postfix on TextInput.IsVisible() — the sign-text dialog's static gate.
    // Read by exactly six game systems and nothing else (SPEC-title-ui.md,
    // "the input gate" bullet, grepped against decomp/full): Player.TakeInput
    // (decomp/Player.cs:2671 — movement, attacks, hotbar, the Tab/inventory
    // toggle Player.Update runs behind TakeInput()), PlayerController.TakeInput
    // (decomp/PlayerController.cs:228 — look), GameCamera.UpdateMouseCapture
    // (decomp/GameCamera.cs:191 — while the gate is up the cursor falls
    // through to the "else if (!Menu.IsVisible() …)" branch: LockState = None;
    // Show(); so the cursor frees WITHOUT the lock-then-unlock snap UIFocus's
    // header warns about), Menu.Update (decomp/Menu.cs:388 — ESC does not
    // open the pause menu while the gate is up), Minimap (the map key) and
    // Chat.Update (Enter does not open chat). Forcing this true while the
    // panel is open therefore makes the panel modal the same way the game's
    // own sign dialog is modal, with no patches needed on Player,
    // PlayerController or GameCamera themselves. TextInput.Update reads its
    // own m_visibleFrame FIELD, not IsVisible() (decomp/TextInput.cs), so
    // this postfix has no effect on the sign dialog itself.
    [HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
    internal static class Patch_TitlePanelInputGate
    {
        private static void Postfix(ref bool __result)
        {
            if (TitlePanel.GateActive) __result = true;
        }
    }

    #endregion
}
