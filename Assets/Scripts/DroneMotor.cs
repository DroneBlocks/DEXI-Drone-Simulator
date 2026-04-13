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


    private Transform propeller;

    public void HandlePropellers(DroneInputs inputs)
    {
        if(!propeller)
        {
            if (transform.childCount > 0)
                propeller = transform.GetChild(0);
            if (!propeller) return;
        }

        if(!PX4StateManager.Instance.IsArmed)
        {
            return;
        }

        float currentRotationSpeed = Mathf.Lerp(baseRotationSpeed, maxRotationSpeed, inputs.Throttle);

        float direction = isClockwise ? 1f : -1f;
        propeller.Rotate(Vector3.up, currentRotationSpeed * direction * Time.fixedDeltaTime);
    }
}