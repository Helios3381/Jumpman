using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace WeddingRun.EditorTools
{
    /// <summary>
    /// Builds small *static* TextMeshPro font assets that contain only the characters the game actually uses
    /// (every string / char literal in Assets/Wedding/Scripts + ASCII). The dynamic CJK fonts drag the whole
    /// .TTC files (≈20 MB) into the WebGL download; these subsets are ≈1 MB and need no font file at runtime.
    /// They are regenerated automatically whenever the set of characters in the scripts changes.
    /// </summary>
    public static class FontSubsetter
    {
        const string ScriptDir = "Assets/Wedding/Scripts";
        const string OutDir = "Assets/Fonts/Subset";
        const string BoldPath = OutDir + "/MSJHBD Subset SDF.asset";
        const string JapanesePath = OutDir + "/BIZ-UDMINCHOM Subset SDF.asset";
        const string CharsetPath = OutDir + "/charset.txt";
        const string BoldSource = "Assets/Fonts/MSJHBD.TTC";
        const string JapaneseSource = "Assets/Fonts/BIZ-UDMINCHOM.TTC";

        // Same proportions as the original dynamic assets (90 pt / padding 9), a bit smaller so it all fits one atlas.
        const int PointSize = 64, Padding = 6;
        const string Extra = "…·、。，！？：；（）「」『』—～×→↑↓←★♥♡©・ー　";

        /// <summary>Returns (main font, Japanese fallback or null), regenerating them if needed.</summary>
        public static (TMP_FontAsset bold, TMP_FontAsset japanese) Ensure()
        {
            string chars = CollectCharacters();
            var bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldPath);
            var jp = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JapanesePath);
            bool upToDate = bold != null && File.Exists(CharsetPath) && File.ReadAllText(CharsetPath, Encoding.UTF8) == chars;
            if (upToDate) return (bold, jp);
            return Generate(chars);
        }

        [MenuItem("Tools/Wedding Run/Rebuild Font Subsets")]
        public static void RebuildMenu() => Generate(CollectCharacters());

        static string CollectCharacters()
        {
            var set = new SortedSet<char>();
            for (char c = (char)32; c < 127; c++) set.Add(c);
            foreach (char c in Extra) set.Add(c);
            var literal = new Regex(@"""(?:[^""\\\n]|\\.)*""|'(?:[^'\\\n]|\\.)'");
            foreach (var file in Directory.GetFiles(ScriptDir, "*.cs", SearchOption.AllDirectories))
                foreach (Match m in literal.Matches(File.ReadAllText(file, Encoding.UTF8)))
                    foreach (char c in m.Value)
                        if (c > 126 && !char.IsSurrogate(c) && !char.IsControl(c)) set.Add(c);
            return new string(set.ToArray());
        }

        static (TMP_FontAsset, TMP_FontAsset) Generate(string chars)
        {
            if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/Fonts", "Subset");

            var bold = Make(AssetDatabase.LoadAssetAtPath<Font>(BoldSource), BoldPath, chars, 2048, 2048, out string missing);
            TMP_FontAsset jp = null;
            AssetDatabase.DeleteAsset(JapanesePath);
            if (!string.IsNullOrEmpty(missing))
            {
                // characters the Chinese font lacks (Japanese-only kanji forms …) come from the Japanese font
                int size = missing.Length <= 30 ? 512 : 1024;
                jp = Make(AssetDatabase.LoadAssetAtPath<Font>(JapaneseSource), JapanesePath, missing, size, size, out string stillMissing);
                if (!string.IsNullOrEmpty(stillMissing)) Debug.LogWarning("[FontSubset] no glyph in either font: " + stillMissing);
                bold.fallbackFontAssetTable = new List<TMP_FontAsset> { jp };
                EditorUtility.SetDirty(bold);
            }

            File.WriteAllText(CharsetPath, chars, Encoding.UTF8);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(CharsetPath);
            Debug.Log($"[FontSubset] {chars.Length} characters → {BoldPath} ({bold.atlasTextures.Length} atlas)" +
                      (jp != null ? $" + {missing.Length} from {JapanesePath}" : ""));
            return (bold, jp);
        }

        static TMP_FontAsset Make(Font source, string path, string chars, int w, int h, out string missing)
        {
            AssetDatabase.DeleteAsset(path);
            var fa = TMP_FontAsset.CreateFontAsset(source, PointSize, Padding, GlyphRenderMode.SDFAA, w, h, AtlasPopulationMode.Dynamic, true);
            fa.name = Path.GetFileNameWithoutExtension(path);
            fa.TryAddCharacters(chars, out missing);

            AssetDatabase.CreateAsset(fa, path);
            for (int i = 0; i < fa.atlasTextures.Length; i++)
            {
                var tex = fa.atlasTextures[i];
                tex.name = fa.name + " Atlas" + (i > 0 ? " " + i : "");
                AssetDatabase.AddObjectToAsset(tex, fa);
            }
            var mat = fa.material;
            mat.name = fa.name + " Material";
            var sdf = Shader.Find("TextMeshPro/Distance Field");   // same shader as the original font assets
            if (sdf != null) mat.shader = sdf;
            AssetDatabase.AddObjectToAsset(mat, fa);

            // Static: glyphs are baked, and the source .TTC reference is dropped so it is not shipped in builds.
            fa.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(fa);
            return fa;
        }
    }
}
