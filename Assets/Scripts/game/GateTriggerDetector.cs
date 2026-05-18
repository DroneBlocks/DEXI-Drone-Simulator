using UnityEngine;

public class GateTriggerDetector : MonoBehaviour
{
    public FlyThroughGate gate;

    void OnTriggerEnter(Collider other)
    {
        DroneController drone = other.GetComponentInParent<DroneController>();

        if (other.CompareTag("Player"))
        {
            gate.OnDroneEntered();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            gate.OnDroneExited();
        }
    }
}