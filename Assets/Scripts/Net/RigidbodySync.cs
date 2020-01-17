
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class RigidbodySync : NetworkBehaviour
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

    [Range(0f, 60f)]
    public float SendRate = 20f;

    private float timer = 0f;

    private SyncPositionData latest;
    private SyncPositionData lastSent;

    public SyncPositionData GetCurrentData()
    {
        return new SyncPositionData()
        {
            Position = Body.position,
            Velocity = Body.velocity,
            Rotation = Body.rotation,
            AngularVelocity = Body.angularVelocity,
            Time = (float)NetworkTime.time
        };
    }

    public bool ShouldSendData()
    {
        // TODO make conditions (done) and make client detect when no new messages come in and respond by snapping to last position.
        return true;
        //return lastSent.SqrDistanceTo(GetCurrentData()) >= 0.05f || Mathf.Abs(lastSent.AngleTo(GetCurrentData())) >= 1f;
    }

    private void LateUpdate()
    {
        if (!isServer)
            return;

        timer += Time.unscaledDeltaTime;
        if(timer >= 1f / SendRate && ShouldSendData())
        {
            timer = 0f;
            // Send data!
            RpcSendData(lastSent = GetCurrentData());
        }
    }

    [ClientRpc]
    private void RpcSendData(SyncPositionData data)
    {
        if (isServer)
            return;

        float currentTime = (float)NetworkTime.time;
        float sentTime = data.Time;
        float diff = currentTime - sentTime;

        // 'Simulate' the body in the time that the message took to arrive.
        latest = data.AdvanceTime(diff);
        //latest = data;

        Body.MovePosition(data.Position);
        Body.MoveRotation(data.Rotation);
        Body.velocity = data.Velocity;
        Body.angularVelocity = data.AngularVelocity * Mathf.Deg2Rad;
    }

    private void OnDrawGizmosSelected()
    {
        latest.DrawGizmo();
    }
}

public struct SyncPositionData
{
    public Vector2 Position;
    public Vector2 Velocity;

    public float Rotation;
    public float AngularVelocity;

    public float Time;

    public SyncPositionData AdvanceTime(float seconds)
    {
        Vector2 newPos = Position + Velocity * seconds;
        float newRotation = Rotation + AngularVelocity * seconds;

        return new SyncPositionData()
        {
            Position = newPos,
            Velocity = Velocity,
            Rotation = newRotation,
            AngularVelocity = AngularVelocity,
            Time = Time + seconds
        };
    }

    public float SqrDistanceTo(SyncPositionData other)
    {
        return (other.Position - this.Position).sqrMagnitude;
    }

    public float AngleTo(SyncPositionData other)
    {
        return other.Rotation - this.Rotation;
    }

    public void DrawGizmo()
    {
        Gizmos.color = Color.grey;
        Gizmos.DrawCube(Position, Vector3.one * 0.5f);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(Position, Position + Velocity);
    }
}
