using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;


public class AnswerKey
{
    public List<AnswerKeyTarget> targets = new List<AnswerKeyTarget>();
}

public class AnswerKeyTarget
{
    public string group;
    public string target_name;
    public string target_type;
    public int apriltag_id;
    public string yolo_class;
    public string led_color;
}

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
    public List<ScanTarget> AllTargets => allTargets;
    public List<LandingZone> AllLandingZones => allLandingZones;
    public List<FlyThroughGate> AllGates => allGates;

    [Header("Scoring")]
    public int scanMultiplier = 1;
    public int ledMultiplier = 3;
    public int bridgeMultiplier = 10;
    public int landingMultiplier = 5;

    public GameScoreUpdate LatestScore => latestScore;

    public int baseScore = 10;
    public int finalScore = 0;

    private List<ScanTarget> allTargets = new List<ScanTarget>();
    private List<LandingZone> allLandingZones = new List<LandingZone>();
    private List<FlyThroughGate> allGates = new List<FlyThroughGate>();

    private GameScoreSubscriber scoreSubscriber;

    private GameScoreUpdate latestScore;
    private float scoreMessageTimer;
    private const float SCORE_MESSAGE_DURATION = 4f;

    private bool hasInitialized;
    private string answerKeyTopic;
    private string totalPointsTopic;
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
        totalPointsTopic = ROSBridgeManager.Instance.ApplyNamespace("/game/total_points");

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
        ROSBridgeManager.Instance.Advertise(totalPointsTopic, "std_msgs/msg/String");
        hasAdvertisedTopics = true;
    }

    private void HandleScoreUpdate(GameScoreUpdate score)
    {
        latestScore = score;
        scoreMessageTimer = SCORE_MESSAGE_DURATION;

        string[] scannedSplit = score.detected.Split('/');
        string[] ledSplit = score.led_correct.Split('/');

        int.TryParse(scannedSplit[0], out int targetsScanned);
        int.TryParse(ledSplit[0], out int ledsCorrect);

        int scannedPoints = targetsScanned * scanMultiplier;
        int ledPoints = ledsCorrect * ledMultiplier;
        int bridgePoints = score.gate_correct ? bridgeMultiplier : 0;
        int landingPoints = score.landings * landingMultiplier;

        float totalPoints = scannedPoints + ledPoints + bridgePoints + landingPoints;
        float timeFactor = 1f / Mathf.Sqrt(elapsedTime);
        float scaledScore = totalPoints * timeFactor;

        finalScore = Mathf.Max(baseScore, (int)scaledScore);

        // Treat first landing as end-of-run, whether or not all targets/LEDs/gate are done.
        // Without this, partial runs never publish final_time/total_points and the
        // leaderboard falls back to a wrong wall clock.
        if ((score.game_complete || score.landings > 0) && state == GameState.Running)
        {
            state = GameState.Completed;
            score.elapsed_seconds = elapsedTime.ToString("F2");

            PublishFinalTime();
            PublishTotalPoints();

            Debug.Log($"GameManager: Run ended in {elapsedTime:F2}s (complete: {score.game_complete}, landings: {score.landings})");
        }

        Debug.Log($"GameManager: Score update — {score.detected} detected, {score.led_correct} LED correct, complete: {score.game_complete}");
    }

    void Update()
    {
        if (scoreMessageTimer > 0f)
        {
            scoreMessageTimer -= Time.unscaledDeltaTime;
        }

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
        else if (kb.tKey.wasPressedThisFrame && state == GameState.WaitingToStart)
        {
            // moved to ui
            //StartGame();
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

            int realIndex = UnityEngine.Random.Range(0, targets.Count);
            ScanTarget target = targets[realIndex];
            target.SetReal(true);

            Debug.Log($"GameManager: Group '{group.Key}' — target '{target.targetName}' is REAL " + $"at position ({target.transform.position.x:F1}, {target.transform.position.z:F1})");
        }

        foreach (var gate in allGates)
        {
            gate.linkedScanTarget = allTargets
                .OrderBy(t => Vector3.Distance(gate.transform.position, t.transform.position))
                .FirstOrDefault();
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

    private void ShufflePositions(List<ScanTarget> targets)
    {
        Vector3[] positions = targets.Select(t => t.transform.position).ToArray();

        for (int i = positions.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
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

    private async void PublishTotalPoints()
    {
        if (!ROSBridgeManager.Instance.IsConnected) return;

        var totalPoints = new
        {
            points = finalScore
        };

        var msg = new { data = JsonConvert.SerializeObject(totalPoints) };
        await ROSBridgeManager.Instance.Publish(totalPointsTopic, "std_msgs/msg/String", msg);
        Debug.Log($"GameManager: Published total points {finalScore} pts to {totalPointsTopic}");
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

    public string ColorToName(Color c)
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
        {
            ROSBridgeManager.Instance.OnConnected -= OnROSConnected;
        }
    }
}
