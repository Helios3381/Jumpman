using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace WeddingRun.EditorTools
{
    /// <summary>Creates Assets/Scenes/WeddingRun.unity with everything wired up. Menu: Tools > Wedding Run.</summary>
    public static class WeddingSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/WeddingRun.unity";
        const string MatDir = "Assets/Wedding/Materials";

        [MenuItem("Tools/Wedding Run/Build Scene")]
        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateScene();
            EditorUtility.DisplayDialog("Wedding Run", "Scene created: " + ScenePath + "\nPress Play to try it.", "OK");
        }

        public static void BuildSceneBatch() => CreateScene();

        static Material MakeMat(string name, string shader, System.Action<Material> setup = null)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find(shader);
            setup?.Invoke(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void CreateScene()
        {
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Wedding", "Materials");

            var lit = MakeMat("WR_Lit", "Standard", m => { m.SetFloat("_Glossiness", 0.25f); m.color = Color.white; });
            var unlit = MakeMat("WR_Unlit", "Unlit/Color", m => m.color = Color.white);
            var vertex = MakeMat("WR_VertexColor", "Sprites/Default", m => m.color = Color.white);
            var fade = MakeMat("WR_Fade", "Standard", m =>
            {
                m.SetFloat("_Mode", 2);
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3000;
                m.SetOverrideTag("RenderType", "Transparent");
                m.color = new Color(1, 1, 1, 0.5f);
            });
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.95f, 0.6f, 0.7f);
            cam.fieldOfView = 40;
            cam.farClipPlane = 1000;
            camGo.transform.SetPositionAndRotation(new Vector3(1.5f, 5.2f, -19f), Quaternion.Euler(6, 0, 0));
            camGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(48, -35, 0);

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var gameGo = new GameObject("WeddingGame");
            var game = gameGo.AddComponent<WeddingGame>();
            game.litMaterial = lit;
            game.unlitMaterial = unlit;
            game.vertexColorMaterial = vertex;
            game.fadeMaterial = fade;
            game.fontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MSJHBD SDF.asset");
            // One font only: every extra CJK font adds 10-20 MB to the WebGL download.
            game.fontRegular = game.fontBold;
            game.fontJapanese = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BIZ-UDMINCHOM SDF.asset");
            if (game.fontJapanese != null)
            {
                game.fontJapanese.isMultiAtlasTexturesEnabled = true;
                if (game.fontBold.fallbackFontAssetTable == null) game.fontBold.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                if (!game.fontBold.fallbackFontAssetTable.Contains(game.fontJapanese)) game.fontBold.fallbackFontAssetTable.Add(game.fontJapanese);
                EditorUtility.SetDirty(game.fontJapanese);
                EditorUtility.SetDirty(game.fontBold);
            }
            if (game.fontBold != null && !game.fontBold.isMultiAtlasTexturesEnabled)
            {
                game.fontBold.isMultiAtlasTexturesEnabled = true;
                EditorUtility.SetDirty(game.fontBold);
            }
            game.music = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/BGM/Theme.mp3");
            game.gameOverMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/BGM/Gameover.wav");
            game.jumpSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Course Library/Sound/SFX/jump1.wav");
            game.crashSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Course Library/Sound/SFX/crash1.wav");

            // Fog must be on in the scene, otherwise its shader variants are stripped from builds.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 45;
            RenderSettings.fogEndDistance = 230;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.7f, 0.65f, 0.75f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[WeddingRun] Scene built: " + ScenePath);
        }

        /// <summary>Batch: build a macOS player used for automated screenshots.</summary>
        public static void BuildMacTest()
        {
            CreateScene();
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/MacTest/WeddingRun.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("[WeddingRun] Mac build: " + report.summary.result + " errors=" + report.summary.totalErrors);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>Batch: WebGL build for the wedding website.</summary>
        [MenuItem("Tools/Wedding Run/Build WebGL")]
        public static void BuildWebGL()
        {
            CreateScene();
            // Page template made for the wedding site: fills its iframe and exposes callUnityStartBGM / callUnityStopBGM.
            PlayerSettings.WebGL.template = "PROJECT:Wedding";
            // Gzip + decompression fallback: ~4x smaller download and works on any static host (GitHub Pages etc.).
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            // Unique file names per build so browsers never mix an old cached loader with new data files.
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            if (System.IO.Directory.Exists("Builds/JumpmanGL")) System.IO.Directory.Delete("Builds/JumpmanGL", true);
            // Output folder name = file prefix, so this matches the wedding site's JumpmanGL/ folder.
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/JumpmanGL", BuildTarget.WebGL, BuildOptions.None);
            Debug.Log("[WeddingRun] WebGL build: " + report.summary.result);
        }
    }
}
