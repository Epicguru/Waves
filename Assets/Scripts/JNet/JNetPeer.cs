
using JNetworking.Internal;
using Lidgren.Network;
using System;
using System.Collections.Generic;

namespace JNetworking
{
    public abstract class JNetPeer : NetPeer
    {
        internal bool IsServer { get; private set; }

        /// <summary>
        /// The action that is called when a custom data message is recived.
        /// </summary>
        public ProcessCustomData UponCustomData;

        /// <summary>
        /// The action called when an unhandled exception is thrown when processing an incoming data message.
        /// If this action is null (has no recievers), then the exception is caught a rethrown.
        /// If this action is not null (has at least one reciever), then the exception is caught and passed into this delegate
        /// for custom processing.
        /// </summary>
        public Action<Exception, NetIncomingMessage> UponMessageException;

        internal DataProcessor[] InternalDataProcessors = new DataProcessor[JNet.RESERVED_ID_COUNT];

        public JNetPeer(NetPeerConfiguration config, bool isServer) : base(config)
        {
            this.IsServer = isServer;
        }

        internal virtual void Update()
        {
            NetIncomingMessage msg;

            while ((msg = base.ReadMessage()) != null)
            {
                try
                {
                    // Proccess message...
                    ProcessRawMessage(msg);
                }
                catch (Exception e)
                {
                    if(UponMessageException == null)
                    {
                        throw e;
                    }
                    else
                    {
                        UponMessageException.Invoke(e, msg);
                    }
                }
                finally
                {
                    base.Recycle(msg);
                }
            }
        }

        internal void InjectDataMessage(NetIncomingMessage msg)
        {
            ProcessData(msg);
        }

        internal virtual void ProcessRawMessage(NetIncomingMessage msg)
        {
            // Read message type.
            NetIncomingMessageType type = msg.MessageType;
            try
            {
                switch (type)
                {
                    case NetIncomingMessageType.Error:
                        bool worked = msg.ReadString(out string text);
                        LogError("Unexpected network message error: {0}", worked ? text : "no info given");
                        break;

                    case NetIncomingMessageType.StatusChanged:
                        byte statusByte = msg.ReadByte();
                        NetConnectionStatus status = (NetConnectionStatus)statusByte;
                        ProcessStatusChanged(msg, status);
                        break;

                    case NetIncomingMessageType.UnconnectedData:
                        ProcessUnconnectedData(msg);
                        break;

                    case NetIncomingMessageType.ConnectionApproval:
                        ProcessConnectionRequest(msg);
                        break;

                    case NetIncomingMessageType.Data:
                        JNet.Playback.LogIncoming(msg);
                        ProcessData(msg);
                        break;

                    case NetIncomingMessageType.DiscoveryRequest:
                        throw new System.NotImplementedException();

                    case NetIncomingMessageType.DiscoveryResponse:
                        throw new NotImplementedException();

                    case NetIncomingMessageType.VerboseDebugMessage:
                        ProcessLogMessage(msg, type);
                        break;

                    case NetIncomingMessageType.DebugMessage:
                        ProcessLogMessage(msg, type);
                        break;

                    case NetIncomingMessageType.WarningMessage:
                        ProcessLogMessage(msg, type);
                        break;

                    case NetIncomingMessageType.ErrorMessage:
                        ProcessLogMessage(msg, type);
                        break;

                    case NetIncomingMessageType.NatIntroductionSuccess:
                        throw new NotImplementedException();

                    default:
                        Log("Unhandled incoming message type: {0}", type);
                        break;
                }
            }
            catch (Exception e)
            {
                JNetMsgReadException ex = new JNetMsgReadException("Unhandled expception while handling message of type: " + type, e);
                ex.NetMessage = msg;

                throw ex;
            }
        }

        internal void SetProcessor(JDataType type, DataProcessor p)
        {
            this.InternalDataProcessors[(byte)type] = p;
        }

        public NetOutgoingMessage CreateMessage(byte type)
        {
            NetOutgoingMessage msg = base.CreateMessage();
            msg.Write(type);

            return msg;
        }

        public NetOutgoingMessage CreateMessage(byte type, int initialCapacity)
        {
            NetOutgoingMessage msg = base.CreateMessage(initialCapacity);
            msg.Write(type);

            return msg;
        }

        internal NetOutgoingMessage CreateMessage(JDataType type)
        {
            NetOutgoingMessage msg = base.CreateMessage();
            msg.Write((byte)type);

            return msg;
        }

        internal NetOutgoingMessage CreateMessage(JDataType type, int capacity)
        {
            NetOutgoingMessage msg = base.CreateMessage(capacity);
            msg.Write((byte)type);

            return msg;
        }

        /// <summary>
        /// Called when a message containing connected data is recieived.
        /// </summary>
        protected virtual void ProcessData(NetIncomingMessage msg)
        {
            bool hasBasic = msg.ReadByte(out byte typeByte);
            if (!hasBasic)
            {
                LogError("Missing basic data type in recieved message. Corrupted?");
                return;
            }

            if (typeByte >= JNet.RESERVED_ID_COUNT)
            {
                // It's a custom data type.
                UponCustomData?.Invoke(typeByte, msg);
            }
            else
            {
                // It's an internal data type.
                var processor = InternalDataProcessors[typeByte];
                if(processor == null)
                {
                    LogError("No internal data processor for type {0}", (JDataType)typeByte);
                }
                else
                {
                    processor(msg);
                }
            }
        }

        /// <summary>
        /// Called when a message from an unconnected client is recieived. Treat with caution.
        /// </summary>
        protected virtual void ProcessUnconnectedData(NetIncomingMessage msg)
        {

        }

        /// <summary>
        /// Called (if enabled) when a client attempts to connect.
        /// </summary>
        /// <param name="msg"></param>
        protected virtual void ProcessConnectionRequest(NetIncomingMessage msg)
        {

        }

        /// <summary>
        /// Called when a connection status has changed. When a client, this is the connection to the server.
        /// When a server, this can be the status of any connected (or connecting) client.
        /// </summary>
        /// <param name="status">The updated status.</param>
        protected virtual void ProcessStatusChanged(NetIncomingMessage msg, NetConnectionStatus status)
        {
            Log("Status update: {0}", status);
        }

        /// <summary>
        /// Called when a debug, warning or error message comes in from Lidgren. By default logs the message or error
        /// to the standard JNet output.
        /// </summary>
        /// <param name="msg">The message that contains debug info.</param>
        /// <param name="type">The message type. Can be used to check if it is debug, warning or error.</param>
        protected virtual void ProcessLogMessage(NetIncomingMessage msg, NetIncomingMessageType type)
        {
            string text = msg.ReadString();
            bool isError = type == NetIncomingMessageType.ErrorMessage;

            if (isError)
            {
                LogError(text);
            }
            else
            {
                Log(type.ToString() + ": " + text);
            }
        }

        public void EnableMessageType(NetIncomingMessageType type)
        {
            base.Configuration.EnableMessageType(type);
        }

        public void DisableMessageType(NetIncomingMessageType type)
        {
            base.Configuration.DisableMessageType(type);
        }

        protected void Log(string s, params object[] args)
        {
            JNet.Log((IsServer ? "[S] " : "[C] ") + string.Format(s, args));
        }

        protected void LogError(string s, params object[] args)
        {
            JNet.Error((IsServer ? "[S] " : "[C] ") + string.Format(s, args));
        }
    }

    internal delegate void DataProcessor(NetIncomingMessage msg);
    public delegate void ProcessCustomData(byte type, NetIncomingMessage msg);
}
