using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;
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

    void Awake()
    {
        if (!isWebGL())
        {
            //get player best score (saved on this device)
            bestScore = GetBestScore();
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

    private double GetBestScore()
    {
        return GetMyBestScore() ?? 0f;
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

    // The online leaderboard (Unity Gaming Services) was removed: it added several MB to the wedding site's WebGL
    // download. Scores are now kept on this device (PlayerPrefs).
    [System.Serializable]
    class LocalBoard { public List<LocalEntry> entries = new List<LocalEntry>(); }

    [System.Serializable]
    class LocalEntry { public string name; public double score; }

    string BoardKey => "local_board_" + leaderboardID;

    LocalBoard LoadBoard()
    {
        var json = PlayerPrefs.GetString(BoardKey, "");
        return string.IsNullOrEmpty(json) ? new LocalBoard() : (JsonUtility.FromJson<LocalBoard>(json) ?? new LocalBoard());
    }

    public void SubmitScore()
    {
        //save score on this device
        var board = LoadBoard();
        string playerName = rankingNameInputField != null ? rankingNameInputField.text : "";
        board.entries.Add(new LocalEntry { name = string.IsNullOrWhiteSpace(playerName) ? "Unknown" : playerName.Trim(), score = score });
        board.entries.Sort((a, b) => b.score.CompareTo(a.score));
        if (board.entries.Count > 5) board.entries.RemoveRange(5, board.entries.Count - 5);
        PlayerPrefs.SetString(BoardKey, JsonUtility.ToJson(board));
        PlayerPrefs.Save();
        RestartGame();
    }

    public double? GetMyBestScore()
    {
        var board = LoadBoard();
        if (board.entries.Count == 0) return null;   // No Record
        return board.entries[0].score;
    }

    public void GetTop5Scores()
    {
        var entries = LoadBoard().entries;

        TextMeshProUGUI[] nameTexts =
        { top1Name, top2Name, top3Name, top4Name, top5Name };

        TextMeshProUGUI[] scoreTexts =
        { top1Score, top2Score, top3Score, top4Score, top5Score };

        for (int i = 0; i < 5; i++)
        {
            nameTexts[i].text = "-";
            scoreTexts[i].text = "-";
        }

        for (int i = 0; i < entries.Count && i < 5; i++)
        {
            nameTexts[i].text = entries[i].name;
            scoreTexts[i].text = entries[i].score.ToString("F2");
        }
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


