
using JNetworking.Internal;
using Lidgren.Network;
using System.Collections.Generic;

namespace JNetworking
{
    internal class WorldStateTracker
    {
        internal const int MAX_TRACKED_OBJECTS = ushort.MaxValue - 1;

        public int ObjectCount { get; private set; }
        internal NetObject[] TrackedObjects = new NetObject[ushort.MaxValue];
        internal List<NetObject> ActiveObjects = new List<NetObject>();
        private ushort CurrentTopID = 1;

        public bool Reset()
        {
            bool didUnregister = false;
            CurrentTopID = 1;
            ObjectCount = 0;
            for (int i = 0; i < TrackedObjects.Length; i++)
            {
                var obj = TrackedObjects[i];
                if(obj != null)
                {
                    obj.NetID = 0;
                    didUnregister = true;
                }
                TrackedObjects[i] = null;
            }

            ActiveObjects.Clear();

            return didUnregister;
        }

        public void SerializeAll()
        {
            if (!JNet.IsServer)
            {
                JNet.Error("Not on server, cannot serialize all.");
                return;
            }

            for (int i = 0; i < ActiveObjects.Count; i++)
            {
                var obj = ActiveObjects[i];
                if(obj == null)
                {
                    JNet.Error("Null object in tracked objects list.");
                    ActiveObjects.RemoveAt(i);
                    i--;
                    continue;
                }

                if(obj.NetBehaviours != null)
                {
                    foreach (var b in obj.NetBehaviours)
                    {
                        if(b != null && b.NetDirty)
                        {
                            JNetServer server = JNet.GetServer();
                            NetOutgoingMessage msg = server.CreateMessage();
                            msg.Write((byte)JDataType.SERIALIZED);
                            msg.Write(obj.NetID);
                            msg.Write(b.BehaviourID);
                            b.NetSerialize(msg, false);

                            server.SendToAllExcept(server.LocalClientConnection, msg, b.SerializationDeliveryMethod, 1);

                            b.NetDirty = false;
                        }
                    }
                }
            }
        }

        public void Register(NetObject obj, ushort overrideNetID = 0)
        {
            if (obj == null)
            {
                JNet.Error("Null object to register.");
                return;
            }

            if (obj.HasNetID)
            {
                JNet.Error("Object already registered!");
                return;
            }

            ushort id = 0;
            if(overrideNetID == 0)
            {
                int maxID = TrackedObjects.Length;

                for (int i = 0; i < maxID + 1; i++)
                {
                    id = CurrentTopID;

                    CurrentTopID++;
                    if (CurrentTopID >= maxID)
                    {
                        CurrentTopID = 1;
                    }

                    if (TrackedObjects[id] == null)
                    {
                        break;
                    }
                    else
                    {
                        id = 0;
                    }
                }
            }
            else
            {
                id = overrideNetID;
            }            

            if(id == 0)
            {
                JNet.Error("Cannot register new net object to be tracked, out of ID's! Too many objects are already tracked! Max object count: " + MAX_TRACKED_OBJECTS);
                return;
            }

            obj.NetID = id;
            obj.UpdateBehaviourList();

            if(TrackedObjects[id] != null)
            {
                JNet.Error("Overriding object in register, possibly from client. Override netID: " + overrideNetID);
            }

            TrackedObjects[id] = obj;
            ObjectCount++;

            ActiveObjects.Add(obj);
        }

        public void Unregister(NetObject obj)
        {
            if (obj == null)
                return;

            if (!obj.HasNetID)
                return;

            ushort id = obj.NetID;

            NetObject current = TrackedObjects[id];
            if(current != obj)
            {
                JNet.Error("Current is not the object that is about to be unregistered.");
                return;
            }

            TrackedObjects[id] = null;
            obj.NetID = 0;
            ObjectCount--;

            ActiveObjects.Remove(obj);
        }

        public NetObject GetObject(ushort objectID)
        {
            return TrackedObjects[objectID];
        }

        public NetBehaviour GetBehaviour(ushort objectID, byte behaviourID)
        {
            var obj = TrackedObjects[objectID];
            if (obj == null)
                return null;

            return obj.GetBehaviour(behaviourID);
        }
    }
}
