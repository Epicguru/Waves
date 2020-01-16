
using Mirror;
using UnityEngine;

public class SyncListGameObject : SyncList<GameObject>
{
    protected override void SerializeItem(NetworkWriter writer, GameObject item)
    {
        writer.WriteGameObject(item);
    }

    protected override GameObject DeserializeItem(NetworkReader reader)
    {
        return reader.ReadGameObject();
    }
}