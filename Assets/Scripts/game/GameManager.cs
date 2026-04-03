using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Core game manager for the simulation challenge MVP.
/// Handles target randomization, timer, scoring, and LED color coordination.
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

    // Registered targets, zones, and gates
    private List<ScanTarget> allTargets = new List<ScanTarget>();
    private List<LandingZone> allLandingZones = new List<LandingZone>();
    private List<FlyThroughGate> allGates = new List<FlyThroughGate>();

    // Tracking
    private int targetsScanned;
    private bool landingComplete;
    private int gatesCompleted;

    // Events
    public System.Action<ScanTarget> OnTargetScanned;
    public System.Action<LandingZone> OnLandingDetected;
    public System.Action<FlyThroughGate> OnGateTriggered;
    public System.Action OnGameStarted;
    public System.Action<float> OnGameCompleted; // passes final time

    private bool hasInitialized;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        // Randomize on first frame after all targets have registered via Start()
        if (!hasInitialized && allTargets.Count > 0)
        {
            hasInitialized = true;
            RandomizeTargets();
        }

        if (state == GameState.Running)
        {
            elapsedTime += Time.deltaTime;
            CheckCompletion();
        }
    }

    /// <summary>
    /// Get all registered targets (used by LEDColorValidator).
    /// </summary>
    public List<ScanTarget> GetAllTargets() => allTargets;

    /// <summary>
    /// Register a scan target with the game manager.
    /// Called by ScanTarget.Start().
    /// </summary>
    public void RegisterTarget(ScanTarget target)
    {
        if (!allTargets.Contains(target))
            allTargets.Add(target);
    }

    /// <summary>
    /// Register a landing zone with the game manager.
    /// Called by LandingZone.Start().
    /// </summary>
    public void RegisterLandingZone(LandingZone zone)
    {
        if (!allLandingZones.Contains(zone))
            allLandingZones.Add(zone);
    }

    /// <summary>
    /// Register a fly-through gate with the game manager.
    /// Called by FlyThroughGate.Start().
    /// </summary>
    public void RegisterGate(FlyThroughGate gate)
    {
        if (!allGates.Contains(gate))
            allGates.Add(gate);
    }

    /// <summary>
    /// Randomize which targets are real vs fake.
    /// Call this before starting the game or from the editor setup.
    /// </summary>
    [ContextMenu("Randomize Targets")]
    public void RandomizeTargets()
    {
        // Group targets by their group name
        var groups = allTargets.GroupBy(t => t.groupName);

        foreach (var group in groups)
        {
            var targets = group.ToList();
            if (targets.Count == 0) continue;

            // Shuffle positions within the group
            ShufflePositions(targets);

            // Reset all to fake
            foreach (var t in targets)
                t.SetReal(false);

            // Pick one random target to be real
            int realIndex = Random.Range(0, targets.Count);
            targets[realIndex].SetReal(true);

            Debug.Log($"GameManager: Group '{group.Key}' — target '{targets[realIndex].targetName}' is REAL " +
                      $"at position ({targets[realIndex].transform.position.x:F1}, {targets[realIndex].transform.position.z:F1})");
        }
    }

    /// <summary>
    /// Shuffle the world positions of targets within a group.
    /// The GameObjects swap positions so a different target is at each slot every round.
    /// </summary>
    private void ShufflePositions(List<ScanTarget> targets)
    {
        // Collect current positions
        Vector3[] positions = targets.Select(t => t.transform.position).ToArray();

        // Fisher-Yates shuffle
        for (int i = positions.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (positions[i], positions[j]) = (positions[j], positions[i]);
        }

        // Apply shuffled positions
        for (int i = 0; i < targets.Count; i++)
        {
            targets[i].transform.position = positions[i];
        }
    }

    /// <summary>
    /// Start the game timer.
    /// </summary>
    [ContextMenu("Start Game")]
    public void StartGame()
    {
        if (state != GameState.WaitingToStart)
        {
            Debug.LogWarning("GameManager: Game already started or completed. Reset first.");
            return;
        }

        // Auto-randomize if not already done
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

    /// <summary>
    /// Called by ScanDetector when drone scans a target.
    /// </summary>
    /// <summary>
    /// Called by LEDColorValidator when a correct LED color is submitted.
    /// </summary>
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

    /// <summary>
    /// Called by LandingZone when drone lands.
    /// </summary>
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

    /// <summary>
    /// Called by FlyThroughGate when drone flies through.
    /// </summary>
    public void ReportGate(FlyThroughGate gate)
    {
        if (state == GameState.WaitingToStart)
            StartGame();
        if (state != GameState.Running) return;

        gatesCompleted++;
        Debug.Log($"GameManager: Gate '{gate.gateName}' completed! [{gatesCompleted}/{allGates.Count}]");

        OnGateTriggered?.Invoke(gate);
    }

    /// <summary>
    /// Reset the game to initial state.
    /// </summary>
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
            ledVis.SetAllLEDs(color);
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
        // Simple HUD overlay
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = Color.white;

        float y = 60f;

        // State
        string stateText = state switch
        {
            GameState.WaitingToStart => "READY — Fly over a target to begin",
            GameState.Running => "RUNNING",
            GameState.Completed => "COMPLETED!",
            _ => ""
        };
        GUI.Label(new Rect(10, y, 500, 28), stateText, style);
        y += 26;

        // Timer
        GUI.Label(new Rect(10, y, 300, 28), $"Time: {elapsedTime:F1}s", style);
        y += 26;

        // Scan progress per group
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

        // Gates
        foreach (var gate in allGates)
        {
            string label = gate.IsTriggered
                ? $"  {gate.gateName}: PASSED"
                : $"  {gate.gateName}: —";
            GUI.Label(new Rect(10, y, 300, 28), label, gate.IsTriggered ? completedStyle : pendingStyle);
            y += 24;
        }
        if (allGates.Count > 0) y += 4;

        // Landing
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

        // Answer Key — anchored to bottom-left
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

        // Controls hint
        GUIStyle smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        smallStyle.normal.textColor = new Color(1, 1, 1, 0.6f);
        GUI.Label(new Rect(10, Screen.height - 30, 400, 25), "[R] Reset  [Space] Start", smallStyle);

        // Handle input
        if (Event.current.type == EventType.KeyDown)
        {
            if (Event.current.keyCode == KeyCode.R)
                ResetGame();
            else if (Event.current.keyCode == KeyCode.Space && state == GameState.WaitingToStart)
                StartGame();
        }
    }
}
