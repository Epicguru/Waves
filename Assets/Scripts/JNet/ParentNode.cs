
using UnityEngine;

namespace JNetworking
{
    [DisallowMultipleComponent]
    public class ParentNode : MonoBehaviour, IParentNode
    {
        public NetObject NetObject
        {
            get
            {
                if (_no == null)
                    _no = GetComponentInParent<NetObject>();
                return _no;
            }
        }
        private NetObject _no;

        private byte id;

        public NetObject GetNetObject()
        {
            return NetObject;
        }

        public byte GetNodeID()
        {
            return id;
        }

        public Transform GetTransform()
        {
            return this.transform;
        }

        public void SetNodeID(byte id)
        {
            this.id = id;
        }
    }
}
