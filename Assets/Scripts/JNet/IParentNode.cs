
using UnityEngine;

namespace JNetworking
{
    public interface IParentNode
    {
        byte GetNodeID();
        void SetNodeID(byte id);
        Transform GetTransform();
        NetObject GetNetObject();
    }
}
