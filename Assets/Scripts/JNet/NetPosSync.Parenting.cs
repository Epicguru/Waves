
using UnityEngine;

namespace JNetworking
{
	public partial class NetPosSync
    {
        [Header("Parent Sync")]
		[SyncVar]
        public ushort ParentNetID;
		[SyncVar(Hook = "NewNodeID")]
        public byte ParentNodeID;

        private bool updateParent = false;

		public void SetParent(IParentNode parentNode)
        {
            if (!JNet.IsServer)
            {
                Debug.LogError("Not on server, should not call SetParent.");
                return;
            }

			if(parentNode == null)
            {
                ParentNodeID = 0;
                ParentNetID = 0;
                transform.SetParent(null, true);
            }
            else
            {
                ParentNodeID = parentNode.GetNodeID();
                ParentNetID = parentNode.GetNetObject()?.NetID ?? 0;
                transform.SetParent(parentNode.GetTransform(), true);
            }
        }

        public void NewNodeID(byte id)
        {
            this.ParentNodeID = id;
            updateParent = true;
        }

		private void LateUpdate()
        {
            if (!updateParent)
                return;

            updateParent = false;

            if(ParentNetID != 0 && ParentNodeID != 0)
            {
                // Try to find the object.
                NetObject obj = JNet.GetObject(ParentNetID);
                if (obj == null)
                {
                    JNet.Error($"Failed to find net object of id {ParentNetID} to sync the transform of this object.");
                    return;
                }

                var node = obj.GetParentNode(ParentNodeID);
                if (node == null)
                {
                    JNet.Error($"Failed to find net object of id {ParentNetID} parent node ({ParentNodeID}) to sync the transform of this object.");
                    return;
                }

                this.transform.SetParent(node.GetTransform(), true);
            }
            else
            {
                this.transform.SetParent(null, true);
            }            
        }
    }
}
