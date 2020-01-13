
using Lidgren.Network;
using System;

namespace JNetworking.Internal
{
    public class JNetMsgWriteException : JNetException
    {
        public NetOutgoingMessage NetMessage { get; internal set; }

        public JNetMsgWriteException(string message) : base(message)
        {
        }

        public JNetMsgWriteException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}