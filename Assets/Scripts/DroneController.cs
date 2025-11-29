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

    private float yaw;
    private float finalPitch;
    private float finalRoll;
    private float finalYaw;


    private void Start()
    {
        inputs = GetComponent<DroneInputs>();
        motors = GetComponentsInChildren<IMotor>().ToList<IMotor>();
    }


    protected override void HandlePhysics()
    {
        HandleMotors();
        HandleControls();
    }

    protected virtual void HandleMotors()
    {
        foreach (IMotor motor in motors)
        {
            motor.UpdateMotor(rb, inputs);
        }
        
    }

    protected virtual void HandleControls()
    {
        float pitch = inputs.Cyclic.y * minMaxPitch;
        float roll = -inputs.Cyclic.x * minMaxRoll;
        yaw += inputs.Yaw * yawPower;

        finalPitch = Mathf.Lerp(finalPitch, pitch, Time.deltaTime * lerpSpeed);
        finalRoll = Mathf.Lerp(finalRoll, roll, Time.deltaTime * lerpSpeed);
        finalYaw = Mathf.Lerp(finalYaw, yaw, Time.deltaTime * lerpSpeed);

        Quaternion rotation = Quaternion.Euler(finalPitch, finalYaw, finalRoll);
        rb.MoveRotation(rotation);
    }

    protected virtual void HandleControls2()
    {
        // Get input values
        float pitch = inputs.Cyclic.y * minMaxPitch;
        float roll = inputs.Cyclic.x * minMaxRoll;
        yaw += inputs.Yaw * yawPower;

        // Calculate torque forces
        Vector3 pitchTorque = transform.right * pitch;
        Vector3 rollTorque = transform.forward * roll;
        Vector3 yawTorque = transform.up * yaw;

        // Apply combined torque
        rb.AddTorque(pitchTorque + rollTorque + yawTorque, ForceMode.Force);
    }
    
    
    
}