
using Lidgren.Network;
using Newtonsoft.Json;
using UnityEngine;

namespace JNetworking
{
    public static class MessageUtils
    {
        public static void WriteObject(this NetOutgoingMessage msg, object o)
        {
            string s = JsonConvert.SerializeObject(o);
            msg.Write(s);
        }

        public static T ReadObject<T>(this NetIncomingMessage msg)
        {
            string json = msg.ReadString();
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static void Write(this NetOutgoingMessage msg, Vector2 vector)
        {
            msg.Write(vector.x);
            msg.Write(vector.y);
        }

        public static Vector2 ReadVector2(this NetIncomingMessage msg)
        {
            float x = msg.ReadFloat();
            float y = msg.ReadFloat();

            return new Vector2(x, y);
        }

        public static void Write(this NetOutgoingMessage msg, Vector3 vector)
        {
            msg.Write(vector.x);
            msg.Write(vector.y);
            msg.Write(vector.z);
        }

        public static Vector3 ReadVector3(this NetIncomingMessage msg)
        {
            float x = msg.ReadFloat();
            float y = msg.ReadFloat();
            float z = msg.ReadFloat();

            return new Vector3(x, y, z);
        }

        public static void Write(this NetOutgoingMessage msg, Vector4 vector)
        {
            msg.Write(vector.x);
            msg.Write(vector.y);
            msg.Write(vector.z);
            msg.Write(vector.w);
        }

        public static Vector4 ReadVector4(this NetIncomingMessage msg)
        {
            float x = msg.ReadFloat();
            float y = msg.ReadFloat();
            float z = msg.ReadFloat();
            float w = msg.ReadFloat();

            return new Vector4(x, y, z, w);
        }

        /// <summary>
        /// Note: Compresses colour to a Color32, allowing for a 32 bit colour depth.
        /// The written colour may not be exactly the same as the read colour.
        /// </summary>
        public static void Write(this NetOutgoingMessage msg, Color colour)
        {
            Color32 c = colour;
            msg.Write(c.r);
            msg.Write(c.g);
            msg.Write(c.b);
            msg.Write(c.a);
        }

        /// <summary>
        /// Note: Colour sent was compressed to a Color32, allowing for a 32 colour depth.
        /// The written colour may not be exactly the same as the read colour.
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static Color ReadColor(this NetIncomingMessage msg)
        {
            byte r = msg.ReadByte();
            byte g = msg.ReadByte();
            byte b = msg.ReadByte();
            byte a = msg.ReadByte();

            return new Color32(r, g, b, a);
        }

        /// <summary>
        /// IMPORTANT: This actually writes a double, so data is lost. This has been implemented
        /// for convenince, but is not ideal so send highly accurate decimals.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="value"></param>
        public static void Write(this NetOutgoingMessage msg, decimal value)
        {
            double d = (double)value;
            msg.Write(d);
        }

        /// <summary>
        /// IMPORTANT: This actually reads a double, so data is lost. This has been implemented
        /// for convenince, but is not ideal so send highly accurate decimals.
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static decimal ReadDecimal(this NetIncomingMessage msg)
        {
            double d = msg.ReadDouble();
            return (decimal)d;
        }

        public static void WriteNetObject(this NetOutgoingMessage msg, NetObject obj)
        {
            if (obj == null)
                msg.Write((ushort)0);
            else
                msg.Write(obj.NetID);
        }

        public static NetObject ReadNetObject(this NetIncomingMessage msg)
        {
            return JNet.GetObject(msg.ReadUInt16());
        }
    }
}
