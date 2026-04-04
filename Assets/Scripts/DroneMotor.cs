using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class DroneMotor : MonoBehaviour, IMotor
{

    [Header("Motor Properties")]
    [SerializeField]
    private float maxPower = 4f;

    [Header("Propeller Properties")]
    [SerializeField]
    private float baseRotationSpeed = 1500f;
    [SerializeField]
    private float maxRotationSpeed = 15000f;
    [SerializeField]
    private bool isClockwise = true; // true for clockwise, false for counter-clockwise


    public void UpdateMotor(Rigidbody rb, DroneInputs inputs)
    {
        // Don't apply forces when disarmed
        if (!PX4StateManager.Instance.IsArmed)
        {
            HandlePropellers(inputs.Throttle);
            return;
        }

        // Keep the drone level while rolling and pitching
        Vector3 upVec = transform.up;
        upVec.x = 0;
        upVec.z = 0;
        float diff = 1 - upVec.magnitude;
        float finalDiff = Physics.gravity.magnitude * diff;

        Vector3 motorForce = Vector3.zero;
        motorForce = transform.up * ((rb.mass * Physics.gravity.magnitude + finalDiff) + (inputs.Throttle * maxPower)) / 4f;

        rb.AddForce(motorForce, ForceMode.Force);

        HandlePropellers(inputs.Throttle);
    }

    private Transform propeller;

    void HandlePropellers(float throttle)
    {
        if(!propeller)
        {
            if (transform.childCount > 0)
                propeller = transform.GetChild(0);
            if (!propeller) return;
        }

        // Don't rotate propellers if the drone is not armed
        if(!PX4StateManager.Instance.IsArmed)
        {
            return;
        }

        // Calculate rotation speed based on throttle
        float currentRotationSpeed = Mathf.Lerp(baseRotationSpeed, maxRotationSpeed, throttle);

        // Apply rotation direction based on isClockwise property
        float direction = isClockwise ? 1f : -1f;
        propeller.Rotate(Vector3.up, currentRotationSpeed * direction * Time.fixedDeltaTime);
    }
}