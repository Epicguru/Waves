
using Lidgren.Network;
using UnityEngine;

namespace JNetworking
{
    public partial class JNetClient
    {
        private void ProcessPing(NetIncomingMessage msg)
        {
            // Cool.
            // TODO calculate ping here.
        }

        private void ProcessSpawn(NetIncomingMessage msg)
        {
            // Do not process if the server is active.
            if (JNet.IsServer)
                return;

            // Prefab ID.
            ushort prefabID = msg.ReadUInt16();

            // Net ID.
            ushort netID = msg.ReadUInt16();

            // Get the prefab for that ID...
            NetObject prefab = JNet.GetPrefab(prefabID);
            if(prefab == null)
            {
                JNet.Error("Failed to find prefab for ID " + prefabID + ", object not spawned on client. Desync incoming!");
                return;
            }

            if(netID == 0)
            {
                JNet.Error("Client got spawn message with instance net ID of zero, server error or corruption? Object not spawned, prefab id was " + prefabID + " (" + prefab + ").");
                return;
            }

            // Instantiate the game object.
            NetObject no = GameObject.Instantiate(prefab);

            // We need to register the object to the system.
            JNet.Tracker.Register(no, netID);

            // Read all the behaviours.
            no.Deserialize(msg, true);
        }

        private void ProcessDespawn(NetIncomingMessage msg)
        {
            // Do not process if the server is active.
            if (JNet.IsServer)
                return;

            // Net ID.
            ushort netID = msg.ReadUInt16();

            if(netID == 0)
            {
                JNet.Error("Client was instructed to despawn object with netID 0, server error or corruption?");
                return;
            }

            NetObject obj = JNet.Tracker.TrackedObjects[netID];

            if(obj == null)
            {
                // Hmmm...
                JNet.Error(string.Format("Client was instructed to depsnaw object of NetID {0}, but that object was not found. Already destroyed?", netID));
                return;
            }

            // Destroy the object and unregister it.
            JNet.Tracker.Unregister(obj);

            // Destroy.
            GameObject.Destroy(obj.gameObject);
        }

        private void ProcessSerialize(NetIncomingMessage msg)
        {
            // Do not process when server is active.
            if (JNet.IsServer)
                return;

            // Object ID.
            ushort objID = msg.ReadUInt16();

            // Behaviour ID.
            byte behaviourID = msg.ReadByte();

            if(JNet.Tracker != null)
            {
                var b = JNet.Tracker.GetBehaviour(objID, behaviourID);

                if(b != null)
                {
                    b.NetDeserialize(msg, false);
                }
            }
        }

        private void ProcessRMC(NetIncomingMessage msg)
        {
            ushort netID = msg.ReadUInt16();
            byte behaviourID = msg.ReadByte();
            byte methodID = msg.ReadByte();

            var b = JNet.Tracker.GetBehaviour(netID, behaviourID);
            if(b == null)
            {
                LogError("Didn't find behaviour's method at net address {0}.{1}.{2} to invoke an Rpc.", netID, behaviourID, methodID);
            }
            else
            {
                b.HandleRMCMessage(msg, methodID);
            }
        }

        private void ProcessSpawnAll(NetIncomingMessage msg)
        {
            int objectCount = msg.ReadInt32();
            int expectedSceneObjects = JNet.EnableSceneObjectNetworking ? JNet.TrackedObjectCount : 0;

            JNet.Log($"Recieved {objectCount} objects from the server.");

            for (int i = 0; i < objectCount; i++)
            {
                ushort prefabID = msg.ReadUInt16();
                ushort netID = msg.ReadUInt16();

                // Get the prefab for that ID...
                NetObject prefab = JNet.GetPrefab(prefabID);
                if (prefab == null && netID > expectedSceneObjects)
                {
                    JNet.Error("Failed to find prefab for ID " + prefabID + ", object not spawned on client. Desync incoming!");
                    return;
                }

                if (netID == 0)
                {
                    JNet.Error("Client got spawn message with instance net ID of zero, server error or corruption? Object not spawned, prefab id was " + prefabID + " (" + prefab + ").");
                    return;
                }

                // Instantiate the game object, if not scene object.
                NetObject no;
                if (prefab != null)
                    no = GameObject.Instantiate(prefab);
                else
                    no = JNet.GetObject(netID);

                // We need to register the object to the system, if not scene object.
                if(prefab != null)
                    JNet.Tracker.Register(no, netID);

                // Read all the behaviours.
                no.Deserialize(msg, true);
            }

            UponWorldRecieved?.Invoke();
        }

        private void ProcessSetOwner(NetIncomingMessage msg)
        {
            ushort netID = msg.ReadUInt16();
            long ownerID = msg.ReadInt64();

            var obj = JNet.GetObject(netID);
            if(obj != null)
            {
                obj.OwnerID = ownerID;
            }
        }
    }
}
