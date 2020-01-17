using Mirror;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Vehicle))]
public class CarPhysics : NetworkBehaviour
{   
    public Rigidbody2D Body
    {
        get
        {
            if (_body == null)
                _body = GetComponent<Rigidbody2D>();
            return _body;
        }
    }
    private Rigidbody2D _body;
    public Vehicle Vehicle
    {
        get
        {
            if (_veh == null)
                _veh = GetComponent<Vehicle>();
            return _veh;
        }
    }
    private Vehicle _veh;

    [Header("References")]
    public TrailRenderer[] Wheels;

    [Header("Input")]
    [Range(-1f, 1f)]
    public float TurnInput = 0;
    public float ForwardsInput = 0f;

    [Header("Handling")]
    public float TurnSpeed = 8f;
    [Range(0f, 1f)]
    public float MaxReverseForceScale = 1f;

    [Header("Forces")]
    public float MaxDriveForce = 50000f;
    public float MaxTurnTorque = 80000f;
    public float ResistiveForceCoefficient = 100f;
    public float MaxResistiveForce = 100000f;

    [Header("Turning")]
    public float FrontWheelMaxAngle = 35f;
    public float WheelTurnSpeed = 10f;
    public float WheelResetSpeed = 10f;
    public float BaseSpeedForTurn = 4f;

    [Header("Tire Trails")]
    public float MinTrailVel = 11f;

    [Header("Runtime")]
    [Range(-1f, 1f)]
    public float Forwards = 0f;
    [Range(-1f, 1f)]
    public float Turn = 0f;

    [Header("Debug")]
    public bool DrawDebug = false;

    [SyncVar]
    private Vector2 BodyVel = Vector2.zero;
    [SyncVar]
    private float BodyAngularVel = 0f;
    [SyncVar]
    private float WheelAngle = 0f;

    private void Update()
    {
        // Update input-to-drive (turning input into actual control).
        UpdateInputToDrive();

        // Update network related stuff.
        UpdateNet();

        // Update wheel trails. Works on both client and server.
        UpdateWheelTrails();

        // Update visual wheel turning. Works on both client and server.
        UpdateWheelTurn();

        // Draw debug
        if (DrawDebug && Application.isEditor)
        {
            Debug.DrawLine(transform.position, transform.position + transform.right * Forwards * 2f, Color.green);

            float angle = Vector2.SignedAngle(Body.velocity, transform.right);
            float dot = 1f - Mathf.Abs(Vector2.Dot(Body.velocity.normalized, transform.forward));
            float horizontalVel = transform.InverseTransformVector(Body.velocity).y;
            float resistiveForceScale = dot * ResistiveForceCoefficient * Mathf.Abs(horizontalVel);
            resistiveForceScale = Mathf.Min(resistiveForceScale, MaxResistiveForce);
            Debug.DrawLine(transform.position, transform.position + (resistiveForceScale / MaxResistiveForce) * transform.up * (angle > 0f ? 1f : -1f), resistiveForceScale == MaxResistiveForce ? Color.red : Color.blue);
        }
    }

    private void UpdateInputToDrive()
    {
        TurnInput = Mathf.Clamp(TurnInput, -1f, 1f);
        ForwardsInput = Mathf.Clamp(ForwardsInput, -1f, 1f);
        TurnSpeed = Mathf.Abs(TurnSpeed);

        if (!Vehicle.HasDriver)
        {
            ForwardsInput = 0f;
            TurnInput = 0f;
        }

        if(Turn < TurnInput)
        {
            Turn += Time.deltaTime * TurnSpeed;
            if (Turn > TurnInput)
                Turn = TurnInput;
        }
        else if(Turn > TurnInput)
        {
            Turn -= Time.deltaTime * TurnSpeed;
            if (Turn < TurnInput)
                Turn = TurnInput;
        }

        if (ForwardsInput >= 0f)
            Forwards = ForwardsInput;
        else
            Forwards = MaxReverseForceScale * ForwardsInput;
    }

    /// <summary>
    /// Networking related tweaks.
    /// </summary>
    private void UpdateNet()
    {
        // URGTODO fixme: removing from simulation also removes colliders, allowing cars to be walked through by client.
        return;

        bool sim = isServer;
        if (sim != Body.simulated)
            Body.simulated = sim;

        if (!sim)
        {
            Body.velocity = BodyVel;
            Body.angularVelocity = BodyAngularVel;
        }
        else
        {
            BodyVel = Body.velocity;
            BodyAngularVel = Body.angularVelocity;
        }
    }

    private void FixedUpdate()
    {

        // Update driving force.
        Body.AddForce(MaxDriveForce * Mathf.Clamp(Forwards, -1f, 1f) * transform.right);        

        // Update turning torque.
        Body.AddTorque(Mathf.Clamp(Turn, -1f, 1f) * MaxTurnTorque * Mathf.Clamp01(Body.velocity.magnitude / BaseSpeedForTurn));

        // Update resistive force (prevents car from sliding sideways)
        float angle = Vector2.SignedAngle(Body.velocity, transform.right);
        float dot = 1f - Mathf.Abs(Vector2.Dot(Body.velocity.normalized, transform.forward));
        float horizontalVel = transform.InverseTransformVector(Body.velocity).y;

        float resistiveForceScale = dot * ResistiveForceCoefficient * Mathf.Abs(horizontalVel);
        resistiveForceScale = Mathf.Min(resistiveForceScale, MaxResistiveForce);

        Body.AddForce(resistiveForceScale * transform.up * (angle > 0f ? 1f : -1f));
    }    

    public float GetFrontWheelAngle()
    {
        return Mathf.Clamp(Turn, -1f, 1f) * FrontWheelMaxAngle;
    }

    private void UpdateWheelTurn()
    {
        if (isServer)
            WheelAngle = GetFrontWheelAngle();

        Wheels[2].transform.localEulerAngles = new Vector3(0f, 0f, WheelAngle);
        Wheels[3].transform.localEulerAngles = new Vector3(0f, 0f, WheelAngle);
    }

    private void UpdateWheelTrails()
    {
        foreach (var wheel in Wheels)
        {
            Vector2 velAtWheel = Body.GetPointVelocity(wheel.transform.position);

            Vector2 localVel = transform.InverseTransformVector(velAtWheel);

            float horizontal = localVel.y;

            wheel.emitting = Mathf.Abs(horizontal) >= MinTrailVel;
        }
    }

    private void OnGUI()
    {
        GUILayout.Label($"Speed: {Body.velocity.magnitude * 3.6f:F1} kph.");
    }
}
