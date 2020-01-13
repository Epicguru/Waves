
using JNetworking.CodeGeneration;
using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace JNetworking
{
    /// <summary>
    /// The base class for all script components that want to use high level networking features such as sync vars.
    /// </summary>
    [RequireComponent(typeof(NetObject))]
    public abstract class NetBehaviour : MonoBehaviour
    {
        private static Dictionary<Type, ReadWrite> Parsers = new Dictionary<Type, ReadWrite>();

        private static void AddAllParsers()
        {
            AddParser(
                (m) => m.ReadBoolean(),
                (m, o) => m.Write((bool)o));
            AddParser(
                (m) => m.ReadColor(),
                (m, o) => m.Write((Color)o));
            AddParser(
                (m) => m.ReadString(),
                (m, o) => m.Write((string)o));
            AddParser(
                (m) => m.ReadByte(),
                (m, o) => m.Write((byte)o));
            AddParser(
                (m) => m.ReadSByte(),
                (m, o) => m.Write((sbyte)o));
            AddParser(
                (m) => m.ReadInt16(),
                (m, o) => m.Write((short)o));
            AddParser(
                (m) => m.ReadUInt16(),
                (m, o) => m.Write((ushort)o));
            AddParser(
                (m) => m.ReadInt32(),
                (m, o) => m.Write((int)o));
            AddParser(
                (m) => m.ReadUInt32(),
                (m, o) => m.Write((uint)o));
            AddParser(
                (m) => m.ReadInt64(),
                (m, o) => m.Write((long)o));
            AddParser(
                (m) => m.ReadUInt64(),
                (m, o) => m.Write((ulong)o));
            AddParser(
                (m) => m.ReadFloat(),
                (m, o) => m.Write((float)o));
            AddParser(
                (m) => m.ReadDouble(),
                (m, o) => m.Write((double)o));
            AddParser(
                (m) => m.ReadDecimal(),
                (m, o) => m.Write((decimal)o));
            AddParser(
                (m) => m.ReadVector2(),
                (m, o) => m.Write((Vector2)o));
            AddParser(
                (m) => m.ReadVector3(),
                (m, o) => m.Write((Vector3)o));
            AddParser(
                (m) => m.ReadVector4(),
                (m, o) => m.Write((Vector4)o));
            AddParser(
                (m) => JNet.GetObject(m.ReadUInt16()),
                (m, o) => m.Write((ushort)o));
        }

        private static void AddParser<T>(Read<T> r, Write w)
        {
            var type = typeof(T);
            ReadWrite rw = (i, mi, mo, o) =>
            {
                if (i)
                {
                    return r(mi);
                }
                else
                {
                    w(mo, o);
                    return null;
                }
            };

            Parsers.Add(type, rw);
        }

        private delegate T Read<T>(NetIncomingMessage msg);
        private delegate void Write(NetOutgoingMessage msg, object o);
        private delegate object ReadWrite(bool i, NetIncomingMessage mi, NetOutgoingMessage mo, object o);

        public NetObject NetObject
        {
            get
            {
                if (_no == null)
                    _no = GetComponent<NetObject>();
                return _no;
            }
        }
        private NetObject _no;

        /// <summary>
        /// Returns true when on server or on a client with local ownership. See <see cref="HasLocalOwnership"/>.
        /// Having this authority allows for CMDs to be sent.
        /// </summary>
        public bool HasAuthority
        {
            get
            {
                return NetObject.HasAuthority;
            }
        }

        /// <summary>
        /// True if the local client has ownership of this object. False if the local client is null or doesn't have ownership.
        /// Call <see cref="JNet.SetOwner(NetObject, NetConnection)"/> when on the server to assign ownership to a particular client.
        /// </summary>
        public bool HasLocalOwnership
        {
            get
            {
                return NetObject.HasLocalOwnership;
            }
        }

        public bool IsServer { get { return JNet.IsServer; } }
        public bool IsClient { get { return JNet.IsClient && (JNet.ClientConnectonStatus == NetConnectionStatus.Connected || JNet.Playback.IsInPlayback); } }
        public int LastSerializedFrame { get; private set; } = -1;
        public int LastDeserializedFrame { get; private set; } = -1;

        public NetDeliveryMethod RMCDeliveryMethod { get; protected set; } = NetDeliveryMethod.ReliableOrdered;
        public NetDeliveryMethod SerializationDeliveryMethod { get; protected set; } = NetDeliveryMethod.UnreliableSequenced;

        public bool IsRegistered { get { return NetObject.HasNetID; } }
        public byte BehaviourID { get; internal set; }
        public bool NetDirty
        {
            get
            {
                return localNetDirty || (CustomGeneratedBehaviour != null && CustomGeneratedBehaviour.NetDirty);
            }
            set
            {
                localNetDirty = value;
            }
        }
        public JNetGeneratedBehaviour CustomGeneratedBehaviour { get; protected set; }

        private bool localNetDirty;

        private static readonly Dictionary<Type, Type> classMap = new Dictionary<Type, Type>();

        internal static void UpdateClassMap()
        {
            classMap.Clear();

            var s = new System.Diagnostics.Stopwatch();
            s.Start();

            Type parent = typeof(JNetGeneratedBehaviour);
            uint typeCount = 0;
            uint assemblyCount = 0;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                string name = assembly.GetName().Name;

                if (name.StartsWith("Unity"))
                    continue;
                if (name.StartsWith("Mono"))
                    continue;
                if (name.StartsWith("System"))
                    continue;
                if (name.StartsWith("Microsoft"))
                    continue;
                if (name == "mscorlib")
                    continue;

                // JNet.Log(name);
                assemblyCount++;

                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    typeCount++;

                    if (!type.IsClass)
                        continue;
                    if (!type.IsSealed)
                        continue;

                    if (type.IsSubclassOf(parent))
                    {
                        var attribute = type.GetCustomAttribute<GeneratedTargetAttribute>();
                        if(attribute != null)
                        {
                            var targetType = attribute.GetTargetType();

                            classMap.Add(targetType, type);
                        }
                    }
                }
            }

            s.Stop();
            JNet.Log(string.Format("Rebuild class map in {0}ms, scanned {1} assemblies and {2} types.", s.Elapsed.TotalMilliseconds, assemblyCount, typeCount));
        }

        private static Type GetGeneratedType(Type self)
        {
            if (classMap.ContainsKey(self))
            {
                return classMap[self];
            }
            return null;
        }
        
        /// <summary>
        /// Invokes the named method on the behaviours of all connected clients.
        /// Therefore, this is only valid when called on the server.
        /// If the server is a host (client & server at the same time), then the method is also invoked 
        /// locally.
        /// </summary>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="args"></param>
        protected void InvokeRPC(string methodName, params object[] args)
        {
            if (!JNet.IsServer)
            {
                JNet.Error($"Cannot call Rpc {methodName} because server is not active!");
                return;
            }

            if (!NetObject.HasNetID)
            {
                JNet.Error($"Cannot call Rpc {methodName} because the object that {GetType().FullName} is on is not net spawned.");
                return;
            }

            MethodInfo method = GetRpc(methodName);
            if(method == null)
            {
                JNet.Error($"Could not find valid method with Rpc tag {methodName}. Check tags, name spelling and regenerate netcode!");
                return;
            }

            methodName = methodName.Trim().ToLower();
            byte methodID = CustomGeneratedBehaviour.GetMethodID(methodName);

            CheckDeliveryMethod();

            NetOutgoingMessage msg = JNet.GetServer().CreateMessage(Internal.JDataType.RMC);
            msg.Write(NetObject.NetID);
            msg.Write(this.BehaviourID);
            msg.Write(methodID);

            bool worked = ArgsToMsg(method, args, msg);
            if (!worked)
            {
                JNet.Error("Did not invoke Rpc due to error.");
                return;
            }

            var server = JNet.GetServer();
            server.SendToAllExcept(server.LocalClientConnection, msg, RMCDeliveryMethod, 24);

            // Call on local client, if we are a local client...
            if (JNet.IsClient)
            {
                method.Invoke(this, args);
            }
        }

        /// <summary>
        /// Invokes the named method on the corresponding behaviour on the server.
        /// When called on the server, the target method will be instantly invoked.
        /// </summary>
        /// <param name="methodName">The name of the method to call. The method should have the [Cmd] attribute.</param>
        /// <param name="args">The arguments to pass to the method.</param>
        protected void InvokeCMD(string methodName, params object[] args)
        {
            if (!NetObject.HasNetID)
            {
                JNet.Error($"Cannot call Cmd {methodName} because the object that {GetType().FullName} is on is not net spawned.");
                return;
            }            

            MethodInfo method = GetCmd(methodName);
            if (method == null)
            {
                JNet.Error($"Could not find valid method with Cmd tag {methodName}. Check tags, name spelling and regenerate netcode!");
                return;
            }

            if(!IsServer && !IsClient)
            {
                JNet.Error($"Not on client or server, can't invoke cmd {methodName}");
                return;
            }

            if (!HasAuthority)
            {
                JNet.Error($"There is currently no authority over this object {name}. Must call from on a client that has object ownership, or on the server. Current owner: {NetObject.OwnerID}");
                return;
            }

            methodName = methodName.Trim().ToLower();
            byte methodID = CustomGeneratedBehaviour.GetMethodID(methodName);

            if (!JNet.IsServer)
            {
                CheckDeliveryMethod();

                NetOutgoingMessage msg = JNet.GetClient().CreateMessage(Internal.JDataType.RMC);
                msg.Write(NetObject.NetID);
                msg.Write(this.BehaviourID);
                msg.Write(methodID);

                bool worked = ArgsToMsg(method, args, msg);
                if (!worked)
                {
                    JNet.Error("Did not invoke Cmd due to error.");
                    return;
                }

                var client = JNet.GetClient();
                client.Send(msg, RMCDeliveryMethod, 24);
            }
            else
            {
                // Invoke directly.
                var m = CustomGeneratedBehaviour.GetCmd(methodName);
                m.Invoke(this, args);
            }
            
        }

        private bool ArgsToMsg(MethodInfo f, object[] args, NetOutgoingMessage msg)
        {
            var parameters = f.GetParameters();

            if(parameters.Length != args.Length)
            {
                JNet.Error($"Expected {parameters.Length} parameters for remote invocation of {f.Name}, but only {args.Length} were supplied. Method will not be called.");
                return false;
            }

            if (Parsers.Count == 0)
                AddAllParsers();

            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];

                Type type = param.ParameterType;
                Type realType = args[i].GetType();

                if (!type.IsAssignableFrom(realType))
                {
                    JNet.Error($"Cannot automatically cast argument of type {realType.Name} to param {param.Name} of type {type.Name}.");
                    return false;
                }

                ReadWrite rw = Parsers[type];

                rw(false, null, msg, args[i]);
            }

            return true;
        }

        private void CheckDeliveryMethod()
        {
            if (RMCDeliveryMethod == NetDeliveryMethod.ReliableSequenced || RMCDeliveryMethod == NetDeliveryMethod.UnreliableSequenced)
            {
                NetDeliveryMethod replacement = RMCDeliveryMethod == NetDeliveryMethod.ReliableSequenced ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.Unreliable;
                JNet.Error($"Remote Delivery Method {RMCDeliveryMethod} is not allowed because it is sequenced. Using {replacement} instead.");
                RMCDeliveryMethod = replacement;
            }
        }

        private static List<object> MethodArgs = new List<object>();
        private object[] MsgToArgs(MethodInfo f, NetIncomingMessage msg)
        {
            MethodArgs.Clear();
            var parameters = f.GetParameters();

            if (Parsers.Count == 0)
                AddAllParsers();

            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                Type type = parameter.ParameterType;

                if (!Parsers.ContainsKey(type))
                {
                    JNet.Error($"Failed to find parser for incoming data type: {type.FullName}. Method invocation will probably fail.");
                    continue;
                }

                ReadWrite rw = Parsers[type];
                object arg = rw(true, msg, null, null);
                MethodArgs.Add(arg);
            }

            return MethodArgs.ToArray();
        }

        private MethodInfo GetCmd(string methodName)
        {
            if (CustomGeneratedBehaviour == null)
                return null;

            return CustomGeneratedBehaviour.GetCmd(methodName);
        }

        private MethodInfo GetRpc(string methodName)
        {
            if (CustomGeneratedBehaviour == null)
                return null;

            return CustomGeneratedBehaviour.GetRpc(methodName);
        }

        internal void HandleRMCMessage(NetIncomingMessage msg, byte methodID)
        {
            // TODO catch exceptions.

            bool expectCmd = JNet.IsServer;
            string methodName = CustomGeneratedBehaviour.GetMethodName(methodID);
            (var method, bool isCmd) = CustomGeneratedBehaviour.RemoteMethods[methodName];

            if(expectCmd != isCmd)
            {
                JNet.Error("Wrong remote type?? Invesitgate.");
                return;
            }

            if (isCmd)
            {
                // Check message sender for ownership of this object.
                long owner = this.NetObject.OwnerID;
                long messageSender = msg.SenderConnection.RemoteUniqueIdentifier;

                if(owner != messageSender)
                {
                    JNet.Error($"Got CMD from a client for an object that they don't own. Serious bug or hacking attempt? owner ID: {owner}, message sender: {messageSender}.");
                    return;
                }
            }

            // Read arguments.
            object[] args = MsgToArgs(method, msg);

            try
            {
                method.Invoke(this, args);
            }
            catch(Exception e)
            {
                JNet.Error($"Exception invoking {(isCmd ? "CMD" : "RPC")} {method.Name} - {e.GetType().Name}: {e.Message}");
            }
        }

        internal void NetAwake()
        {
            // This (derived) class should have a corresponding autogenerated netcode class. Let's find it!
            Type c = GetGeneratedType(this.GetType());

            if (c == null)
            {
                JNet.Error($"Failed to find network autogenerated class for {this.GetType().FullName}, perhaps network code needs to be regenerated?");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPaused = true;
#endif
                return;
            }

            var constructor = c.GetConstructors()[0];
            var instance = constructor.Invoke(null);

            CustomGeneratedBehaviour = instance as JNetGeneratedBehaviour;
        }

        internal void NetUpdate()
        {
            if (CustomGeneratedBehaviour == null)
                return;

            CustomGeneratedBehaviour.Update(this);
        }

        internal void NetSerialize(NetOutgoingMessage msg, bool isForFirst)
        {
            LastSerializedFrame = Time.frameCount;

            if (CustomGeneratedBehaviour != null)
                CustomGeneratedBehaviour.Serialize(this, msg, isForFirst);

            this.Serialize(msg, isForFirst);
        }

        internal void NetDeserialize(NetIncomingMessage msg, bool first)
        {
            LastDeserializedFrame = Time.frameCount;

            if (CustomGeneratedBehaviour != null)
                CustomGeneratedBehaviour.Deserialize(this, msg, first);

            this.Deserialize(msg, first);
        }

        /// <summary>
        /// Called when this object needs to (or has requested to be) serialized to be sent to clients.
        /// Only called when server is running.
        /// </summary>
        /// <param name="msg">The message to write data to.</param>
        /// <param name="isForFirst">If true, then this serialization is for a new client that needs this object, or when this object first spawns.</param>
        public virtual void Serialize(NetOutgoingMessage msg, bool isForFirst)
        {

        }

        /// <summary>
        /// Called on clients (but not when hosting!) when the server has serialized data that needs to be processed or stored on this client.
        /// </summary>
        /// <param name="msg">The message to read data from.</param>
        /// <param name="first">If true, then this serialized message is the first to ever be recieved by this client, such as when joining a server or when a new object spawns.</param>
        public virtual void Deserialize(NetIncomingMessage msg, bool first)
        {

        }

        public override string ToString()
        {
            return string.Format("{0}.{1} : {2}.{3}", gameObject.name, GetType().Name, NetObject.NetID, BehaviourID);
        }
    }
}
