using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class DroneMotor : MonoBehaviour, IMotor
{

    [Header("Motor Properties")]
    [SerializeField]
    private float maxPower = 4f;

    [Header("Propeller Properties")]
    [SerializeField]
    private Transform propeller;
    [SerializeField]
    private float baseRotationSpeed = 30f;
    [SerializeField]
    private float maxRotationSpeed = 300f;
    [SerializeField]
    private bool isClockwise = true; // true for clockwise, false for counter-clockwise


    public void UpdateMotor(Rigidbody rb, DroneInputs inputs)
    {

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

    void HandlePropellers(float throttle)
    {
        if(!propeller)
        {
            return;
        }

        // Calculate rotation speed based on throttle
        float currentRotationSpeed = Mathf.Lerp(baseRotationSpeed, maxRotationSpeed, throttle);
        
        // Apply rotation direction based on isClockwise property
        float direction = isClockwise ? 1f : -1f;
        propeller.Rotate(Vector3.up, currentRotationSpeed * direction);
    }
}