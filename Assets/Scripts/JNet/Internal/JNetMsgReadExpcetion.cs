
using Lidgren.Network;
using System;

namespace JNetworking.Internal
{
    public class JNetMsgReadException : JNetException
    {
        public NetIncomingMessage NetMessage { get; internal set; }

        public JNetMsgReadException(string message) : base(message)
        {
        }

        public JNetMsgReadException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}