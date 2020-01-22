
using Mirror;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PoolObject))]
public class Projectile : MonoBehaviour
{
    public PoolObject PoolObject
    {
        get
        {
            if (_po == null)
                _po = GetComponent<PoolObject>();
            return _po;
        }
    }
    private PoolObject _po;

    [Header("Settings")]
    public float Speed = 20f;
    public float MaxDuration = 10f;
    public float MaxDistance = 0f;

    [Header("Collision")]
    public LayerMask Mask;

    [Header("Runtime")]
    public Vector2 Direction;

    private Vector2 startPos;
    private float timer;

    private void Fire()
    {
        startPos = transform.position;
        timer = 0f;
        GetComponent<TrailRenderer>().Clear();
    }

    public bool IsInsideMaxDistance()
    {
        if (MaxDistance <= 0f)
            return true;

        return ((Vector2)transform.position - startPos).sqrMagnitude <= MaxDistance * MaxDistance;
    }

    public bool IsWithinLifeTime()
    {
        if (MaxDuration <= 0f)
            return true;

        return timer <= MaxDuration;
    }

    private void Update()
    {
        // Should behave the same on clients and server, but on server it is authorative (deals damage, knockback etc.)

        // Check life time and distance travelled.
        if(!IsWithinLifeTime() || !IsInsideMaxDistance())
        {
            PoolObject.Despawn();
            return;
        }
        timer += Time.deltaTime;

        // Normalize direction.
        if (Direction.sqrMagnitude != 1f)
            Direction.Normalize();

        // Move!
        Vector2 currentPos = transform.position;
        Vector2 nextPos = currentPos + Direction * Speed * Time.deltaTime;

        Vector2 finalPos = ResolveCollisions(currentPos, nextPos);

        transform.position = finalPos;
    }

    /// <summary>
    /// Resolves all collisions that would arise when moving from the start to the end position.
    /// Should return the final position, which may be equal to the end position unless a deflection or hit occured.
    /// </summary>
    /// <param name="start">The starting position.</param>
    /// <param name="end">The target destination position.</param>
    /// <returns>The actual final position. This may or may not be equal to the 'end' parameter.</returns>
    private Vector2 ResolveCollisions(Vector2 start, Vector2 end)
    {
        // Cast a ray from start to end.

        var hit = Physics2D.Linecast(start, end, Mask);

        if(hit == false)
        {
            return end;
        }
        else
        {
            // We hit something!
            // In the future, better behaviour will be added, such as bullets deflecting and penetrating surfaces.
            // For now, just despawn once we hit the point.

            StartCoroutine(DespawnNextFrame());

            return hit.point;
        }
    }

    private IEnumerator DespawnNextFrame()
    {
        yield return new WaitForEndOfFrame();
        PoolObject.Despawn();
    }

    public static void Spawn(Projectile prefab, Vector2 position, Vector2 direction)
    {
        if (prefab == null)
        {
            Debug.LogWarning("Tried to spawn a projectile with null prefab.");
            return;
        }
        if (direction == Vector2.zero)
        {
            Debug.LogWarning("Tried to spawn projectile with zero direction. Projectile will not be spawned to avoid player confusion.");
            return;
        }

        if (!NetworkServer.active)
            return;

        SpawnLocal(prefab, position, direction);

        ProjectileMessage m = new ProjectileMessage();
        m.Direction = direction;
        m.Position = position;

        NetworkServer.SendToAll(m);
    }

    private static Projectile SpawnLocal(Projectile prefab, Vector2 position, Vector2 direction)
    {
        var spawned = PoolObject.Spawn(prefab);
        spawned.transform.position = position;
        spawned.Direction = direction;

        spawned.Fire();

        return spawned;
    }

    public static void Init()
    {
        NetworkClient.RegisterHandler<ProjectileMessage>(OnMessage);
    }

    private static void OnMessage(NetworkConnection c, ProjectileMessage m)
    {
        if (NetworkServer.active)
            return;

        //var prefab = ???;
        //SpawnLocal(prefab, m.Position, m.Direction);
    }
}