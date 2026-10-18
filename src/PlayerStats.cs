// Per-player stats on the results screen.
//
// The host already broadcasts per-chef stat increments to every client with
// ServerMessenger.Achievement(chef, statId, increment) (MessageType.Achievement).
// Unmodded clients pass them to StatSystem.IncStat, which ignores unknown ids, so the
// modded host adds delivery stats on the same message under unused ids. Modded
// clients tally every Achievement message per chef entity.
//
// Wire limits (AchievementMessage): statId is 10 bits, increment is 3 bits.

using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace OC2Mods
{
    internal static class StatIds
    {
        // Built-in game stats, sent by the host for every chef.
        public const int Washed = 2;
        public const int Chopped = 5;
        public const int CaughtThrow = 12;
        public const int Binned = 14;
        public const int WaterGunWashed = 101;

        // Mod stats, sent only by a modded host.
        public const int Delivered = 1000;
        public const int WrongDish = 1001;
        public const int HostHandshake = 1023;

        // Sent as the handshake increment (3 bits: 1..7).
        public const int Protocol = 1;

        public static readonly int[] Custom = { Delivered, WrongDish, HostHandshake };
    }

    internal enum Column
    {
        Delivered,
        WrongDish,
        Chopped,
        Washed,
        Binned,
        Caught,
        Count
    }

    internal static class PlayerStats
    {
        private struct Row
        {
            public string Name;
            public uint Entity;
            public uint Entity2;
        }

        private static readonly Dictionary<uint, int[]> s_byEntity = new Dictionary<uint, int[]>();
        private static readonly List<Row> s_rows = new List<Row>();

        // Protocol announced by the host this level; 0 = host not modded (or not announced yet).
        internal static int HostProtocol;

        internal static void OnChefsSetup()
        {
            s_byEntity.Clear();
            s_rows.Clear();
            HostProtocol = 0;
            FastList<User> users = ClientUserSystem.m_Users;
            for (int i = 0; i < users.Count; i++)
            {
                User u = users._items[i];
                Row row = new Row();
                row.Name = u.DisplayName;
                row.Entity = u.EntityID;
                row.Entity2 = u.Entity2ID;
                s_rows.Add(row);
            }
        }

        internal static void Record(uint entity, int statId, int increment)
        {
            if (statId == StatIds.HostHandshake)
            {
                HostProtocol = increment;
                if (increment != StatIds.Protocol)
                {
                    Plugin.Log("Host stats protocol " + increment + " differs from ours (" + StatIds.Protocol + ").");
                }
                return;
            }
            int column;
            switch (statId)
            {
                case StatIds.Delivered: column = (int)Column.Delivered; break;
                case StatIds.WrongDish: column = (int)Column.WrongDish; break;
                case StatIds.Chopped: column = (int)Column.Chopped; break;
                case StatIds.Washed:
                case StatIds.WaterGunWashed: column = (int)Column.Washed; break;
                case StatIds.Binned: column = (int)Column.Binned; break;
                case StatIds.CaughtThrow: column = (int)Column.Caught; break;
                default: return;
            }
            int[] counts;
            if (!s_byEntity.TryGetValue(entity, out counts))
            {
                counts = new int[(int)Column.Count];
                s_byEntity[entity] = counts;
            }
            counts[column] += increment;
        }

        internal static int RowCount
        {
            get { return s_rows.Count; }
        }

        internal static string RowName(int row)
        {
            return s_rows[row].Name;
        }

        internal static int Value(int row, Column column)
        {
            Row r = s_rows[row];
            return Get(r.Entity, column) + (r.Entity2 != r.Entity ? Get(r.Entity2, column) : 0);
        }

        private static int Get(uint entity, Column column)
        {
            int[] counts;
            if (entity == 0 || !s_byEntity.TryGetValue(entity, out counts)) return 0;
            return counts[(int)column];
        }
    }

    // Host-side sending of the mod stats.
    internal static class HostStats
    {
        private static Func<GameObject, int, int, bool> s_achievement;
        private static bool? s_idsFree;

        // Chef currently placing an item on a delivery station, for the duration of the call.
        internal static GameObject Placer;

        internal static void Init()
        {
            Type messenger = AccessTools.TypeByName("ServerMessenger");
            s_achievement = AccessTools.MethodDelegate<Func<GameObject, int, int, bool>>(
                AccessTools.Method(messenger, "Achievement", new[] { typeof(GameObject), typeof(int), typeof(int) }));
        }

        // Fail closed: send mod ids only once we've confirmed this game version doesn't define them,
        // because a defined id would bump a real stat for the local player on unmodded clients.
        private static bool IdsFree()
        {
            if (s_idsFree.HasValue) return s_idsFree.Value;
            StatSystem stats = UnityEngine.Object.FindObjectOfType<StatSystem>();
            if (stats == null) return false;
            MethodInfo find = AccessTools.Method(typeof(StatSystem), "FindStat");
            if (find == null)
            {
                s_idsFree = false;
                Plugin.Log("StatSystem.FindStat not found; delivery stats disabled.");
                return false;
            }
            foreach (int id in StatIds.Custom)
            {
                object[] args = { id, null };
                if ((bool)find.Invoke(stats, args))
                {
                    s_idsFree = false;
                    Plugin.Log("Stat id " + id + " is used by the game; delivery stats disabled.");
                    return false;
                }
            }
            s_idsFree = true;
            return true;
        }

        internal static void Send(GameObject chef, int statId, int increment)
        {
            if (chef == null || !Plugin.HasAuthority() || !IdsFree()) return;
            s_achievement(chef, statId, increment);
        }

        internal static void SendHandshake()
        {
            FastList<User> users = ServerUserSystem.m_Users;
            if (users.Count == 0) return;
            EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(users._items[0].EntityID);
            if (entry != null)
            {
                Send(entry.m_GameObject, StatIds.HostHandshake, StatIds.Protocol);
            }
        }
    }

    [HarmonyPatch(typeof(OvercookedAchievementManager), "OnAchievementRecieved")]
    internal static class AchievementReceivedPatch
    {
        private static void Postfix(Serialisable message)
        {
            AchievementMessage m = message as AchievementMessage;
            if (m != null)
            {
                PlayerStats.Record(m.m_Header.m_uEntityID, m.m_statId, m.m_increment);
            }
        }
    }

    [HarmonyPatch(typeof(ServerAttachStation), "HandlePlacement")]
    internal static class PlacementPatch
    {
        // __state restores the outer placer if placements nest.
        private static void Prefix(ServerAttachStation __instance, ICarrier _iCarrier, out GameObject __state)
        {
            __state = HostStats.Placer;
            if (_iCarrier != null && __instance.GetComponent<ServerPlateStation>() != null)
            {
                HostStats.Placer = _iCarrier.AccessGameObject();
            }
        }

        private static void Finalizer(GameObject __state)
        {
            HostStats.Placer = __state;
        }
    }

    [HarmonyPatch(typeof(ServerKitchenFlowControllerBase), "OnSuccessfulDelivery")]
    internal static class SuccessfulDeliveryPatch
    {
        private static void Postfix()
        {
            HostStats.Send(HostStats.Placer, StatIds.Delivered, 1);
        }
    }

    [HarmonyPatch(typeof(ServerKitchenFlowControllerBase), "OnFailedDelivery")]
    internal static class FailedDeliveryPatch
    {
        private static void Postfix()
        {
            HostStats.Send(HostStats.Placer, StatIds.WrongDish, 1);
        }
    }

    [HarmonyPatch(typeof(ServerFlowControllerBase), "StartFlow")]
    internal static class StartFlowPatch
    {
        private static void Postfix()
        {
            HostStats.SendHandshake();
        }
    }

    // Draws the stats table while the level results screen is up.
    internal class PlayerStatsPanel : MonoBehaviour
    {
        private static readonly string[] k_headers = { "Delivered", "Wrong dish", "Chopped", "Washed", "Binned", "Caught throws" };

        internal ConfigEntry<bool> Enabled;
        internal ConfigEntry<int> FontSize;

        private float m_nextScan;
        private StarRatingUIController m_results;
        private GUIStyle m_cell;
        private GUIStyle m_best;
        private GUIStyle m_head;
        private GUIStyle m_note;
        private Texture2D m_bg;

        private void Update()
        {
            if (Time.unscaledTime >= m_nextScan)
            {
                m_nextScan = Time.unscaledTime + 0.5f;
                m_results = FindObjectOfType<StarRatingUIController>();
            }
        }

        private void EnsureStyles()
        {
            int size = Mathf.Max(10, FontSize.Value - 4);
            if (m_cell != null && m_cell.fontSize == size) return;
            m_cell = new GUIStyle(GUI.skin.label);
            m_cell.fontSize = size;
            m_cell.alignment = TextAnchor.MiddleCenter;
            m_cell.normal.textColor = Color.white;
            m_best = new GUIStyle(m_cell);
            m_best.fontStyle = FontStyle.Bold;
            m_best.normal.textColor = new Color(1f, 0.85f, 0.2f);
            m_head = new GUIStyle(m_cell);
            m_head.fontStyle = FontStyle.Bold;
            m_head.normal.textColor = new Color(0.8f, 0.9f, 1f);
            m_note = new GUIStyle(m_cell);
            m_note.fontStyle = FontStyle.Italic;
            m_note.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            if (m_bg == null)
            {
                m_bg = new Texture2D(1, 1);
                m_bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
                m_bg.Apply();
            }
        }

        private void OnGUI()
        {
            if (!Enabled.Value || m_results == null || PlayerStats.RowCount == 0) return;
            EnsureStyles();

            int rows = PlayerStats.RowCount;
            bool hostModded = PlayerStats.HostProtocol > 0;
            float rowH = m_cell.fontSize * 1.8f;
            float nameW = m_cell.fontSize * 9f;
            float colW = m_cell.fontSize * 6.5f;
            float width = nameW + colW * k_headers.Length + 24f;
            float height = rowH * (rows + 1) + 16f + (hostModded ? 0f : rowH);
            float x = (Screen.width - width) / 2f;
            float y = Screen.height - height - 24f;

            GUI.DrawTexture(new Rect(x, y, width, height), m_bg);
            float cx = x + 12f;
            float cy = y + 8f;
            GUI.Label(new Rect(cx, cy, nameW, rowH), "Player", m_head);
            for (int c = 0; c < k_headers.Length; c++)
            {
                GUI.Label(new Rect(cx + nameW + colW * c, cy, colW, rowH), k_headers[c], m_head);
            }

            int[] best = new int[(int)Column.Count];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < best.Length; c++)
                {
                    best[c] = Mathf.Max(best[c], PlayerStats.Value(r, (Column)c));
                }
            }

            for (int r = 0; r < rows; r++)
            {
                float ry = cy + rowH * (r + 1);
                GUI.Label(new Rect(cx, ry, nameW, rowH), PlayerStats.RowName(r), m_cell);
                for (int c = 0; c < (int)Column.Count; c++)
                {
                    bool deliveryColumn = c == (int)Column.Delivered || c == (int)Column.WrongDish;
                    string text;
                    GUIStyle style = m_cell;
                    if (deliveryColumn && !hostModded)
                    {
                        text = "—";
                    }
                    else
                    {
                        int v = PlayerStats.Value(r, (Column)c);
                        text = v.ToString();
                        if (v > 0 && v == best[c] && rows > 1) style = m_best;
                    }
                    GUI.Label(new Rect(cx + nameW + colW * c, ry, colW, rowH), text, style);
                }
            }
            if (!hostModded)
            {
                GUI.Label(new Rect(x, cy + rowH * (rows + 1), width, rowH),
                    "Delivery stats need the host to have the mod.", m_note);
            }
        }
    }
}
