// Overcooked! 2 BepInEx plugin:
//   1. Spawn rotation: the host rotates which kitchen spawn positions players 2-4
//      take (P1 stays fixed). Change it with a hotkey while on the world map.
//   2. Star overlay: shows the 1/2/3 (and NG+ 4) star score thresholds and the
//      current score at the top of the screen during a level (F3 toggles).
//   3. Player stats: per-player table on the results screen (see PlayerStats.cs).
//
// The game runs Mono 2.0 (.NET 3.5 profile) and is built with the .NET Framework
// csc (C# 5), so this file avoids C# 6+ syntax.

using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace OC2Mods
{
    [BepInPlugin("oc2mods.spawnrotation.staroverlay", "OC2 QoL Mods", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance;

        // Rotation offset applied to players 2..N. Kept in memory for the play session.
        internal static int Rotation;

        private ConfigEntry<KeyCode> m_rotateKey;
        private ConfigEntry<KeyCode> m_rotateBackKey;
        private ConfigEntry<bool> m_autoRotate;
        private ConfigEntry<bool> m_showStars;
        private ConfigEntry<KeyCode> m_toggleStarsKey;
        private ConfigEntry<int> m_fontSize;

        private static Func<bool> s_isHost;
        private static Func<bool> s_isInSession;

        private float m_nextScan;
        private ServerWorldMapFlowController m_serverMap;
        private ClientCampaignFlowController m_campaignFlow;
        private float m_toastUntil;
        private GUIStyle m_style;
        private GUIStyle m_shadow;

        private void Awake()
        {
            Instance = this;
            m_rotateKey = Config.Bind("SpawnRotation", "RotateKey", KeyCode.F5,
                "Host only, on the world map: rotate spawn positions of players 2-4 forward.");
            m_rotateBackKey = Config.Bind("SpawnRotation", "RotateBackKey", KeyCode.F6,
                "Host only, on the world map: rotate spawn positions of players 2-4 backward.");
            m_autoRotate = Config.Bind("SpawnRotation", "AutoRotateEachLevel", false,
                "Automatically rotate players 2-4 one step every time a kitchen loads.");
            m_showStars = Config.Bind("StarOverlay", "Enabled", true,
                "Show star score thresholds at the top of the screen during a level.");
            m_toggleStarsKey = Config.Bind("StarOverlay", "ToggleKey", KeyCode.F3,
                "Toggle the star overlay on/off. The choice is saved to Enabled.");
            m_fontSize = Config.Bind("StarOverlay", "FontSize", 22, "Overlay font size.");

            PlayerStatsPanel panel = gameObject.AddComponent<PlayerStatsPanel>();
            panel.Enabled = Config.Bind("PlayerStats", "Enabled", true,
                "Show the per-player stats table on the level results screen.");
            panel.FontSize = m_fontSize;
            ClientKitchenLoader.OnChefsSetupComplete += PlayerStats.OnChefsSetup;
            HostStats.Init();

            // ConnectionStatus is internal to Assembly-CSharp.
            Type status = AccessTools.TypeByName("ConnectionStatus");
            s_isHost = AccessTools.MethodDelegate<Func<bool>>(AccessTools.Method(status, "IsHost"));
            s_isInSession = AccessTools.MethodDelegate<Func<bool>>(AccessTools.Method(status, "IsInSession"));

            new Harmony("oc2mods.spawnrotation.staroverlay").PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo("Loaded.");
        }

        // Same authority test the game uses: online host, or offline/local play.
        internal static bool HasAuthority()
        {
            return s_isHost() || !s_isInSession();
        }

        internal static void Log(string message)
        {
            if (Instance != null)
            {
                Instance.Logger.LogInfo(message);
            }
        }

        internal static bool AutoRotate
        {
            get { return Instance != null && Instance.m_autoRotate.Value; }
        }

        private void Update()
        {
            if (Time.unscaledTime >= m_nextScan)
            {
                m_nextScan = Time.unscaledTime + 0.5f;
                m_serverMap = FindObjectOfType<ServerWorldMapFlowController>();
                m_campaignFlow = FindObjectOfType<ClientCampaignFlowController>();
            }

            if (Input.GetKeyDown(m_toggleStarsKey.Value))
            {
                m_showStars.Value = !m_showStars.Value;
                Log("Star overlay " + (m_showStars.Value ? "on" : "off"));
            }

            if (m_serverMap != null && HasAuthority())
            {
                int step = 0;
                if (Input.GetKeyDown(m_rotateKey.Value)) step = 1;
                if (Input.GetKeyDown(m_rotateBackKey.Value)) step = -1;
                if (step != 0)
                {
                    Rotation += step;
                    m_toastUntil = Time.unscaledTime + 4f;
                    Log("Spawn rotation now " + Rotation + ": " + DescribeOrder());
                }
            }
        }

        // Player order as it will be applied in the next kitchen.
        private static string DescribeOrder()
        {
            FastList<User> users = ServerUserSystem.m_Users;
            int n = users.Count;
            if (n < 3)
            {
                return "need 3+ players to rotate";
            }
            string[] bySpawn = new string[n];
            for (int i = 0; i < n; i++)
            {
                bySpawn[SpawnRotation.SpawnIndexFor(i, n, Rotation)] = users._items[i].DisplayName;
            }
            StringBuilder sb = new StringBuilder();
            for (int s = 0; s < n; s++)
            {
                if (s > 0) sb.Append("   ");
                sb.Append("Spawn ").Append(s + 1).Append(": ").Append(bySpawn[s]);
            }
            return sb.ToString();
        }

        private void EnsureStyles()
        {
            if (m_style != null && m_style.fontSize == m_fontSize.Value) return;
            m_style = new GUIStyle(GUI.skin.label);
            m_style.fontSize = m_fontSize.Value;
            m_style.fontStyle = FontStyle.Bold;
            m_style.alignment = TextAnchor.UpperCenter;
            m_style.normal.textColor = Color.white;
            m_shadow = new GUIStyle(m_style);
            m_shadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        }

        private void DrawText(float y, string text)
        {
            Rect r = new Rect(0f, y, Screen.width, m_style.fontSize * 2f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, m_shadow);
            GUI.Label(r, text, m_style);
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (m_serverMap != null && HasAuthority())
            {
                string hint = "[" + m_rotateKey.Value + "/" + m_rotateBackKey.Value + "] Rotate spawns";
                if (Time.unscaledTime < m_toastUntil)
                {
                    DrawText(8f, DescribeOrder());
                }
                else
                {
                    DrawText(8f, hint + (Rotation != 0 ? "  (rotation " + Rotation + ")" : ""));
                }
                return;
            }

            if (m_showStars.Value && m_campaignFlow != null && m_campaignFlow.InRound)
            {
                string line = StarLine();
                if (line != null)
                {
                    DrawText(8f, line);
                }
            }
        }

        private string StarLine()
        {
            GameSession session = GameUtils.GetGameSession();
            if (session == null || session.LevelSettings == null) return null;
            SceneDirectoryData.PerPlayerCountDirectoryEntry entry = session.LevelSettings.SceneDirectoryVarientEntry;
            if (entry == null || entry.OneStarScore <= 0) return null;

            ClientTeamMonitor monitor = m_campaignFlow.GetMonitorForTeam(TeamID.One);
            int score = monitor != null && monitor.Score != null ? monitor.Score.GetTotalScore() : 0;

            bool ngp = false;
            try
            {
                GameProgress.GameProgressData save = session.Progress.SaveData;
                ngp = save.NewGamePlusDialogShown && save.IsNGPEnabledForLevel(GameUtils.GetLevelID());
            }
            catch (Exception)
            {
                // Arcade/online levels may have no campaign progress entry.
            }

            StringBuilder sb = new StringBuilder();
            AppendStar(sb, 1, entry.OneStarScore, score);
            AppendStar(sb, 2, entry.TwoStarScore, score);
            AppendStar(sb, 3, entry.ThreeStarScore, score);
            if (ngp && entry.FourStarScore > 0)
            {
                AppendStar(sb, 4, entry.FourStarScore, score);
            }
            sb.Append("      Score: ").Append(score);
            return sb.ToString();
        }

        private static void AppendStar(StringBuilder sb, int stars, int threshold, int score)
        {
            if (sb.Length > 0) sb.Append("    ");
            sb.Append(score >= threshold ? "✓ " : "").Append(stars).Append('★').Append(' ').Append(threshold);
        }
    }

    internal static class SpawnRotation
    {
        // User 0 keeps spawn 0; users 1..n-1 rotate among spawns 1..n-1.
        internal static int SpawnIndexFor(int userIndex, int userCount, int rotation)
        {
            if (userIndex == 0 || userCount < 3) return userIndex;
            int others = userCount - 1;
            int r = ((rotation % others) + others) % others;
            return 1 + (userIndex - 1 + r) % others;
        }
    }

    // Host-authoritative: the server assigns chef entities to users, then broadcasts
    // ChefOwnership. Clients bind controls and chef skins from each user's EntityID,
    // so only the host needs this plugin.
    [HarmonyPatch(typeof(CampaignKitchenLoaderManager), "AssignChefEntities")]
    internal static class AssignChefEntitiesPatch
    {
        private static bool Prefix(FastList<User> users)
        {
            int n = users.Count;
            if (n < 3)
            {
                return true;
            }
            if (Plugin.AutoRotate)
            {
                Plugin.Rotation++;
            }
            if (Plugin.Rotation % (n - 1) == 0)
            {
                return true;
            }

            PlayerIDProvider[] providers = PlayerIDProvider.s_AllProviders.ToArray();
            if (providers.Length < n)
            {
                Plugin.Log("Kitchen has " + providers.Length + " chefs for " + n + " users; not rotating.");
                return true;
            }
            Array.Sort(providers, (PlayerIDProvider x, PlayerIDProvider y) => x.GetID() - y.GetID());

            uint[] ids = new uint[n];
            for (int i = 0; i < n; i++)
            {
                EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(providers[SpawnRotation.SpawnIndexFor(i, n, Plugin.Rotation)].gameObject);
                if (entry == null)
                {
                    Plugin.Log("Chef entity not registered; not rotating.");
                    return true;
                }
                ids[i] = entry.m_Header.m_uEntityID;
            }
            for (int i = 0; i < n; i++)
            {
                users._items[i].EntityID = ids[i];
            }
            Plugin.Log("Applied spawn rotation " + Plugin.Rotation + " for " + n + " users.");
            return false;
        }
    }
}
