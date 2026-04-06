using UnityEngine;

public interface IMotor
{
    void InitMotor() {}

    void HandlePropellers(DroneInputs inputs) {}
}