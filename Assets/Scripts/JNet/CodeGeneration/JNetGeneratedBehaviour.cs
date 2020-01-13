
using JNetworking.Utils;
using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace JNetworking.CodeGeneration
{
    public abstract class JNetGeneratedBehaviour
    {
        public int SyncVarCount { get; protected set; }
        public int RemoteMethodCount { get { return RemoteMethods.Count; } }
        public bool NetDirty { get; protected set; }
        public Dictionary<string, (MethodInfo method, bool isCmd)> RemoteMethods = new Dictionary<string, (MethodInfo, bool)>();

        public DualMap<string, byte> RemoteMethodMap = new DualMap<string, byte>();

        public abstract void Update(NetBehaviour target);
        public abstract void Serialize(NetBehaviour target, NetOutgoingMessage msg, bool first);
        public abstract void Deserialize(NetBehaviour target, NetIncomingMessage msg, bool first);

        public MethodInfo GetCmd(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            name = name.Trim().ToLower();
            if (RemoteMethods.ContainsKey(name))
            {
                (var m, bool isCmd) = RemoteMethods[name];
                if (isCmd)
                    return m;
                else
                    return null;
            }
            return null;
        }

        public MethodInfo GetRpc(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            name = name.Trim().ToLower();
            if (RemoteMethods.ContainsKey(name))
            {
                (var m, bool isCmd) = RemoteMethods[name];
                if (!isCmd)
                    return m;
                else
                    return null;
            }
            return null;
        }

        public byte GetMethodID(string name)
        {
            return RemoteMethodMap[name];
        }

        public string GetMethodName(byte id)
        {
            return RemoteMethodMap[id];
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class GeneratedTargetAttribute : Attribute
    {
        private readonly Type targetType;

        public GeneratedTargetAttribute(Type t)
        {
            this.targetType = t;
        }

        public Type GetTargetType()
        {
            return targetType;
        }
    }
}
