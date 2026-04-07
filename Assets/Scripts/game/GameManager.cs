using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

/// <summary>
/// Core game manager for the simulation challenge.
/// Handles target randomization, timer, and answer key publishing.
/// Scoring is handled externally by the Node-RED validator via /game/score_update.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { WaitingToStart, Running, Completed }

    [Header("Game State")]
    [SerializeField] private GameState state = GameState.WaitingToStart;
    public GameState State => state;

    [Header("Timer")]
    [SerializeField] private float elapsedTime;
    public float ElapsedTime => elapsedTime;

    private List<ScanTarget> allTargets = new List<ScanTarget>();
    private List<LandingZone> allLandingZones = new List<LandingZone>();
    private List<FlyThroughGate> allGates = new List<FlyThroughGate>();

    private GameScoreSubscriber scoreSubscriber;

    private GameScoreUpdate latestScore;
    private float scoreMessageTimer;
    private const float SCORE_MESSAGE_DURATION = 4f;

    private bool hasInitialized;
    private string answerKeyTopic;
    private string gateEventTopic;
    private string landingEventTopic;
    private string finalTimeTopic;
    private bool hasAdvertisedTopics;
    private float answerKeyRepublishTimer;
    private const float ANSWER_KEY_REPUBLISH_INTERVAL = 5f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        scoreSubscriber = GetComponent<GameScoreSubscriber>();
    }

    private void Start()
    {
        scoreSubscriber.OnScoreUpdateReceived += HandleScoreUpdate;

        answerKeyTopic = ROSBridgeManager.Instance.ApplyNamespace("/game/answer_key");
        gateEventTopic = ROSBridgeManager.Instance.ApplyNamespace("/game/gate_event");
        landingEventTopic = ROSBridgeManager.Instance.ApplyNamespace("/game/landing_event");
        finalTimeTopic = ROSBridgeManager.Instance.ApplyNamespace("/game/final_time");
        ROSBridgeManager.Instance.OnConnected += OnROSConnected;

        if (ROSBridgeManager.Instance.IsConnected)
            AdvertiseTopics();
    }

    private void OnROSConnected()
    {
        hasAdvertisedTopics = false;
        AdvertiseTopics();

        if (hasInitialized)
            PublishAnswerKey();
    }

    private void AdvertiseTopics()
    {
        if (hasAdvertisedTopics) return;
        ROSBridgeManager.Instance.Advertise(answerKeyTopic, "std_msgs/msg/String");
        ROSBridgeManager.Instance.Advertise(gateEventTopic, "std_msgs/msg/String");
        ROSBridgeManager.Instance.Advertise(landingEventTopic, "std_msgs/msg/String");
        ROSBridgeManager.Instance.Advertise(finalTimeTopic, "std_msgs/msg/String");
        hasAdvertisedTopics = true;
    }

    private void HandleScoreUpdate(GameScoreUpdate score)
    {
        latestScore = score;
        scoreMessageTimer = SCORE_MESSAGE_DURATION;

        if (score.game_complete && state == GameState.Running)
        {
            state = GameState.Completed;
            score.elapsed_seconds = elapsedTime.ToString("F2");
            PublishFinalTime();
            Debug.Log($"GameManager: Game COMPLETED in {elapsedTime:F2}s");
        }

        Debug.Log($"GameManager: Score update — {score.detected} detected, {score.led_correct} LED correct, complete: {score.game_complete}");
    }

    void Update()
    {
        if (scoreMessageTimer > 0f)
            scoreMessageTimer -= Time.unscaledDeltaTime;

        if (!hasInitialized && allTargets.Count > 0)
        {
            hasInitialized = true;
            RandomizeTargets();
        }

        if (state == GameState.Running)
        {
            elapsedTime += Time.unscaledDeltaTime;
        }

        // Re-publish answer key periodically so late subscribers receive it
        if (hasInitialized && ROSBridgeManager.Instance.IsConnected)
        {
            answerKeyRepublishTimer -= Time.unscaledDeltaTime;
            if (answerKeyRepublishTimer <= 0f)
            {
                answerKeyRepublishTimer = ANSWER_KEY_REPUBLISH_INTERVAL;
                PublishAnswerKey();
            }
        }

        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.rKey.wasPressedThisFrame)
        {
            ResetGame();
        }
        else if ((kb.spaceKey.wasPressedThisFrame || kb.tKey.wasPressedThisFrame) && state == GameState.WaitingToStart)
        {
            StartGame();
        }
    }

    public void RegisterTarget(ScanTarget target)
    {
        if (!allTargets.Contains(target))
        {
            allTargets.Add(target);
        }
    }

    public void RegisterLandingZone(LandingZone zone)
    {
        if (!allLandingZones.Contains(zone))
        {
            allLandingZones.Add(zone);
        }
    }

    public void RegisterGate(FlyThroughGate gate)
    {
        if (!allGates.Contains(gate))
        {
            allGates.Add(gate);
        }
    }

    [ContextMenu("Randomize Targets")]
    public void RandomizeTargets()
    {
        var groups = allTargets.GroupBy(t => t.groupName);

        foreach (var group in groups)
        {
            var targets = group.ToList();
            if (targets.Count == 0) continue;

            ShufflePositions(targets);

            foreach (var t in targets)
            {
                t.SetReal(false);
            }

            int realIndex = Random.Range(0, targets.Count);
            ScanTarget target = targets[realIndex];

            target.SetReal(true);

            Debug.Log($"GameManager: Group '{group.Key}' — target '{target.targetName}' is REAL " + $"at position ({target.transform.position.x:F1}, {target.transform.position.z:F1})");
        }

        PublishAnswerKey();
    }

    private async void PublishAnswerKey()
    {
        if (!ROSBridgeManager.Instance.IsConnected) return;

        var answerKey = new AnswerKey();
        var groups = allTargets.GroupBy(t => t.groupName);

        foreach (var group in groups)
        {
            ScanTarget realTarget = group.FirstOrDefault(t => t.IsReal);
            if (realTarget == null) continue;

            answerKey.targets.Add(new AnswerKeyTarget
            {
                group = group.Key,
                target_name = realTarget.targetName,
                target_type = realTarget.targetType == ScanTarget.TargetType.AprilTag ? "apriltag" : "yolo",
                apriltag_id = realTarget.apriltagID,
                yolo_class = realTarget.yoloLabel,
                led_color = ColorToName(realTarget.expectedLEDColor)
            });
        }

        var msg = new { data = JsonConvert.SerializeObject(answerKey) };
        await ROSBridgeManager.Instance.Publish(answerKeyTopic, "std_msgs/msg/String", msg);
        Debug.Log($"GameManager: Published answer key with {answerKey.targets.Count} targets to {answerKeyTopic}");
    }

    [System.Serializable]
    private class AnswerKey
    {
        public List<AnswerKeyTarget> targets = new List<AnswerKeyTarget>();
    }

    [System.Serializable]
    private class AnswerKeyTarget
    {
        public string group;
        public string target_name;
        public string target_type;
        public int apriltag_id;
        public string yolo_class;
        public string led_color;
    }

    private void ShufflePositions(List<ScanTarget> targets)
    {
        Vector3[] positions = targets.Select(t => t.transform.position).ToArray();

        for (int i = positions.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (positions[i], positions[j]) = (positions[j], positions[i]);
        }

        for (int i = 0; i < targets.Count; i++)
        {
            targets[i].transform.position = positions[i];
        }
    }

    [ContextMenu("Start Game")]
    public void StartGame()
    {
        if (state != GameState.WaitingToStart)
        {
            Debug.LogWarning("GameManager: Game already started or completed. Reset first.");
            return;
        }

        if (allTargets.All(t => !t.IsReal))
        {
            RandomizeTargets();
        }

        elapsedTime = 0f;
        latestScore = null;
        state = GameState.Running;

        Debug.Log("GameManager: Game STARTED");
    }

    [ContextMenu("Reset Game")]
    public void ResetGame()
    {
        state = GameState.WaitingToStart;
        elapsedTime = 0f;
        latestScore = null;

        foreach (var t in allTargets)
        {
            t.ResetState();
        }

        foreach (var z in allLandingZones)
        {
            z.ResetState();
        }

        foreach (var g in allGates)
        {
            g.ResetState();
        }

        RandomizeTargets();
        Debug.Log("GameManager: Game RESET");
    }

    public void ReportLanding(LandingZone zone)
    {
        if (state != GameState.Running) return;
        if (zone.IsLanded) return;

        zone.MarkLanded();
        Debug.Log($"GameManager: Landing confirmed on '{zone.zoneName}'");
        PublishLandingEvent(zone);
    }

    public void ReportGate(FlyThroughGate gate)
    {
        if (state != GameState.Running) return;

        string linkedTarget = gate.linkedScanTarget != null ? gate.linkedScanTarget.targetName : "unknown";
        bool isCorrect = gate.linkedScanTarget != null && gate.linkedScanTarget.IsReal;

        Debug.Log($"GameManager: Gate '{gate.gateName}' completed! Linked: {linkedTarget}, Correct: {isCorrect}");
        PublishGateEvent(gate);
    }

    private async void PublishGateEvent(FlyThroughGate gate)
    {
        if (!ROSBridgeManager.Instance.IsConnected) return;

        var gateEvent = new
        {
            gate_name = gate.gateName,
            linked_target = gate.linkedScanTarget != null ? gate.linkedScanTarget.targetName : "",
            is_correct = gate.linkedScanTarget != null && gate.linkedScanTarget.IsReal
        };

        var msg = new { data = JsonConvert.SerializeObject(gateEvent) };
        await ROSBridgeManager.Instance.Publish(gateEventTopic, "std_msgs/msg/String", msg);
    }

    private async void PublishFinalTime()
    {
        if (!ROSBridgeManager.Instance.IsConnected) return;

        var finalTime = new
        {
            time = elapsedTime,
            time_formatted = $"{elapsedTime:F2}s"
        };

        var msg = new { data = JsonConvert.SerializeObject(finalTime) };
        await ROSBridgeManager.Instance.Publish(finalTimeTopic, "std_msgs/msg/String", msg);
        Debug.Log($"GameManager: Published final time {elapsedTime:F2}s to {finalTimeTopic}");
    }

    private async void PublishLandingEvent(LandingZone zone)
    {
        if (!ROSBridgeManager.Instance.IsConnected) return;

        var landingEvent = new
        {
            zone_name = zone.zoneName
        };

        var msg = new { data = JsonConvert.SerializeObject(landingEvent) };
        await ROSBridgeManager.Instance.Publish(landingEventTopic, "std_msgs/msg/String", msg);
    }

    private string ColorToName(Color c)
    {
        if (c == Color.red) return "RED";
        if (c == Color.blue) return "BLUE";
        if (c == Color.green) return "GREEN";
        if (c == Color.yellow) return "YELLOW";
        return $"({c.r:F1},{c.g:F1},{c.b:F1})";
    }

    private void OnDestroy()
    {
        if (ROSBridgeManager.Instance != null)
            ROSBridgeManager.Instance.OnConnected -= OnROSConnected;
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = Color.white;

        float y = 60f;

        string stateText = state switch
        {
            GameState.WaitingToStart => "READY — Press [Space] to start",
            GameState.Running => "RUNNING",
            GameState.Completed => "COMPLETED!",
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
    }
}
