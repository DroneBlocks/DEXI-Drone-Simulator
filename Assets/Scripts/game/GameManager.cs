using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Core game manager for the simulation challenge.
/// Handles target randomization, timer, scoring, and LED color coordination.
/// Timer uses unscaledDeltaTime so Time.timeScale can't affect scoring.
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

    [Header("LED Colors")]
    [Tooltip("LED color shown when landing is confirmed")]
    public Color landingConfirmColor = Color.yellow;

    private int targetApriltagID = 0;
    private string targetBridgeClass = "";
    private string targetCabinClass = "";

    private List<ScanTarget> allTargets = new List<ScanTarget>();
    private List<LandingZone> allLandingZones = new List<LandingZone>();
    private List<FlyThroughGate> allGates = new List<FlyThroughGate>();

    private int targetsScanned;
    private bool landingComplete;
    private int gatesCompleted;

    private ApriltagSubscriber apriltagSubscriber;

    // Events
    public System.Action<ScanTarget> OnTargetScanned;
    public System.Action<LandingZone> OnLandingDetected;
    public System.Action<FlyThroughGate> OnGateTriggered;
    public System.Action OnGameStarted;
    public System.Action<float> OnGameCompleted;

    private bool hasInitialized;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;

        apriltagSubscriber = GetComponent<ApriltagSubscriber>();
    }

    private void Start()
    {
        apriltagSubscriber.OnApriltagDetectionsReceived += HandleApriltagDetection;
    }

    private void HandleApriltagDetection(ApriltagDetectionArray array)
    {
        if (array.detections[0].id == targetApriltagID)
        {
            Debug.Log("Target Apriltag detected by DEXI!");
        }
    }

    void Update()
    {
        if (!hasInitialized && allTargets.Count > 0)
        {
            hasInitialized = true;
            RandomizeTargets();
        }

        if (state == GameState.Running)
        {
            elapsedTime += Time.unscaledDeltaTime;
            CheckCompletion();
        }

        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.rKey.wasPressedThisFrame)
        {
            ResetGame();
        }
        else if (kb.spaceKey.wasPressedThisFrame && state == GameState.WaitingToStart)
        {
            StartGame();
        }
    }

    public List<ScanTarget> GetAllTargets() => allTargets;

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

            if (target.targetType == ScanTarget.TargetType.AprilTag)
            {
                targetApriltagID = target.apriltagID;
            }
            else if (target.targetType == ScanTarget.TargetType.YoloImage)
            {
                switch (target.yoloPlace)
                {
                    case ScanTarget.YoloTargetPlace.Bridge:
                        targetBridgeClass = target.yoloLabel;
                    break;

                    case ScanTarget.YoloTargetPlace.Cabin:
                        targetCabinClass = target.yoloLabel;
                    break;
                }
            }

            Debug.Log($"GameManager: Group '{group.Key}' — target '{target.targetName}' is REAL " +
                      $"at position ({target.transform.position.x:F1}, {target.transform.position.z:F1})");
        }
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
            RandomizeTargets();

        elapsedTime = 0f;
        targetsScanned = 0;
        landingComplete = false;
        gatesCompleted = 0;
        state = GameState.Running;

        Debug.Log("GameManager: Game STARTED");
        OnGameStarted?.Invoke();
    }

    public void ReportScan(ScanTarget target)
    {
        if (state != GameState.Running) return;
        if (target.IsScanned) return;

        target.MarkScanned();
        targetsScanned++;

        Debug.Log($"GameManager: Target validated — '{target.targetName}' (group: {target.groupName}). " +
                  $"LED: {ColorToName(target.expectedLEDColor)}. [{targetsScanned} scanned]");

        OnTargetScanned?.Invoke(target);
    }

    public void ReportLanding(LandingZone zone)
    {
        if (state != GameState.Running) return;
        if (zone.IsLanded) return;

        zone.MarkLanded();
        landingComplete = true;

        SetDroneLEDColor(landingConfirmColor);
        Debug.Log($"GameManager: Landing confirmed on '{zone.zoneName}'. LED → {ColorToName(landingConfirmColor)}");

        OnLandingDetected?.Invoke(zone);
    }

    public void ReportGate(FlyThroughGate gate)
    {
        if (state != GameState.Running) return;

        gatesCompleted++;
        Debug.Log($"GameManager: Gate '{gate.gateName}' completed! [{gatesCompleted}/{allGates.Count}]");

        OnGateTriggered?.Invoke(gate);
    }

    [ContextMenu("Reset Game")]
    public void ResetGame()
    {
        state = GameState.WaitingToStart;
        elapsedTime = 0f;
        targetsScanned = 0;
        landingComplete = false;
        gatesCompleted = 0;

        foreach (var t in allTargets)
            t.ResetState();
        foreach (var z in allLandingZones)
            z.ResetState();
        foreach (var g in allGates)
            g.ResetState();

        RandomizeTargets();
        Debug.Log("GameManager: Game RESET");
    }

    private void CheckCompletion()
    {
        int realTargetCount = allTargets.Count(t => t.IsReal);
        bool allRealScanned = targetsScanned >= realTargetCount && realTargetCount > 0;
        bool allLandingsDone = allLandingZones.Count == 0 || landingComplete;
        bool allGatesDone = allGates.Count == 0 || gatesCompleted >= allGates.Count;

        if (allRealScanned && allLandingsDone && allGatesDone)
        {
            state = GameState.Completed;
            Debug.Log($"GameManager: Game COMPLETED in {elapsedTime:F2}s");
            OnGameCompleted?.Invoke(elapsedTime);
        }
    }

    private void SetDroneLEDColor(Color color)
    {
        var ledVis = FindFirstObjectByType<LEDRingVisualizer>();
        if (ledVis != null)
        {
            ledVis.SetAllLEDs(color);
        }
    }

    private string ColorToName(Color c)
    {
        if (c == Color.red) return "RED";
        if (c == Color.blue) return "BLUE";
        if (c == Color.green) return "GREEN";
        if (c == Color.yellow) return "YELLOW";
        return $"({c.r:F1},{c.g:F1},{c.b:F1})";
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
            bool found = realTarget != null && realTarget.IsScanned;
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
    }
}
