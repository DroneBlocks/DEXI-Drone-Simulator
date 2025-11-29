using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]

public class DroneInputs : MonoBehaviour
{
    public InputAction moveAction;

    private Vector2 cyclic;
    private float yaw;
    private float throttle;

    public Vector2 Cyclic { get => cyclic; }
    public float Yaw { get => yaw; }
    public float Throttle { get => throttle; }
    


    void Update()
    {

    }

    private void OnCyclic(InputValue value)
    {
        cyclic = value.Get<Vector2>();
    }

    private void OnYaw(InputValue value)
    {
        yaw = value.Get<float>();
    }

    private void OnThrottle(InputValue value)
    {
        throttle = value.Get<float>();
    }
    
}