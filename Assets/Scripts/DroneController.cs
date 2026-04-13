using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(DroneInputs))]

public class DroneController : BaseRigidBody
{
    [Header("Control Properties")]

    [SerializeField]
    private float minMaxPitch = 30f;
    [SerializeField]
    private float minMaxRoll = 30f;
    [SerializeField]
    private float yawPower = 4f;
    [SerializeField]
    private float lerpSpeed = 2f;

    private DroneInputs inputs;
    private List<IMotor> motors = new List<IMotor>();

    private DroneOdometrySubscriber odometry;

    private void Start()
    {
        inputs = GetComponent<DroneInputs>();
        motors = GetComponentsInChildren<IMotor>().ToList<IMotor>();
        odometry = GetComponent<DroneOdometrySubscriber>();
    }

    protected override void HandlePhysics()
    {
        if (odometry != null && odometry.FreeFlightOverride)
        {
            // Free flight: ROSKeyboardController moves the drone directly
            return;
        }

        if (ROSBridgeManager.Instance.IsConnected && odometry != null && odometry.HasReceivedData)
        {
            odometry.ApplyPhysics(rb);
        }

        foreach (IMotor motor in motors)
        {
            motor.HandlePropellers(inputs);
        }
    }
}