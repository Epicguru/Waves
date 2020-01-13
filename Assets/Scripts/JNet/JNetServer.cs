using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JNetworking.Internal;
using Lidgren.Network;

namespace JNetworking
{
    public partial class JNetServer : JNetPeer
    {
        private const string DEFAULT_NAME = "";

        /// <summary>
        /// The name of the server.
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// The action that is called when a connection is requested from a client.
        /// If this is left as null, then all incoming connections are accepted.
        /// First tupple part (bool) is true to accept the connection, false to deny it.
        /// Second tupple part (string) is a reason for rejection (only used when denying the connection).
        /// </summary>
        public ConnectionApproval UponConnectionRequest;
        /// <summary>
        /// Called when a connection is successfully made and the client is accepted. Allways called after <see cref="UponConnectionRequest"/>.
        /// </summary>
        public Action<RemoteClient> UponConnection;
        /// <summary>
        /// Called when a client connection is ended.
        /// Parameters are the client that disconnected and the reason for disconnection.
        /// </summary>
        public Action<RemoteClient, string> UponDisconnection;

        public List<RemoteClient> Clients { get; protected set; }
        public int ClientCount { get { return Clients.Count; } }
        public NetConnection LocalClientConnection { get; private set; }

        private Dictionary<long, RemoteClient> dictClients;

        public JNetServer(string name, NetPeerConfiguration config) : base(config, true)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                JNet.Error(string.Format("Null name supplied to server, default name '{0}' will be used.", DEFAULT_NAME));
                name = DEFAULT_NAME;
            }

            Name = name.Trim();
            Clients = new List<RemoteClient>();
            dictClients = new Dictionary<long, RemoteClient>();

            SetProcessor(JDataType.PING, this.ProcessPing);
            SetProcessor(JDataType.RMC, this.ProcessRMC);
        }

        protected override void ProcessConnectionRequest(NetIncomingMessage msg)
        {
            bool isLocalClient = msg.ReadBoolean();
            double key = msg.ReadDouble();

            if (isLocalClient)
            {
                if (key == HostKey)
                {
                    // Nice!
                    Log("Detected connection from local host.");
                    if (LocalClientConnection == null)
                    {
                        LocalClientConnection = msg.SenderConnection;
                    }
                    else
                    {
                        LogError("Client connected claiming to be local (host) client, but local client connection is already established.");
                    }
                }
                else
                {
                    LogError("Client {0} claimed to be the connecting host, but had the incorrect key {1} vs real {2}", msg.SenderConnection, key, HostKey);
                }                
            }

            RemoteClient client = new RemoteClient(msg.SenderConnection);
            if (UponConnectionRequest != null)
            {
                (bool accept, string denyReason) = UponConnectionRequest.Invoke(msg, client);


                if (accept)
                {
                    msg.SenderConnection.Approve();
                }
                else
                {
                    msg.SenderConnection.Deny(denyReason);
                    client.Dispose();
                    return;
                }
            }
            else
            {
                msg.SenderConnection.Approve();
            }

            this.Clients.Add(client);
            this.dictClients.Add(msg.SenderConnection.RemoteUniqueIdentifier, client);
        }

        public void SendToAll(NetOutgoingMessage msg, NetDeliveryMethod method, int sequence)
        {
            if (msg == null)
                return;

            if (base.Connections.Count == 0)
            {
                return;
            }

            base.SendMessage(msg, base.Connections, method, sequence);
        }

        private List<NetConnection> tempConns = new List<NetConnection>();
        public void SendToAllExcept(NetConnection except, NetOutgoingMessage msg, NetDeliveryMethod method, int sequence)
        {
            if (msg == null)
                return;

            if(except == null)
            {
                SendToAll(msg, method, sequence);
                return;
            }

            tempConns.Clear();
            bool found = false;
            foreach (var conn in Connections)
            {
                if (conn == except)
                {
                    found = true;
                    continue;
                }
                tempConns.Add(conn);
            }

            if (!found)
            {
                //JNet.Error("Excluded connection was not found in the current connections list. Sending to all clients instead.");
                SendToAll(msg, method, sequence);
                return;
            }

            if(tempConns.Count > 0)
                base.SendMessage(msg, tempConns, method, sequence);

            tempConns.Clear();
        }

        public void SendMessage(NetOutgoingMessage msg, RemoteClient c, NetDeliveryMethod method, int sequence)
        {
            if (msg == null)
            {
                LogError("Message to send is null.");
                return;
            }

            if (c == null)
            {
                LogError("Client to send to is null.");
                return;
            }

            if (!c.IsActive)
            {
                LogError("Client to send to is not connected.");
                return;
            }

            base.SendMessage(msg, c.Connection, method, sequence);
        }

        protected override void ProcessStatusChanged(NetIncomingMessage msg, NetConnectionStatus status)
        {
            base.ProcessStatusChanged(msg, status);

            if(status == NetConnectionStatus.Connected)
            {
                Log("New client connected from {0}", msg.SenderConnection.RemoteEndPoint);

                // Send currently tracked objects, if not host.
                if(msg.SenderConnection != LocalClientConnection)
                    SendAllObjects(msg.SenderConnection);

                RemoteClient c = GetClient(msg);

                UponConnection?.Invoke(c);
            }

            if(status == NetConnectionStatus.Disconnected)
            {
                string reason = msg.ReadString();
                Log("Client has disconnected: {0}", reason);

                if(msg.SenderConnection == LocalClientConnection)
                {
                    Log("(Was local client)");
                    LocalClientConnection = null;
                }

                var client = GetClient(msg);
                if(client != null)
                {                   
                    // Remove net object from this client's ownership.
                    int loop = 0;
                    int max = client.OwnedObjectsList.Count;
                    while(client.OwnedObjectsList.Count > 0)
                    {
                        JNet.SetOwner(client.OwnedObjectsList[0], null);

                        loop++;
                        if (loop > max)
                        {
                            JNet.Error($"Infinite loop?! Looped {loop}, expected {max}.");
                            break;
                        }
                    }

                    // Remove client.
                    this.Clients.Remove(client);
                    this.dictClients.Remove(client.ConnectionID);

                    UponDisconnection?.Invoke(client, reason);
                }
                else
                {
                    LogError("Client with Id {0} from {1} did not have a valid RemoteClient to remove.", msg.SenderConnection.RemoteUniqueIdentifier, msg.SenderConnection.RemoteEndPoint);
                }
            }
        }

        public RemoteClient GetClient(NetConnection connection, bool hideError = false)
        {
            if (connection == null)
                return null;

            return GetClient(connection.RemoteUniqueIdentifier, hideError);
        }

        public RemoteClient GetClient(NetIncomingMessage msg, bool hideError = false)
        {
            if (msg == null)
                return null;

            return GetClient(msg.SenderConnection.RemoteUniqueIdentifier, hideError);
        }

        public RemoteClient GetClient(long connectionID, bool hideError = false)
        {
            if (dictClients.ContainsKey(connectionID))
            {
                return dictClients[connectionID];
            }
            else if(!hideError)
            {
                LogError("RemoteClient for connectionId {0} is not found. Has the client disconnected?", connectionID);
            }
            return null;
        }

        private void SendAllObjects(NetConnection n)
        {
            if (n == null)
                return;

            var msg = this.CreateMessage(JDataType.SPAWN_ALL);
            WriteAllObjects(msg);

            SendMessage(msg, n, NetDeliveryMethod.ReliableOrdered, 0);
        }

        internal void WriteAllObjects(NetOutgoingMessage msg)
        {
            int c = JNet.TrackedObjectCount;
            msg.Write(c);

            foreach (var obj in JNet.Tracker.ActiveObjects)
            {
                msg.Write(obj.PrefabID);
                msg.Write(obj.NetID);
                obj.Serialize(msg, true);
            }
        }
    }

    public delegate (bool accept, string denyReason) ConnectionApproval(NetIncomingMessage msg, RemoteClient client);
}
