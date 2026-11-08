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
            // Static subset fonts (only the characters used in the scripts): the dynamic CJK fonts would ship
            // the whole .TTC files (~20 MB) with the WebGL build.
            var (fontBold, fontJapanese) = FontSubsetter.Ensure();
            game.fontBold = fontBold;
            game.fontRegular = fontBold;
            game.fontJapanese = fontJapanese;
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
            ShrinkWebGL();
            if (System.IO.Directory.Exists("Builds/JumpmanGL")) System.IO.Directory.Delete("Builds/JumpmanGL", true);
            // Output folder name = file prefix, so this matches the wedding site's JumpmanGL/ folder.
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/JumpmanGL", BuildTarget.WebGL, BuildOptions.None);
            Debug.Log("[WeddingRun] WebGL build: " + report.summary.result);
        }

        /// <summary>Download-size settings for the web build (guests open it on phones over mobile data).</summary>
        static void ShrinkWebGL()
        {
            var webgl = UnityEditor.Build.NamedBuildTarget.WebGL;
            // Strip unused managed code (old Jumpman scripts, Unity Services …) and generate smaller IL2CPP code.
            PlayerSettings.SetManagedStrippingLevel(webgl, ManagedStrippingLevel.Medium);
            PlayerSettings.SetIl2CppCodeGeneration(webgl, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.stripEngineCode = true;
            SetWasmCodeOptimization("DiskSize");

            // Background music was imported at 100 % quality (≈5 MB of the download). WebGL has no per-platform audio
            // override, so the default settings are lowered: 35 % at 32 kHz still sounds fine on phone speakers.
            foreach (var path in new[] { "Assets/BGM/Theme.mp3", "Assets/BGM/Gameover.wav" })
            {
                if (!(AssetImporter.GetAtPath(path) is AudioImporter imp)) continue;
                var s = imp.defaultSampleSettings;
                if (Mathf.Approximately(s.quality, 0.35f) && s.sampleRateSetting == AudioSampleRateSetting.OverrideSampleRate && s.sampleRateOverride == 32000) continue;
                s.quality = 0.35f;
                s.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                s.sampleRateOverride = 32000;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
                Debug.Log("[WeddingRun] music import settings lowered: " + path);
            }
        }

        /// <summary>Build Settings > Code Optimization (WebGL), set via reflection so this compiles without the WebGL module.</summary>
        static void SetWasmCodeOptimization(string value)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.GetName().Name.StartsWith("UnityEditor.WebGL")) continue;
                foreach (var t in asm.GetTypes())
                {
                    var prop = t.GetProperty("codeOptimization", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (prop == null || !prop.PropertyType.IsEnum || !prop.CanWrite) continue;
                    prop.SetValue(null, System.Enum.Parse(prop.PropertyType, value));
                    Debug.Log("[WeddingRun] WebGL code optimization: " + prop.GetValue(null));
                    return;
                }
            }
            Debug.LogWarning("[WeddingRun] WebGL code optimization setting not found");
        }
    }
}
