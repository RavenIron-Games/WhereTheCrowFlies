using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;

namespace WhereTheCrowFlies.Patches
{
    #region Nameplate Titles

    // Other players' active TheRavensCall titles, on a line under their name
    // (1.3.0; needs TheRavensCall 1.8.0+ on the server). TheRavensCall
    // broadcasts RavensCall_TitleSync_V1 to everybody: the whole online table
    // every time a player joins, and one entry whenever a player's active
    // title changes (set, cleared, or a first title that activates itself).
    // This class keeps the latest title per player name; Patch_NameplateTitle
    // below draws it. HANDOFF.md §2.3b has the wire layout.
    //
    // Keyed by NAME, case-insensitively, the way TheRavensCall keys its own
    // records (its join hook runs before the joining character exists, so
    // there is no player id to send yet). The render looks a player up by
    // Player.GetPlayerName(), the raw name the server stores, not by
    // GetHoverName's censored copy (decomp/Player.cs:775).
    internal static class NameplateTitles
    {
        internal const string RpcName = "RavensCall_TitleSync_V1";

        // The same 64-character cap TheRavensCall puts on a title request
        // (HANDOFF.md §2.2, Event Type 13).
        internal const int MaxTitleLength = 64;

        // A packet names at most this many players. TheRavensCall caps its
        // table at 256; a larger count is not its packet and is ignored whole.
        private const int MaxEntries = 1024;

        // Bounds the cache itself. The server only names players it holds a
        // record for and a table only lists the players online, so a real
        // session stays far below this; the cap only matters against a forged
        // flood (see FromServer). Updates and removals of names already
        // cached still apply at the cap; only new names are skipped.
        private const int MaxNames = 1024;

        // Smaller than the name and a cool blue-grey, so the NAME stays the
        // loudest thing on the plate and this line reads apart from
        // Ragnarok's Wrath's gold one (#c9b458) under it.
        internal const string LineOpen = "\n<size=70%><color=#a8c0d8>";
        internal const string LineClose = "</color></size>";

        private static readonly Dictionary<string, string> _titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal static bool IsEmpty => _titles.Count == 0;

        // Called from Patch_RegisterTitleSync (ZNet.Awake): a new world
        // session starts from nothing, and the server sends this client the
        // full online table as soon as it joins.
        internal static void Reset()
        {
            _titles.Clear();
        }

        // The render's one lookup: no allocation, null when there is no title.
        internal static string TitleFor(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return null;
            return _titles.TryGetValue(playerName, out string title) ? title : null;
        }

        public static void RPC_OnTitleSync(long sender, ZPackage pkg)
        {
            try
            {
                if (!FromServer(sender)) return;
                if (pkg == null || pkg.Size() == 0) return;
                if (pkg.ReadInt() != 1) return; // schema 1 only: a later schema is ignored, not guessed at

                int count = pkg.ReadInt();
                if (count < 0 || count > MaxEntries) return;

                // Read the whole packet before touching the cache, so a
                // truncated one changes nothing instead of half the table.
                var entries = new List<KeyValuePair<string, string>>(count);
                for (int i = 0; i < count; i++)
                {
                    string name = pkg.ReadString();
                    string title = Clean(pkg.ReadString());
                    if (string.IsNullOrEmpty(name)) continue;
                    entries.Add(new KeyValuePair<string, string>(name, title));
                }

                foreach (var entry in entries)
                {
                    if (entry.Value.Length == 0) _titles.Remove(entry.Key); // "" = no active title
                    else if (_titles.Count < MaxNames || _titles.ContainsKey(entry.Key)) _titles[entry.Key] = entry.Value;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] title sync handling failed: {ex.Message}");
            }
        }

        // Only the server's own packets count, the rule
        // TitlePicker.RPC_OnTitleReply (CombatReporter.cs) applies to the
        // title reply: routed RPCs are relayed client-to-client by the server
        // without checking the method name, so any modded peer could
        // otherwise write titles under other players' names on this screen.
        //  - Pure client: the server's routed id is ZDOMan.GetSessionID(), the
        //    same value it sends as its uid in PeerInfo, so
        //    GetServerPeer().m_uid is the one to compare against
        //    (decomp/ZNet.cs SendPeerInfo, :997; the uid lands in peer.m_uid
        //    at :1197, before the routed peer is added at :1235).
        //  - Listen host (IsServer()): the packet is the host's own
        //    broadcast, which InvokeRoutedRPC dispatches locally with
        //    m_senderPeerID = the routed instance's own id
        //    (decomp/ZRoutedRpc.cs:123-132). ZNet.Awake sets that id to
        //    ZDOMan.GetSessionID() (decomp/ZNet.cs:396), which is exactly what
        //    ZNet.GetUID() returns (decomp/ZNet.cs:2163-2165).
        // As with the title reply, a peer that hand-crafts the whole routed
        // packet can still claim the server's id; all that reaches is a line
        // of cleaned text under a nameplate on this screen.
        private static bool FromServer(long sender)
        {
            if (ZNet.instance == null) return false;
            if (ZNet.instance.IsServer()) return sender == ZNet.GetUID();
            long serverPeer = ZNet.instance.GetServerPeer()?.m_uid ?? 0L;
            return serverPeer != 0L && sender == serverPeer;
        }

        // Pure: the title text that may reach a nameplate. The plate is
        // TextMeshPro rich text, and Localization.Localize runs over all of
        // it every frame (decomp/EnemyHud.cs:200), so these are dropped:
        //  - control characters: a newline or tab would break the plate apart;
        //  - U+2028 and U+2029 (line and paragraph separator): the plate's
        //    TextMeshProUGUI breaks a line on them too (GenerateTextMesh),
        //    and char.IsControl misses them (it only matches category Cc);
        //  - '<' and '>': TextMeshPro would read a tag, or print a broken one;
        //  - '$': Localize reads "$word" as a translation key and prints
        //    "[word]" when there is none (Localization.FindNextWord and
        //    Translate, assembly_guiutils);
        //  - '\': TextMeshPro turns "\n", "\t", "\\" and "\uXXXX" written as
        //    plain text into the characters themselves (TMP_Text, the escape
        //    switch in its text processing), so a backslash could bring any
        //    of the above back in.
        // Then trimmed and capped at 64 characters. "" = no title.
        internal static string Clean(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";

            var sb = new StringBuilder(title.Length);
            foreach (char c in title)
            {
                if (char.IsControl(c) || c == '\u2028' || c == '\u2029' || c == '<' || c == '>' || c == '$' || c == '\\') continue;
                sb.Append(c);
            }

            string clean = sb.ToString().Trim();
            if (clean.Length > MaxTitleLength)
            {
                int cut = MaxTitleLength;
                if (char.IsHighSurrogate(clean[cut - 1])) cut--; // never split a surrogate pair
                clean = clean.Substring(0, cut).TrimEnd();
            }
            return clean;
        }
    }

    // Registers the title sync on the same per-session seam as the title
    // reply (Patch_RegisterTitleReply, CombatReporter.cs): ZNet.Awake builds a
    // fresh ZRoutedRpc every world session, on the client and the server
    // alike, so registering here never collides with a stale registration,
    // and it is the right moment to drop the last session's titles. A sibling
    // postfix with its own try/catch, so a failure in either registration
    // never blocks the other.
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    internal static class Patch_RegisterTitleSync
    {
        private static void Postfix()
        {
            try
            {
                NameplateTitles.Reset();
                ZRoutedRpc.instance?.Register<ZPackage>(NameplateTitles.RpcName, NameplateTitles.RPC_OnTitleSync);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[WhereTheCrowFlies] title sync RPC registration failed: {ex.Message}");
            }
        }
    }

    // Draws the title. EnemyHud builds a nameplate for every OTHER player
    // within 10 m (never the local player's own, and not while they crouch)
    // and rewrites its text every frame from Localize(GetHoverName())
    // (decomp/EnemyHud.cs:54, :92, :115-121, :154, :200), so an append-only
    // postfix is the whole render: no UI objects, nothing to clean up.
    //
    // Ragnarok's Wrath appends its own world title to this same method, a
    // default-priority postfix under Harmony id com.raveniron.ragnarokswrath.
    // HarmonyBefore makes this postfix run first, so this title sits
    // directly under the name and RW's line under it: HarmonyX sorts
    // postfixes like every other patch kind, with HarmonyBefore as an
    // ordering edge that beats priority (PatchSorter), and emits them in that
    // order (HarmonyManipulator.WritePostfixes). The two mods share nothing
    // but this seam; without RW the attribute does nothing.
    //
    // The same text also reaches Humanoid's "$msg_cantuseon" message (using
    // an item on another player; decomp/Humanoid.cs:933, :960), as RW's line
    // already does. Accepted.
    [HarmonyPatch(typeof(Player), nameof(Player.GetHoverName))]
    [HarmonyBefore("com.raveniron.ragnarokswrath")]
    internal static class Patch_NameplateTitle
    {
        private static void Postfix(Player __instance, ref string __result)
        {
            try
            {
                if (NameplateTitles.IsEmpty || !Plugin.ShowNameplateTitles.Value) return;
                string title = NameplateTitles.TitleFor(__instance.GetPlayerName());
                if (title != null) __result = string.Concat(__result, NameplateTitles.LineOpen, title, NameplateTitles.LineClose);
            }
            catch
            {
                // Per-frame render path: a title that fails to draw must never
                // take the nameplate down. Vanilla's name is already in __result.
            }
        }
    }

    #endregion
}
