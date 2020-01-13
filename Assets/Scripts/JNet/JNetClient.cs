
using JNetworking.Internal;
using Lidgren.Network;
using System;

namespace JNetworking
{
    public partial class JNetClient : JNetPeer
    {
        public NetConnection ServerConnection
        {
            get
            {
                NetConnection val = null;

                if (base.Connections.Count > 0)
                    val = Connections[0];

                return val;
            }
        }
        public NetConnectionStatus ConnectionStatus
        {
            get
            {
                var conn = ServerConnection;
                if(conn == null)
                {
                    return NetConnectionStatus.Disconnected;
                }
                else
                {
                    return conn.Status;
                }
            }
        }

        public Action<NetConnectionStatus> UponConnectionStatusUpdate;
        public Action UponConnect;
        public Action<string> UponDisconnect;
        public Action UponWorldRecieved;

        internal bool IsHost = false;

        public JNetClient(NetPeerConfiguration config) : base(config, false)
        {
            SetProcessor(JDataType.PING, ProcessPing);
            SetProcessor(JDataType.SPAWN, ProcessSpawn);
            SetProcessor(JDataType.DESPAWN, ProcessDespawn);
            SetProcessor(JDataType.SERIALIZED, ProcessSerialize);
            SetProcessor(JDataType.RMC, this.ProcessRMC);
            SetProcessor(JDataType.SPAWN_ALL, this.ProcessSpawnAll);
            SetProcessor(JDataType.SET_OWNER, this.ProcessSetOwner);
        }

        protected override void ProcessData(NetIncomingMessage msg)
        {
            JNet.CurrentMessagesIn++;
            JNet.CurrentBytesIn += msg.LengthBytes;

            base.ProcessData(msg);
        }

        public NetSendResult Send(NetOutgoingMessage msg, NetDeliveryMethod method, int sequenceChannel)
        {
            if (msg == null)
                return NetSendResult.Dropped;

            if (ServerConnection == null)
            {
                base.LogError("Server connection is not established, cannot send message!");
                return NetSendResult.FailedNotConnected;
            }

            return base.SendMessage(msg, ServerConnection, method, sequenceChannel);
        }

        public void Disconnect(string bye)
        {
            if(ServerConnection == null)
            {
                base.LogError("Server connection is not established, cannot disconnect!");
                return;
            }

            ServerConnection.Disconnect(bye);
        }

        protected override void ProcessStatusChanged(NetIncomingMessage msg, NetConnectionStatus status)
        {
            base.ProcessStatusChanged(msg, status);

            bool hasText = msg.ReadString(out string text);

            UponConnectionStatusUpdate?.Invoke(status);

            if(status == NetConnectionStatus.Connected)
            {
                UponConnect?.Invoke();
            }
            if(status == NetConnectionStatus.Disconnected)
            {
                UponDisconnect?.Invoke(hasText ? text : null);
            }
        }

        public void SendPing()
        {
            var msg = CreateMessage(JDataType.PING);

            this.Send(msg, NetDeliveryMethod.Unreliable, 0);
        }
    }
}