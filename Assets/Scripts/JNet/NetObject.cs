
using Lidgren.Network;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace JNetworking
{
    /// <summary>
    /// A NetObject is a component attached to a GameObject that allows it to be tracked and instantiated over the
    /// network.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetObject : MonoBehaviour, IParentNode
    {
        private static List<IParentNode> nodes = new List<IParentNode>();
        private static Queue<Transform> open = new Queue<Transform>();

        public long OwnerID { get; internal set; }
        public bool HasNetID { get { return NetID != 0; } }
        /// <summary>
        /// Returns true if the prefab ID is valid.
        /// </summary>
        public bool HasPrefabID { get { return PrefabID != 0; } }
        /// <summary>
        /// Returns true if this is a prefab that has been registered using <see cref="JNet.RegisterPrefab(NetObject)"/>.
        /// </summary>
        public bool IsRegisteredPrefab
        {
            get
            {
                return _isPrefab;
            }
            internal set
            {
                _isPrefab = value;
            }
        }
        public bool IsSceneLoaded { get; internal set; }

        public ushort NetID
        {
            get
            {
                return _netID;
            }
            internal set
            {
                _netID = value;
            }
        }
        public ushort PrefabID
        {
            get
            {
                return _prefabID;
            }
            internal set
            {
                _prefabID = value;
            }
        }
        public NetBehaviour[] NetBehaviours { get; internal set; }

        [NonSerialized]
        private ushort _netID;
        [SerializeField]
        [HideInInspector]
        private ushort _prefabID;
        [NonSerialized]
        private bool _isPrefab;

        [NonSerialized]
        [HideInInspector]
        public bool _debugShowComps;

        /// <summary>
        /// True if the local client has ownership of this object. False if the local client is null or doesn't have ownership.
        /// Call <see cref="JNet.SetOwner(NetObject, NetConnection)"/> when on the server to assign ownership to a particular client.
        /// </summary>
        public bool HasLocalOwnership
        {
            get
            {
                if (!JNet.IsClient)
                    return false;

                return JNet.GetClient().UniqueIdentifier == OwnerID;
            }
        }

        /// <summary>
        /// Returns true when on server or on a client with local ownership. See <see cref="HasLocalOwnership"/>.
        /// Having this authority allows for CMDs to be sent.
        /// </summary>
        public bool HasAuthority
        {
            get
            {
                return JNet.IsServer || HasLocalOwnership;
            }
        }

        public IParentNode[] ParentNodes { get; private set; }

        #region IParentNode implementation
        public byte GetNodeID() { return 1; }
        public void SetNodeID(byte id) { }
        public NetObject GetNetObject() { return this; }
        #endregion

        public IParentNode GetParentNode(int id)
        {
            if (id > 0 && id < ParentNodes.Length)
                return ParentNodes[id];
            else
                return null;
        }

        public NetBehaviour GetBehaviour(byte id)
        {
            if (id >= NetBehaviours.Length)
                return null;

            return NetBehaviours[id];
        }

        internal void UpdateBehaviourList()
        {
            var comps = this.GetComponents<NetBehaviour>();
            if(comps.Length <= 256)
            {
                NetBehaviours = comps;
            }
            else
            {
                NetBehaviours = new NetBehaviour[256];
                System.Array.Copy(comps, NetBehaviours, 256);
                JNet.Error(string.Format("NetObject {0} has {1} NetBehaviour components, but a max of 256 are supported. Some of them will not function properly, and errors will arise.", this.ToString(), comps.Length));
            }

            for (int i = 0; i < NetBehaviours.Length; i++)
            {
                NetBehaviours[i].BehaviourID = (byte)i;
                NetBehaviours[i].NetAwake();
            }

            RefreshParentNodes();
        }

        public void RefreshParentNodes()
        {
            nodes.Clear();
            open.Clear();

            nodes.Add(null); // 0
            nodes.Add(this); // 1

            open.Enqueue(this.transform);

            while(open.Count > 0)
            {
                var current = open.Dequeue();
                var obj = current.GetComponent<NetObject>();
                if (obj != null && obj != this)
                    continue; // Ignore this one, next!

                var node = current.GetComponent<IParentNode>();
                if (node != null)
                    nodes.Add(node);

                for (int i = 0; i < current.childCount; i++)
                {
                    var c = current.GetChild(i);
                    open.Enqueue(c);
                }
            }

            this.ParentNodes = nodes.ToArray();
            nodes.Clear();
            open.Clear();
        }

        protected virtual void Update()
        {
            if(NetBehaviours == null)
            {
                return;
            }

            foreach (var b in NetBehaviours)
            {
                if (b == null)
                    continue;

                b.NetUpdate();
            }
        }

        public virtual void Serialize(NetOutgoingMessage msg, bool isForFirst)
        {
            msg.Write(OwnerID);
            foreach (var b in NetBehaviours)
            {
                if (b == null)
                    continue;

                b.NetSerialize(msg, isForFirst);
            }
        }

        public virtual void Deserialize(NetIncomingMessage msg, bool first)
        {
            OwnerID = msg.ReadInt64();
            foreach (var b in NetBehaviours)
            {
                if (b == null)
                    continue;

                b.NetDeserialize(msg, first);
            }
        }

        private void OnDestroy()
        {
            JNet.Despawn(this);
        }

        public override string ToString()
        {
            return string.Format("{0} : {1} with {2} behaviours", name, NetID, NetBehaviours?.Length ?? 0);
        }

        public Transform GetTransform()
        {
            return this.transform;
        }
    }
}
