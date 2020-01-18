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
    private float WheelAngle = 0f;
    [SyncVar]
    private bool backWheelTrail, frontWheelTrail;

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
        return;
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

        if (isServer)
        {
            Wheels[2].transform.localEulerAngles = new Vector3(0f, 0f, WheelAngle);
            Wheels[3].transform.localEulerAngles = new Vector3(0f, 0f, WheelAngle);
        }
        else
        {
            float a = Wheels[2].transform.localEulerAngles.z;
            Wheels[2].transform.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpAngle(a, WheelAngle, Time.deltaTime * WheelTurnSpeed * 2f));
            Wheels[3].transform.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpAngle(a, WheelAngle, Time.deltaTime * WheelTurnSpeed * 2f));
        }
    }

    private void UpdateWheelTrails()
    {
        if(isServer)
        {
            for (int i = 0; i < Wheels.Length; i++)
            {
                // Wheels have trails when they are moving sideways above a certain speed.
                var wheel = Wheels[i];

                Vector2 velAtWheel = Body.GetPointVelocity(wheel.transform.position);
                Vector2 localVel = transform.InverseTransformVector(velAtWheel);
                float horizontal = localVel.y;

                wheel.emitting = Mathf.Abs(horizontal) >= MinTrailVel;

                // Tells clients when to emmit wheel trails, simplified to back and front wheels.
                // I tried to just allow the physics system to determine trail just like on the server, but it resulted in broken (discontinuous) trails
                // and unexpected trails when lag occured. So I'll just do it this way.
                if (i == 0)
                    backWheelTrail = wheel.emitting;
                if (i == 2)
                    frontWheelTrail = wheel.emitting;
            }
        }
        else
        {
            Wheels[0].emitting = backWheelTrail;
            Wheels[1].emitting = backWheelTrail;

            Wheels[2].emitting = frontWheelTrail;
            Wheels[3].emitting = frontWheelTrail;
        }        
    }

    private void OnGUI()
    {
        GUILayout.Label($"Speed: {Body.velocity.magnitude * 3.6f:F1} kph.");
    }
}
