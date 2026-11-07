using UnityEngine;

namespace WeddingRun
{
    public enum Lang { Zh = 0, En = 1, Ja = 2 }

    /// <summary>
    /// Tiny localisation helper: every UI string is written inline as T(繁體中文, English, 日本語).
    /// The choice is saved in PlayerPrefs ("wr_lang"); first launch is Traditional Chinese.
    /// </summary>
    public static class Loc
    {
        public static Lang Current { get; private set; } = Lang.Zh;

        public static void Load()
        {
            int saved = PlayerPrefs.GetInt("wr_lang", -1);
            if (saved >= 0 && saved <= 2) { Current = (Lang)saved; return; }
            Current = Lang.Zh;   // first launch: Traditional Chinese
        }

        public static void Set(Lang lang)
        {
            Current = lang;
            PlayerPrefs.SetInt("wr_lang", (int)lang);
            PlayerPrefs.Save();
        }

        public static Lang Next => (Lang)(((int)Current + 1) % 3);

        /// <summary>Name of the current language, shown on the language button.</summary>
        public static string Label => T("中文", "English", "日本語");

        public static string T(string zh, string en, string ja)
        {
            switch (Current)
            {
                case Lang.En: return string.IsNullOrEmpty(en) ? zh : en;
                case Lang.Ja: return string.IsNullOrEmpty(ja) ? zh : ja;
                default: return zh;
            }
        }
    }
}
