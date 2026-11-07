using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using UnityEngine;

namespace WeddingRun
{
    /// <summary>Unity Gaming Services leaderboard (standalone builds only — same rule as before).</summary>
    public static class OnlineRanking
    {
        public static string LeaderboardId = "highscores";
        static Task<bool> initTask;

        public static bool Available => !Platform.IsWebGL;

        public static Task<bool> Init()
        {
            if (!Available) return Task.FromResult(false);
            return initTask ??= InitInternal();
        }

        static async Task<bool> InitInternal()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ranking] init failed: " + e.Message);
                initTask = null;
                return false;
            }
        }

        public static async Task<double?> MyBest()
        {
            if (!await Init()) return null;
            try { return (await LeaderboardsService.Instance.GetPlayerScoreAsync(LeaderboardId)).Score; }
            catch { return null; }
        }

        public static async Task<bool> Submit(string name, double score)
        {
            if (!await Init()) return false;
            try
            {
                var meta = new Dictionary<string, string> { { "name", string.IsNullOrWhiteSpace(name) ? "Guest" : name.Trim() } };
                await LeaderboardsService.Instance.AddPlayerScoreAsync(LeaderboardId, score, new AddPlayerScoreOptions { Metadata = meta });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ranking] submit failed: " + e.Message);
                return false;
            }
        }

        [Serializable] class Meta { public string name; }

        public static async Task<List<KeyValuePair<string, double>>> Top(int count)
        {
            var list = new List<KeyValuePair<string, double>>();
            if (!await Init()) return null;
            try
            {
                var res = await LeaderboardsService.Instance.GetScoresAsync(LeaderboardId, new GetScoresOptions { Limit = count, IncludeMetadata = true });
                foreach (var e in res.Results)
                {
                    string n = "Guest";
                    if (!string.IsNullOrEmpty(e.Metadata))
                    {
                        var m = JsonUtility.FromJson<Meta>(e.Metadata);
                        if (m != null && !string.IsNullOrEmpty(m.name)) n = m.name;
                    }
                    list.Add(new KeyValuePair<string, double>(n, e.Score));
                }
                return list;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ranking] fetch failed: " + e.Message);
                return null;
            }
        }
    }

    public static class Platform
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        public const bool IsWebGL = true;
        [DllImport("__Internal")] static extern void GameOverAndSendScore(float score);
#else
        public const bool IsWebGL = false;
        static void GameOverAndSendScore(float score) { Debug.Log("[Web] would send score " + score); }
#endif
        /// <summary>Sends the final score to the hosting web page (Vue) — same jslib as before.</summary>
        public static void SendScoreToPage(float score)
        {
            try { GameOverAndSendScore(score); }
            catch (Exception e) { Debug.LogWarning(e.Message); }
        }
    }
}
