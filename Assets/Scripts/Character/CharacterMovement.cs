using Mirror;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Character))]
public class CharacterMovement : NetworkBehaviour
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

    public bool IsMoving { get { return GetVelocity().sqrMagnitude > 0.5f; } }

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
        if (!hasAuthority)
        {
            Body.velocity = Vector2.zero;
            return;
        }

        Body.velocity = InputDirection.normalized * Speed;    
    }
}
