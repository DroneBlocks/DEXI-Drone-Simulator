using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;
using UnityEngine.UI;
using static GameManager;

public class UIManager : MonoBehaviour
{
    [Header("References")]
    public ROSKeyboardController inputController;

    [Header("Pre-game UI")]
    public GameObject preGamePanel;

    [Header("In game UI")]
    public GameObject inGamePanel;
    public TMP_Text elapsedTimeText;
    public TMP_Text targetsScannedText;
    public TMP_Text ledsCorrectText;
    public TMP_Text bridgesFlownText;
    public TMP_Text landingsCompletedText;
    public GameObject correctScanPopupPrefab;

    [Header("PiP UI")]
    [SerializeField] private GameObject pipImage;
    private bool pipEnabled = true;

    [Header("Game completed UI")]
    public GameObject gameCompletedPanel;
    public TMP_Text finalTimeText;
    public TMP_Text finalScoreText;

    private GameState lastState;
    private GameScoreUpdate _prevScore;

    private Coroutine _popupCoroutine;

    // Authoritative score relayed by the validator after submit; null until it arrives.
    private double? _serverTotal;
    private Coroutine _finalScoreFallback;

    private void Start()
    {
        UpdateUIState(GameManager.Instance.State);

        GameManager.Instance.OnScoreUpdateReceived += OnNewScoreUpdate;
    }

    private void OnDestroy()
    {
        GameManager.Instance.OnScoreUpdateReceived -= OnNewScoreUpdate;
    }

    void Update()
    {
        GameState state = GameManager.Instance.State;

        if (state == GameState.Running)
        {
            float elapsed = GameManager.Instance.ElapsedTime;
            elapsedTimeText.text = $"Time: {elapsed:F1}s";
        }

        if (state != lastState)
        {
            UpdateUIState(state);
            lastState = state;
        }
    }


    private void OnNewScoreUpdate(GameScoreUpdate score)
    {
        string detected = "0/3", led_correct = "0/3", gate_correct = "No";
        int landings = 0;

        if (score != null)
        {
            detected = score.detected;
            led_correct = score.led_correct;
            gate_correct = score.gate_correct ? "Yes" : "No";
            landings = score.landings;
        }

        targetsScannedText.text = $"Targets Scanned: {detected}";
        ledsCorrectText.text = $"LEDs Correct: {led_correct}";
        bridgesFlownText.text = $"Flew under bridge: {gate_correct}";
        landingsCompletedText.text = $"Landings Completed: {landings} / 1";

        string popupMessage = GetChangedObjectiveText(score);
        _prevScore = score;

        // The post-submit relay carries the authoritative leaderboard score.
        if (score != null && score.final)
        {
            _serverTotal = score.total;
            if (gameCompletedPanel.activeSelf)
                finalScoreText.text = FormatFinalScore();
        }

        if (popupMessage != null)
        {
            if (_popupCoroutine != null) StopCoroutine(_popupCoroutine);
            _popupCoroutine = StartCoroutine(SpawnPopupDelayed(popupMessage));
        }
    }

    private IEnumerator SpawnPopupDelayed(string message)
    {
        yield return new WaitForSeconds(0.5f);
        GameObject obj = Instantiate(correctScanPopupPrefab, inGamePanel.transform);
        obj.GetComponent<CorrectScanPopup>().SetObjectiveText(message);
        _popupCoroutine = null;
    }

    private string GetChangedObjectiveText(GameScoreUpdate score)
    {
        if (score == null) return null;

        int currDetected = int.Parse(score.detected.Split('/')[0]);
        int prevDetected = _prevScore != null ? int.Parse(_prevScore.detected.Split('/')[0]) : 0;

        int currLedCorrect = int.Parse(score.led_correct.Split('/')[0]);
        int prevLedCorrect = _prevScore != null ? int.Parse(_prevScore.led_correct.Split('/')[0]) : 0;

        bool prevGate = _prevScore?.gate_correct ?? false;
        int prevLandings = _prevScore?.landings ?? 0;

        if (currLedCorrect > prevLedCorrect)
        {
            return $"Target {currDetected} scanned with correct LED!";
        }

        if (currDetected > prevDetected)
        {
            return $"Target {currDetected} scanned!";
        }

        if (score.gate_correct && !prevGate)
        {
            return "Bridge flown through!";
        }

        if (score.landings > prevLandings)
        {
            return $"Landed Successfully!";
        }

        return null;
    }

    private void UpdateUIState(GameState state)
    {
        bool running = state == GameState.Running;

        preGamePanel.SetActive(state == GameState.WaitingToStart);
        inGamePanel.SetActive(running);
        gameCompletedPanel.SetActive(state == GameState.Completed);

        if (state == GameState.Completed)
        {
            float elapsed = GameManager.Instance.ElapsedTime;
            finalTimeText.text = $"FINAL TIME: {elapsed:F2}s";

            // Score is authoritative from the server (relayed after submit). Show it once
            // it arrives; until then a calculating state, with a fallback if it never does.
            if (_serverTotal.HasValue)
            {
                finalScoreText.text = FormatFinalScore();
            }
            else
            {
                finalScoreText.text = "Total Score: calculating...";
                if (_finalScoreFallback != null) StopCoroutine(_finalScoreFallback);
                _finalScoreFallback = StartCoroutine(FinalScoreFallback());
            }
        }
    }

    private string FormatFinalScore() => $"Total Score: {_serverTotal.Value:0.#} points";

    private IEnumerator FinalScoreFallback()
    {
        yield return new WaitForSeconds(8f);
        if (!_serverTotal.HasValue && gameCompletedPanel.activeSelf)
            finalScoreText.text = "Score submitted - see the leaderboard";
        _finalScoreFallback = null;
    }

    public void ResetGame()
    {
        _serverTotal = null;
        GameManager.Instance.ResetGame();
        GameManager.Instance.StartGame();
    }

    public void ViewLeaderboard()
    {
        // for when its real
    }

    public void StartGame()
    {
        GameManager.Instance.StartGame();
    }

    public void TogglePiP() {
        pipEnabled = !pipEnabled;
        pipImage.SetActive(pipEnabled);
    }
}