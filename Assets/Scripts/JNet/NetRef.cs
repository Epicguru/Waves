
using Lidgren.Network;
using System;
using Unity.Collections;
using UnityEngine;

namespace JNetworking
{
    [System.Serializable]
    public sealed class NetRef
    {
        public Action<NetObject> UponObjectUpdate;
        public bool NetDirty { get; private set; }
        private ushort objID;
        private NetObject obj;
        private ushort lastSentDebug;
        /// <summary>
        /// Returns true if the reference has a value, as in Get() != null.
        /// On server this will work instantly when setting the value of the reference, but on remote clients it may take a few
        /// frames for the reference to update.
        /// </summary>
        public bool HasValue { get { return obj != null; } }

        public void Set(NetObject value)
        {
            if (!JNet.IsServer)
            {
                JNet.Error("Cannot set value of net reference when not on server.");
                return;
            }

            if (obj == value)
                return;

            obj = value;
            objID = value?.NetID ?? 0;

            NetDirty = true;
        }

        public NetObject Get()
        {
            return obj;
        }

        public T GetComponent<T>() where T : Component
        {
            if (obj == null)
                return null;
            return obj.GetComponent<T>();
        }

        public void Serialize(NetOutgoingMessage msg)
        {
            ushort sent;
            if(obj == null)
            {
                sent = 0;
                msg.Write((ushort)0);
            }
            else
            {
                if(obj.NetID == objID)
                {
                    sent = objID;
                    msg.Write(objID);
                }
                else
                {
                    objID = obj.NetID;
                    msg.Write(objID);
                    sent = objID;
                }
            }

            if(sent == (obj?.NetID ?? 0))
                NetDirty = false;

            lastSentDebug = sent;
        }

        public void Deserialize(NetIncomingMessage msg, bool first)
        {
            ushort id = msg.ReadUInt16();
            if(objID != id)
            {
                this.objID = id;
                var found = JNet.GetObject(id);
                if(found != obj || first)
                {
                    obj = found;
                    UponObjectUpdate?.Invoke(obj);
                }
            }
        }

        public bool Update()
        {
            if (JNet.IsServer)
                return NetDirty;

            if(objID == 0)
            {
                if(obj != null)
                {
                    obj = null;
                    UponObjectUpdate?.Invoke(null);
                }       
            }
            else
            {
                if(obj == null || obj.NetID != objID)
                {
                    var found = JNet.GetObject(objID);
                    if(found != null)
                    {
                        obj = found;
                        UponObjectUpdate?.Invoke(obj);
                    }
                }
            }

            return false;
        }
    }
}
