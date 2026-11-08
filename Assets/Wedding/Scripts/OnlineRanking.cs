using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace WeddingRun
{
    /// <summary>
    /// In-game online ranking — switched off. The wedding site keeps its own ranking (Firebase, fed by
    /// <see cref="Platform.SendScoreToPage"/>), and the Unity Gaming Services packages it needed added several MB
    /// to the WebGL download, so they were removed. The API stays so the (hidden) ranking UI still compiles.
    /// </summary>
    public static class OnlineRanking
    {
        public static string LeaderboardId = "highscores";
        public static bool Available => false;
        public static Task<bool> Init() => Task.FromResult(false);
        public static Task<double?> MyBest() => Task.FromResult<double?>(null);
        public static Task<bool> Submit(string name, double score) => Task.FromResult(false);
        public static Task<List<KeyValuePair<string, double>>> Top(int count) => Task.FromResult<List<KeyValuePair<string, double>>>(null);
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
