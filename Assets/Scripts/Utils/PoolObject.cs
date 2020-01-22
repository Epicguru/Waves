
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


[DisallowMultipleComponent]
public class PoolObject : MonoBehaviour
{
    private static Dictionary<int, Queue<PoolObject>> pooled = new Dictionary<int, Queue<PoolObject>>();
    private static GameObject parent;

    public int PrefabID
    {
        get
        {
            return _id;
        }
    }
    [SerializeField]
    private int _id;

    private static Transform GetParent()
    {
        if (parent == null)
            parent = new GameObject("Pooled Objects");

        return parent.transform;
    }

    private static PoolObject GetFromPool(int id)
    {
        if (!pooled.ContainsKey(id))
            return null;

        if (pooled[id].Count == 0)
            return null;

        return pooled[id].Dequeue();
    }

    private static void AddToPool(PoolObject obj)
    {
        if (obj == null)
            return;

        if (!pooled.ContainsKey(obj.PrefabID))
        {
            pooled.Add(obj.PrefabID, new Queue<PoolObject>());
        }
        pooled[obj.PrefabID].Enqueue(obj);
    }

    private static PoolObject CreateNew(PoolObject prefab)
    {
        if (prefab == null)
            return null;

        prefab._id = prefab.GetInstanceID();
        var spawned = Instantiate(prefab);
        Debug.Assert(spawned.PrefabID == prefab._id);

        return spawned;
    }

    public static PoolObject Spawn(PoolObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("Null prefab, cannot spawn or get from pool.");
            return null;
        }

        var fromPool = GetFromPool(prefab.GetInstanceID());
        if (fromPool == null)
        {
            var sp = CreateNew(prefab);
            sp.transform.SetParent(null, false);
            sp.gameObject.SetActive(true);
            sp.InvokeUponSpawn();
            return sp;
        }
        else
        {
            fromPool.transform.parent.SetParent(null, false);
            fromPool.gameObject.SetActive(true);
            fromPool.InvokeUponSpawn();
            return fromPool;
        }
    }

    public static PoolObject Spawn(GameObject prefab)
    {
        if (prefab == null)
            return null;

        var comp = prefab.GetComponent<PoolObject>();
        if (comp != null)
        {
            return Spawn(comp);
        }
        else
        {
            Debug.LogWarning("Cannot spawn the PoolObject, not PoolObject component found.");
            return null;
        }
    }

    public static T Spawn<T>(T c) where T : Component
    {
        if (c == null)
            return null;

        var po = Spawn(c.gameObject);
        if (po == null)
            return null;

        return po.GetComponent<T>();
    }

    public static void Despawn(PoolObject obj)
    {
        if (obj == null)
            return;

        AddToPool(obj);
        obj.transform.SetParent(GetParent(), false);
        obj.InvokeUponDespawn();
        obj.gameObject.SetActive(false);
    }

    public static void Despawn(MonoBehaviour behaviour)
    {
        if (behaviour == null)
            return;

        Despawn(behaviour.gameObject);
    }

    public static void Despawn(GameObject go)
    {
        if (go == null)
            return;

        var comp = go.GetComponent<PoolObject>();
        if (comp != null)
            Despawn(comp);
    }

    public void Despawn()
    {
        PoolObject.Despawn(this);
    }

    private void InvokeUponSpawn()
    {
        if (UponSpawn != null)
            UponSpawn.Invoke();

        BroadcastMessage("UponSpawn", SendMessageOptions.DontRequireReceiver);
    }

    private void InvokeUponDespawn()
    {
        if (UponDespawn != null)
            UponDespawn.Invoke();

        BroadcastMessage("UponDespawn", SendMessageOptions.DontRequireReceiver);
    }

    public UnityEvent UponSpawn = new UnityEvent();
    public UnityEvent UponDespawn = new UnityEvent();
}
