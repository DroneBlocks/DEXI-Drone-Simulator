using TMPro;
using UnityEngine;
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

    [Header("PiP UI")]
    [SerializeField] private GameObject pipImage;

    [Header("Game completed UI")]
    public GameObject gameCompletedPanel;
    public TMP_Text finalTimeText;
    public TMP_Text finalScoreText;

    private GameState lastState;
    private bool pipEnabled = true;

    private void Start()
    {
        UpdateUIState(GameManager.Instance.State);
        UpdateTargets();
    }

    void Update()
    {
        GameState state = GameManager.Instance.State;

        if (state == GameState.Running)
        {
            float elapsed = GameManager.Instance.ElapsedTime;
            elapsedTimeText.text = $"Time: {elapsed:F1}s";
            UpdateTargets();
        }

        if (state != lastState)
        {
            UpdateUIState(state);
            lastState = state;
        }
    }

    private void UpdateTargets()
    {
        GameScoreUpdate score = GameManager.Instance.LatestScore;

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
            finalScoreText.text = $"Total Score: {GameManager.Instance.finalScore} points";
        }
    }

    public void ResetGame()
    {
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