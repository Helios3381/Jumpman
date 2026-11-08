using System.Collections;
using System.IO;
using UnityEngine;

namespace WeddingRun
{
    /// <summary>
    /// Automated visual check: launch the player with "-autoshot &lt;folder&gt;" and it plays itself through every
    /// stage, saving screenshots, then quits. Does nothing in normal runs.
    /// </summary>
    public class AutoShot : MonoBehaviour
    {
        string dir;

        IEnumerator Start()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-autoshot");
            dir = i >= 0 && i + 1 < args.Length ? args[i + 1] : Application.persistentDataPath;
            Directory.CreateDirectory(dir);
            var game = GetComponent<WeddingGame>();
            WeddingGame.HasReachedWedding = false;
            int li = System.Array.IndexOf(args, "-lang");
            if (li >= 0 && li + 1 < args.Length)
            {
                var target = args[li + 1] == "en" ? Lang.En : args[li + 1] == "ja" ? Lang.Ja : Lang.Zh;
                yield return null;
                while (Loc.Current != target) game.ChangeLanguage();
            }

            yield return new WaitForSecondsRealtime(2.5f);
            Shot("01_title");

            game.UI.OpenHowTo();
            yield return new WaitForSecondsRealtime(1.2f);
            Shot("02_howto");
            game.UI.CloseModals();
            yield return new WaitForSecondsRealtime(0.6f);

            game.autoPilot = true;
            game.debugInvincible = true;
            game.StartRun();
            yield return new WaitForSecondsRealtime(4.6f);
            Shot("03_stage1");
            game.DebugGiveShield();
            yield return new WaitForSecondsRealtime(0.8f);
            Shot("03b_shield");
            yield return new WaitForSecondsRealtime(6f);
            Shot("04_stage1b");
            for (int s = 1; s < Stages.List.Length; s++)
            {
                game.DebugJumpTo(Stages.List[s].startDistance + 2);
                yield return new WaitForSecondsRealtime(1.0f);
                Shot($"0{4 + s}a_stage{s + 1}_banner");
                yield return new WaitForSecondsRealtime(4.5f);
                Shot($"0{4 + s}b_stage{s + 1}");
                if (Stages.List[s].theme == 3)
                {
                    game.DebugSpawnAt(ModelId.SumoPig, 30f);
                    yield return new WaitForSecondsRealtime(0.35f);
                    Shot("07c_pig_warning");
                    yield return new WaitForSecondsRealtime(0.45f);
                    Shot("07d_pig_charge");
                    // Port of Nagoya and the proposal (first lap)
                    game.DebugJumpTo(Stages.StartOf(0, 3) + Stages.List[3].length - 95f);
                    yield return new WaitForSecondsRealtime(1.5f); Shot("07e_port");
                    yield return new WaitForSecondsRealtime(3.4f); Shot("07f_propose_kneel");
                    yield return new WaitForSecondsRealtime(1.5f); Shot("07g_propose_yes");
                    yield return new WaitForSecondsRealtime(1.0f); Shot("07h_propose_hug");
                    yield return new WaitForSecondsRealtime(2.3f);
                }
            }
            game.DebugForceSlide();
            yield return new WaitForSecondsRealtime(0.15f);
            Shot("08c_slide");

            // wedding finale (hug, kiss, guests) then the loop back to Hong Kong
            game.DebugJumpTo(Stages.FirstLapLength - 45);
            yield return new WaitForSecondsRealtime(1.6f);  Shot("09a_finale_slowdown");
            yield return new WaitForSecondsRealtime(1.4f);  Shot("09b_finale_hug");
            yield return new WaitForSecondsRealtime(1.4f);  Shot("09c_finale_kiss");
            yield return new WaitForSecondsRealtime(1.6f);  Shot("09d_finale_guests");
            yield return new WaitForSecondsRealtime(3.0f);  Shot("09e_resume");
            yield return new WaitForSecondsRealtime(3.5f);  Shot("09f_lap2");
            game.debugInvincible = false;
            game.DebugKill();
            yield return new WaitForSecondsRealtime(1.3f);
            Shot("09c_bump");
            yield return new WaitForSecondsRealtime(2.0f);
            Shot("10_gameover");
            game.GoHome();
            yield return new WaitForSecondsRealtime(1.2f);
            Shot("11_title_after_wedding");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        void Shot(string name)
        {
            string path = Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[AutoShot] " + path);
        }
    }
}
