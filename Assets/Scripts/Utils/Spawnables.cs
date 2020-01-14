
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class Spawnables : MonoBehaviour
{
    private static Spawnables Instance;

    public static T Get<T>(string name) where T : Object
    {
        name = name.Trim().ToLowerInvariant();

        if (map.ContainsKey(name))
        {
            Object obj = map[name];
            if (!(obj is T))
            {
                if (obj is GameObject && typeof(Component).IsAssignableFrom(typeof(T)))
                {
                    return (obj as GameObject).GetComponent<T>();
                }
                else
                {
                    return default(T);
                }
            }
            else
            {
                return obj as T;
            }
        }
        else
        {
            Debug.LogWarning($"Could not find {name}.");
            return default(T);
        }
    }

    public Object[] Objects;
    public static SortedDictionary<string, Object> map;

    private static string ListSpawnables()
    {
        StringBuilder str = new StringBuilder();
        foreach (var value in map.Values)
        {
            str.Append(value.name).Append(": ").AppendLine(value.GetType().Name);
        }

        return str.ToString();
    }

    private void Awake()
    {
        map = new SortedDictionary<string, Object>();
        foreach (var item in Objects)
        {
            if (item != null)
            {
                string name = item.name.Trim().ToLowerInvariant();
                map.Add(name, item);
                Debug.Log($"Registered '{name}' as {item.GetType().Name}.");
            }
        }
        Instance = this;
    }
}
