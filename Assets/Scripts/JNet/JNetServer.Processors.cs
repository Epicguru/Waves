
using JNetworking.Internal;
using Lidgren.Network;
using System;

namespace JNetworking
{
	public partial class JNetServer
    {
		private void ProcessPing(NetIncomingMessage msg)
        {
            // Pong.
            var outmsg = CreateMessage(JDataType.PING);
            base.SendMessage(outmsg, msg.SenderConnection, NetDeliveryMethod.Unreliable);
        }

        private double HostKey;
        internal double GenNewHostKey()
        {
            HostKey = new Random().NextDouble() * 1000;
            return HostKey;
        }

        private void ProcessRMC(NetIncomingMessage msg)
        {
            ushort netID = msg.ReadUInt16();
            byte behaviourID = msg.ReadByte();
            byte methodID = msg.ReadByte();

            var b = JNet.Tracker.GetBehaviour(netID, behaviourID);
            if (b == null)
            {
                LogError("Didn't find behaviour's method at net address {0}.{1}.{2} to invoke an Cmd.", netID, behaviourID, methodID);
            }
            else
            {
                b.HandleRMCMessage(msg, methodID);
            }
        }
    }
}
