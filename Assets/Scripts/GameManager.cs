using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Leaderboards;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

public class GameManager : MonoBehaviour
{
    //Game Info
    public Boolean isGameActive = false;
    //BGM manager
    private static bool hasEnteredGame = false;
    public AudioSource bgmSource;
    public AudioClip confirmSound;
    public AudioClip bgmTheme;
    public AudioClip bgmGameOver;
    //UI
    public List<GameObject> targets;
    private float score = 0;
    private double bestScore = 0;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI bestText;
    public Button restartButton;
    public Button rankingButton;
    public Button settingsButton;
    public GameObject permanentScreen;
    public GameObject titleScreen;
    public GameObject gameScreen;
    public GameObject scoreScreen;
    public GameObject gameOverScreen;
    public GameObject GameOverScreenWebGL;
    public GameObject settingsScreen;
    public GameObject rankingScreen;
    public TextMeshProUGUI newRecordText;
    public TextMeshProUGUI newRecordAlertText;
    public bool isNewRecordAlertTextDisplayed = false;
    public Button submitScoreButton;
    public TMP_InputField rankingNameInputField;
    //prefab
    private Vector3 spawnPos = new Vector3(35, 0, 0);
    private float spawnAcceleration = 0.05f;
    private float baseRepeatRate = 2;
    private float maxRepeatRate = 10;
    //controller
    private PlayerController playerControllerScript;
    //ranking
    private string leaderboardID = "highscores";
    public TextMeshProUGUI top1Name, top1Score;
    public TextMeshProUGUI top2Name, top2Score;
    public TextMeshProUGUI top3Name, top3Score;
    public TextMeshProUGUI top4Name, top4Score;
    public TextMeshProUGUI top5Name, top5Score;
    //game data
    //the time when the game is started
    public float gameTime = 0f;
    //update to vue.js
    [DllImport("__Internal")]
    private static extern void GameOverAndSendScore(float score);

    async void Awake()
    {
        if (!isWebGL())
        {
            //load ugs only in normal mode
            await InitializeUGS();
            //get player best score
            bestScore = await GetBestScore();
            bestText.text = "最佳得分: " + bestScore.ToString("F2") + "m";
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        //object init
        titleScreen = GameObject.Find("TitleScreen");
        if (isWebGL())
        {
            //disable ranking and settings button when it is webGL
            rankingButton.gameObject.SetActive(false);
            settingsButton.gameObject.SetActive(false);
            permanentScreen.gameObject.SetActive(false);
            if (hasEnteredGame)
            {
                StartBGM();
            }
        }
        else
        {
            //play bgm once the game is started
            StartBGM();
        }

        if (GameObject.Find("Player"))
        {
            playerControllerScript = GameObject.Find("Player").GetComponent<PlayerController>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (isGameActive)
        {
            gameTime += Time.deltaTime;
            score += Time.deltaTime * 5f;
            scoreText.text = "Score: " + Mathf.FloorToInt(score) + "m";

            if (score > bestScore && !isNewRecordAlertTextDisplayed && !isWebGL())
            {
                isNewRecordAlertTextDisplayed = true;
                StartCoroutine(ShowNewRecord());
            }
        }
    }

    async Task InitializeUGS()
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        Debug.Log("UGS Initialized & Signed In");
    }

    public void newGame()
    {
        //update UI Screen
        titleScreen.gameObject.SetActive(false);
        scoreScreen.gameObject.SetActive(true);
        //start game
        isGameActive = true;
        //init data
        score = 0;
        UpdateScore(0);
        //start obstacle
        StartCoroutine(SpawnTarget());
    }

    public void StartGame()
    {
        Debug.Log(gameObject.name + " was clicked");
        newGame();
    }

    IEnumerator SpawnTarget()
    {
        while (isGameActive)
        {
            float currentRepeatRate = baseRepeatRate - (gameTime * spawnAcceleration);
            currentRepeatRate = Mathf.Max(currentRepeatRate, 1f);

            yield return new WaitForSeconds(Random.Range(0.5f, currentRepeatRate));
            if (playerControllerScript.gameOver == false)
            {
                int index = Random.Range(0, targets.Count);
                Instantiate(targets[index], spawnPos, targets[index].transform.rotation);
            }
        }
    }

    public void UpdateScore(int scoreToAdd)
    {
        score += scoreToAdd;
        scoreText.text = "Score: " + score;
    }

    public void GameOver()
    {
        isGameActive = false;

        if(isWebGL())
        {
            Debug.Log($"[Unity] WebGL send score：{score} to website");
            GameOverAndSendScore(score);
            GameOverScreenWebGL.gameObject.SetActive(true);
        }
        else
        {
            Debug.Log($"Non WebGL: {score}");
            gameOverScreen.gameObject.SetActive(true);
        }

        restartButton.gameObject.SetActive(true);

        bgmSource.Stop();
        bgmSource.clip = bgmGameOver;
        bgmSource.Play();

        gameTime = 0;
    }

    public void RestartGame()
    {
        score = 0;
        newRecordText.gameObject.SetActive(false);
        isNewRecordAlertTextDisplayed = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void Settings()
    {
        settingsScreen.gameObject.SetActive(true);
    }

    public void Ranking()
    {
        rankingScreen.gameObject.SetActive(true);
        GetTop5Scores();
    }

    public void BacktoMenu()
    {
        settingsScreen.gameObject.SetActive(false);
        rankingScreen.gameObject.SetActive(false);
    }

    IEnumerator ShowNewRecord()
    {
        newRecordAlertText.gameObject.SetActive(true);

        yield return new WaitForSeconds(3f);

        newRecordAlertText.gameObject.SetActive(false);
    }

    private async Task<double> GetBestScore()
    {
        return (await GetMyBestScore()) ?? 0f;
    }

    public void GetResult()
    {
        if (score > bestScore)
        {
            //new record
            Debug.Log("New Record!");
            newRecordText.gameObject.SetActive(true);
        }
    }

    public async void SubmitScore()
    {
        //submit score to server
        var metadata = new Dictionary<string, string>()
            {
                { "name", rankingNameInputField.text }
            };

        await LeaderboardsService.Instance.AddPlayerScoreAsync(
            leaderboardID,
            score,
            new AddPlayerScoreOptions { Metadata = metadata }
        );
        RestartGame();
    }

    public async Task<double?> GetMyBestScore()
    {
        try
        {
            var response = await LeaderboardsService.Instance.GetPlayerScoreAsync(leaderboardID);
            return response.Score;
        }
        catch
        {
            // No Record
            return null;
        }
    }

    public async void GetTop5Scores()
    {
        var options = new GetScoresOptions
        {
            Limit = 5,
            IncludeMetadata = true
        };

        var scoresResponse = await LeaderboardsService.Instance
            .GetScoresAsync(leaderboardID, options);

        TextMeshProUGUI[] nameTexts =
        { top1Name, top2Name, top3Name, top4Name, top5Name };

        TextMeshProUGUI[] scoreTexts =
        { top1Score, top2Score, top3Score, top4Score, top5Score };

        for (int i = 0; i < 5; i++)
        {
            nameTexts[i].text = "-";
            scoreTexts[i].text = "-";
        }

        for (int i = 0; i < scoresResponse.Results.Count && i < 5; i++)
        {
            var entry = scoresResponse.Results[i];

            string name = "Unknown";

            if (!string.IsNullOrEmpty(entry.Metadata))
            {
                PlayerMetadata meta =
                    JsonUtility.FromJson<PlayerMetadata>(entry.Metadata);

                if (meta != null && !string.IsNullOrEmpty(meta.name))
                    name = meta.name;
            }

            nameTexts[i].text = name;
            scoreTexts[i].text = entry.Score.ToString("F2");
        }
    }

    [System.Serializable]
    public class PlayerMetadata
    {
        public string name;
    }

    private bool isWebGL()
    {
        // if WebGL
        #if UNITY_WEBGL && !UNITY_EDITOR
            return true;
        #else
            Debug.Log($"Non WebGL: {score}");
            return false;
        #endif
    }

    public void StartBGM()
    {
        hasEnteredGame = true;
        if (bgmSource.isPlaying) return;
        bgmSource.Stop();
        bgmSource.clip = bgmTheme;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        bgmSource.Stop();
    }
}


