using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BaseRigidBody : MonoBehaviour
{
    [Header("Rigidbody Settings")]
    [SerializeField]
    public float weightInLbs = 1f;

    [Header("Wind Settings")]
    public Vector3 windDirection = new Vector3(1, 0, 0); // Default wind along X
    public float windStrength = 0f; // Default no wind

    const float lbsToKg = 2.20462f;

    protected Rigidbody rb;
    protected float startDrag;
    protected float startAngularDrag;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rb)
        {
            rb.mass = weightInLbs * lbsToKg;
            startDrag = rb.linearDamping;
            startAngularDrag = rb.angularDamping;
        }
    }

    void FixedUpdate()
    {
        if (!rb)
        {
            return;
        }

        // Apply wind force
        if (windStrength != 0f && windDirection != Vector3.zero)
        {
            rb.AddForce(windDirection.normalized * windStrength, ForceMode.Force);
        }

        HandlePhysics();
    }

    protected virtual void HandlePhysics() { }
    


    
    

    
}
