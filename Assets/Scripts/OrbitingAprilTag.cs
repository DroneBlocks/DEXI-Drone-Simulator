using UnityEngine;

/// <summary>
/// Moves an AprilTag GameObject in a horizontal circle around a target (the drone),
/// keeping the tag face always pointing at the target. Used for the Tag Follow teaching scene
/// where a drone must yaw to track a moving tag.
/// </summary>
public class OrbitingAprilTag : MonoBehaviour
{
    [Tooltip("The object the tag orbits around (usually the drone).")]
    public Transform target;

    [Tooltip("Distance from the target, in meters.")]
    [Range(0.5f, 10f)]
    public float radius = 2.5f;

    [Tooltip("Height above the target, in meters. 0 = same height as target.")]
    [Range(-2f, 5f)]
    public float heightOffset = 0f;

    [Tooltip("Orbit speed in radians per second. Negative = counter-clockwise.")]
    [Range(-1.5f, 1.5f)]
    public float orbitSpeed = 0.3f;

    [Tooltip("Starting angle in degrees (0 = in front of target, 90 = right side).")]
    [Range(0f, 360f)]
    public float startAngleDegrees = 0f;

    [Tooltip("Pause orbit motion. Position/rotation still update based on starting angle.")]
    public bool paused = false;

    [Tooltip("Start the orbit immediately at scene load. If false, orbit waits for the toggle key.")]
    public bool autoStart = false;

    [Tooltip("Key that toggles the orbit on/off when autoStart is false.")]
    public KeyCode toggleKey = KeyCode.O;

    private float currentAngle;
    private bool orbiting;

    void Start()
    {
        currentAngle = startAngleDegrees * Mathf.Deg2Rad;
        orbiting = autoStart;
        if (target == null)
        {
            // Try to find the drone by name if no target was assigned
            GameObject drone = GameObject.Find("DEXI");
            if (drone != null)
            {
                target = drone.transform;
            }
            else
            {
                Debug.LogWarning("OrbitingAprilTag: no target assigned and no 'DEXI' GameObject found.");
            }
        }
        Debug.Log(orbiting
            ? "[OrbitingAprilTag] orbit started automatically"
            : $"[OrbitingAprilTag] orbit waiting — press '{toggleKey}' to start/stop");
    }

    void Update()
    {
        if (target == null) return;

        // Toggle on/off with the configured key
        if (Input.GetKeyDown(toggleKey))
        {
            orbiting = !orbiting;
            Debug.Log(orbiting ? "[OrbitingAprilTag] orbit started" : "[OrbitingAprilTag] orbit paused");
        }

        if (orbiting && !paused)
        {
            currentAngle += orbitSpeed * Time.deltaTime;
        }

        // Position on a horizontal circle around the target
        Vector3 offset = new Vector3(
            Mathf.Sin(currentAngle) * radius,
            heightOffset,
            Mathf.Cos(currentAngle) * radius
        );
        transform.position = target.position + offset;

        // Face the target so the tag's printed side always looks at the drone's camera.
        // Unity's Quad primitive has its visible face on the local -Z side, so we point
        // the quad's +Z AWAY from the target — that way the textured face looks at it.
        Vector3 awayFromTarget = transform.position - target.position;
        if (awayFromTarget.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(awayFromTarget, Vector3.up);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (target == null) return;
        // Visualize the orbit path in the editor
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        Vector3 center = target.position + Vector3.up * heightOffset;
        int segments = 64;
        Vector3 prev = center + new Vector3(0, 0, radius);
        for (int i = 1; i <= segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Sin(a) * radius, 0, Mathf.Cos(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
