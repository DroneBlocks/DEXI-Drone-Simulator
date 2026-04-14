using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using static GameManager;
using static UnityEngine.Rendering.DebugUI.MessageBox;

public class UIManager : MonoBehaviour
{
    [Header("References")]
    public ROSKeyboardController inputController;

    [Header("Pre-game UI")]
    public GameObject preGamePanel;

    [Header("In game UI")]
    public GameObject inGamePanel;
    public TMP_Text elapsedTimeText;

    public Color pendingTargetColor = new Color(1, 1, 1, 0.5f);

    public List<TMP_Text> targetStatusTexts = new();

    [Header("Game completed UI")]
    public GameObject gameCompletedPanel;
    public TMP_Text finalTimeText;

    private float elapsedTime = 0f;


    void Update()
    {
        elapsedTime = GameManager.Instance.ElapsedTime;

        elapsedTimeText.text = $"Time: {elapsedTime:F1}s";
        finalTimeText.text = $"FINAL TIME: {elapsedTime:F2}s";

        UpdateUIState(GameManager.Instance.State);

        Debug.Log(GameManager.Instance.LatestScore);

        var groups = GameManager.Instance.AllTargets.GroupBy(t => t.groupName);
        for (var i = 0; i < groups.Count(); i++)
        {
            var group = groups.ElementAt(i);

            ScanTarget realTarget = group.FirstOrDefault(t => t.IsReal);
            bool found = realTarget != null;

            if (found)
            {
                targetStatusTexts[i].color = realTarget.expectedLEDColor;

                string colorName = GameManager.Instance.ColorToName(realTarget.expectedLEDColor);
                string label = $"  {group.Key}: {realTarget.targetName} (LED: {colorName})";

                targetStatusTexts[i].text = label;
            }
            else
            {
                targetStatusTexts[i].color = pendingTargetColor;
                targetStatusTexts[i].text = $"  {group.Key}: ?";
            }
        }
        /*
        foreach (var gate in allGates)
        {
            string label = gate.IsTriggered
                ? $"  {gate.gateName}: PASSED"
                : $"  {gate.gateName}: —";
            GUI.Label(new Rect(10, y, 300, 28), label, gate.IsTriggered ? completedStyle : pendingStyle);
            y += 24;
        }
        if (allGates.Count > 0) y += 4;

        foreach (var zone in allLandingZones)
        {
            string label = zone.IsLanded
                ? $"  {zone.zoneName}: LANDED"
                : $"  {zone.zoneName}: —";
            GUI.Label(new Rect(10, y, 300, 28), label, zone.IsLanded ? completedStyle : pendingStyle);
            y += 24;
        }
        y += 4;*/
    }

    private void UpdateUIState(GameState state)
    {
        switch (state)
        {
            case GameState.WaitingToStart:
                preGamePanel.SetActive(true);
                inGamePanel.SetActive(false);
                gameCompletedPanel.SetActive(false);
            break;

            case GameState.Running:
                preGamePanel.SetActive(false);
                inGamePanel.SetActive(true);
                gameCompletedPanel.SetActive(false);
            break;

            case GameState.Completed:
                preGamePanel.SetActive(false);
                inGamePanel.SetActive(false);
                gameCompletedPanel.SetActive(true);
            break;
        }
    }

    public void StartGame()
    {
        GameManager.Instance.StartGame();
    }

   /* void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = Color.white;

        float y = 60f;


        string stateText = GameManager.Instance.State switch
        {
            GameManager.GameState.WaitingToStart => "READY — Press [Space] to start",
            GameManager.GameState.Running => "RUNNING",
            GameManager.GameState.Completed => "COMPLETED!",
            _ => ""
        };
        GUI.Label(new Rect(10, y, 500, 28), stateText, style);
        y += 26;

        GUI.Label(new Rect(10, y, 300, 28), $"Time: {elapsedTime:F1}s", style);
        y += 26;

        GUIStyle completedStyle = new GUIStyle(style);
        completedStyle.normal.textColor = Color.green;
        GUIStyle pendingStyle = new GUIStyle(style);
        pendingStyle.normal.textColor = new Color(1, 1, 1, 0.5f);

        var groups = allTargets.GroupBy(t => t.groupName);
        foreach (var group in groups)
        {
            ScanTarget realTarget = group.FirstOrDefault(t => t.IsReal);
            bool found = realTarget != null;
            if (found)
            {
                GUIStyle colorStyle = new GUIStyle(style);
                colorStyle.normal.textColor = realTarget.expectedLEDColor;
                string label = $"  {group.Key}: {realTarget.targetName} (LED: {ColorToName(realTarget.expectedLEDColor)})";
                GUI.Label(new Rect(10, y, 600, 28), label, colorStyle);
            }
            else
            {
                GUI.Label(new Rect(10, y, 400, 28), $"  {group.Key}: ?", pendingStyle);
            }
            y += 24;
        }
        y += 4;

        foreach (var gate in allGates)
        {
            string label = gate.IsTriggered
                ? $"  {gate.gateName}: PASSED"
                : $"  {gate.gateName}: —";
            GUI.Label(new Rect(10, y, 300, 28), label, gate.IsTriggered ? completedStyle : pendingStyle);
            y += 24;
        }
        if (allGates.Count > 0) y += 4;

        foreach (var zone in allLandingZones)
        {
            string label = zone.IsLanded
                ? $"  {zone.zoneName}: LANDED"
                : $"  {zone.zoneName}: —";
            GUI.Label(new Rect(10, y, 300, 28), label, zone.IsLanded ? completedStyle : pendingStyle);
            y += 24;
        }
        y += 4;

        if (state == GameState.Completed)
        {
            GUIStyle bigStyle = new GUIStyle(style) { fontSize = 24 };
            bigStyle.normal.textColor = Color.green;
            GUI.Label(new Rect(10, y + 10, 500, 35), $"FINAL TIME: {elapsedTime:F2}s", bigStyle);
        }

        // Answer Key
        int groupCount = groups.Count();
        float answerLineHeight = 30f;
        float answerY = Screen.height - 40 - (groupCount + 1) * answerLineHeight;

        GUIStyle answerHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold
        };
        answerHeaderStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(10, answerY, 400, 35), "ANSWER KEY", answerHeaderStyle);
        answerY += 35;

        GUIStyle answerStyle = new GUIStyle(GUI.skin.label) { fontSize = 22 };

        foreach (var group in groups)
        {
            ScanTarget realTarget = group.FirstOrDefault(t => t.IsReal);
            if (realTarget != null)
            {
                answerStyle.normal.textColor = realTarget.expectedLEDColor;
                string answer = $"  {group.Key}: {realTarget.targetName} → {ColorToName(realTarget.expectedLEDColor)}";
                GUI.Label(new Rect(10, answerY, 600, 30), answer, answerStyle);
            }
            answerY += answerLineHeight;
        }

        GUIStyle smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        smallStyle.normal.textColor = new Color(1, 1, 1, 0.6f);
        GUI.Label(new Rect(10, Screen.height - 30, 400, 25), "[R] Reset  [Space] Start", smallStyle);

        // Validator score feedback
        if (latestScore != null && (scoreMessageTimer > 0f || latestScore.game_complete))
        {
            float bannerW = 420f;
            float bannerH = latestScore.game_complete ? 80f : 50f;
            float bannerX = (Screen.width - bannerW) / 2f;
            float bannerY = 10f;

            Color bgColor = latestScore.game_complete
                ? new Color(0f, 0.5f, 0f, 0.85f)
                : new Color(0f, 0.3f, 0.6f, 0.85f);

            Texture2D bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, bgColor);
            bgTex.Apply();
            GUI.DrawTexture(new Rect(bannerX, bannerY, bannerW, bannerH), bgTex);

            GUIStyle bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            bannerStyle.normal.textColor = Color.white;

            if (latestScore.game_complete)
            {
                GUI.Label(new Rect(bannerX, bannerY, bannerW, bannerH / 2), "MISSION COMPLETE!", bannerStyle);
                bannerStyle.fontSize = 16;
                GUI.Label(new Rect(bannerX, bannerY + bannerH / 2, bannerW, bannerH / 2),
                    $"Time: {latestScore.elapsed_seconds}s", bannerStyle);
            }
            else
            {
                string line = $"Detected: {latestScore.detected}  |  LED: {latestScore.led_correct}";

                if (latestScore.@event == "gate_completed")
                    line = latestScore.gate_correct ? "Correct Tunnel!" : "Wrong Tunnel!";
                else if (latestScore.@event == "landing_completed")
                    line = $"Landing Confirmed! ({latestScore.landings})";

                GUI.Label(new Rect(bannerX, bannerY, bannerW, bannerH), line, bannerStyle);
            }
        }
    }*/
}
