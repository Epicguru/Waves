using JNetworking;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NetObject))]
public class NetObjectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        NetObject n = target as NetObject;

        if(Application.isPlaying && !n.HasNetID)
        {
            EditorGUILayout.HelpBox($"This object is not spawned! Call JNet.Spawn(obj){(JNet.IsServer ? " or press button" : "")} to spawn it.", MessageType.Warning, true);
            if (JNet.IsServer && GUILayout.Button("Spawn"))
            {
                JNet.Spawn(n);
            }
        }

        GUILayout.Label($"NetID: {n.NetID} {(n.HasNetID ? "" : " (not spawned)")}");
        GUILayout.Label($"PrefabID: {n.PrefabID} {(n.HasPrefabID ? "" : " (not registered)")}");
        GUILayout.Label($"Owner ID: {n.OwnerID} {(n.OwnerID != 0 ? (JNet.IsClient ? (JNet.GetClient().UniqueIdentifier == n.OwnerID ? "(Local client owns this)" : "(remote client owns this)") : "") : " (server owns this)")}");
        GUILayout.Label($"Behaviour count: {n.NetBehaviours?.Length ?? 0}");
        GUILayout.Space(5);

        n._debugShowComps = GUILayout.Toggle(n._debugShowComps, "Show Net Behaviours");

        if(n.NetBehaviours != null)
        {
            if (n._debugShowComps)
            {
                foreach (var b in n.NetBehaviours)
                {
                    GUILayout.Label(b.GetType().Name + ":");
                    GUILayout.Label($"   ID: {b.BehaviourID}");
                    GUILayout.Label($"   Syncvars: {b.CustomGeneratedBehaviour?.SyncVarCount ?? 0}");
                    GUILayout.Label($"   Last serialized frame: {b.LastSerializedFrame}");
                    GUILayout.Label($"   Last deserialized frame: {b.LastDeserializedFrame}");
                }
            }            
        }
    }
}