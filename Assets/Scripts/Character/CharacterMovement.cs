using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Character))]
public class CharacterMovement : MonoBehaviour
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
    public Character Character
    {
        get
        {
            if (_char == null)
                _char = GetComponent<Character>();
            return _char;
        }
    }
    private Character _char;

    public Vector2 InputDirection;
    public float Speed = 4;

    [Header("Movement")]
    public float MaxForce = 10f;

    [Header("Feet")]
    public Animator FeetAnim;
    public float SpeedAnimScale = 1f;

    public bool IsMoving { get { return GetVelocity().sqrMagnitude > 0.5f; } }

    private void Update()
    {
        // Runs on all clients.

        FeetAnim.SetBool("Move", IsMoving);
        FeetAnim.SetFloat("Speed", GetVelocity().magnitude * SpeedAnimScale);

        Vector2 rv = GetNormalizedVelocityInDirection(-transform.up);

        FeetAnim.SetLayerWeight(1, rv.magnitude);
    }

    public Vector2 GetForwardDirection()
    {
        return transform.right;
    }

    public Vector2 GetRightDirection()
    {
        return -transform.up;
    }

    public Vector2 GetVelocity()
    {
        return Body.velocity;
    }

    public Vector2 GetVelocityInDirection(Vector2 direction)
    {
        Vector2 normalized = direction.sqrMagnitude == 1f ? direction : direction.normalized;

        return Vector2.Dot(GetVelocity(), normalized) * normalized;
    }

    public Vector2 GetNormalizedVelocityInDirection(Vector2 direction)
    {
        Vector2 normalized = direction.sqrMagnitude == 1f ? direction : direction.normalized;

        return Vector2.Dot(GetVelocity().normalized, normalized) * normalized;
    }

    public float GetSpeedInDirection(Vector2 direction)
    {
        Vector2 normalized = direction.sqrMagnitude == 1f ? direction : direction.normalized;

        Vector2 inDir = Vector2.Dot(GetVelocity(), normalized) * normalized;

        bool forwards = (inDir.normalized - direction.normalized).sqrMagnitude < 0.01f;
        if (forwards)
            return inDir.magnitude;
        else
            return inDir.magnitude * -1f;
    }

    private void FixedUpdate()
    {
        bool forceMode = false;

        if (forceMode)
        {
            float upSpeed = GetVelocity().y;
            float rightSpeed = GetVelocity().x;

            var input = InputDirection.normalized * Speed;

            float targetUp = input.y;
            float targetRight = input.x;

            float upDiff = targetUp - upSpeed;
            float rightDiff = targetRight - rightSpeed;

            const float SPEED_SENS = 1f;

            float forceX = Mathf.Clamp(rightDiff / SPEED_SENS, -1f, 1f) * MaxForce;
            float forceY = Mathf.Clamp(upDiff / SPEED_SENS, -1f, 1f) * MaxForce;

            Vector2 force = new Vector2(forceX, forceY);
            force = force.ClampToMagnitude(MaxForce);

            Body.AddForce(force);
        }
        else
        {
            Body.velocity = InputDirection.normalized * Speed;
        }        
    }
}
