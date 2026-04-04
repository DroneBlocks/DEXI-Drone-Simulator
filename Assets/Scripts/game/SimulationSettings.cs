using UnityEngine;

/// <summary>
/// Enforces consistent simulation settings across all machines.
/// Caps frame rate so faster hardware doesn't give a competitive advantage.
/// </summary>
public class SimulationSettings : MonoBehaviour
{
    [Header("Frame Rate")]
    [Tooltip("Target frame rate for the simulation. Set to 0 for unlimited.")]
    public int targetFrameRate = 60;

    void Awake()
    {
        // Disable VSync so targetFrameRate is honored
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFrameRate;

        Debug.Log($"SimulationSettings: Frame rate capped to {targetFrameRate} FPS, VSync disabled");
    }
}
