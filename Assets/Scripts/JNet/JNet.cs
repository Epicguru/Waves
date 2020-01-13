using JNetworking.Internal;
using JNetworking.Playback;
using Lidgren.Network;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace JNetworking
{
    public static class JNet
    {
        internal static bool USE_CONSOLE_PRINT = false;
        internal static bool USE_REQUEST_ENCRYPTION = true;
        private const string TAG = "[JNet] ";
        private const string ERROR_TAG = "[JNet] (Error) ";
        /// <summary>
        /// The number of reserved data tags, when sending and recieving data messages.
        /// When implementing custom data messages, do not use any data tags lower than this one.
        /// </summary>
        public const byte RESERVED_ID_COUNT = 7;

        private static NetObject[] Prefabs;
        private static int MaxPrefabID = 1;

        /// <summary>
        /// The application unique identifier. Used when establishing connections.
        /// </summary>
        public static string AppID { get; private set; } = null;
        /// <summary>
        /// Is JNet initialized?
        /// </summary>
        public static bool Initialized { get; private set; } = false;
        /// <summary>
        /// When true, object loaded or present in the scene is included in the networking process. Otherwise,
        /// the NetBehaviours on loaded objects in the scene will not function correctly.
        /// </summary>
        public static bool EnableSceneObjectNetworking { get; set; } = false;
        /// <summary>
        /// The connection status of the active client. If there is no active client, this will return Disconnected.
        /// </summary>
        public static NetConnectionStatus ClientConnectonStatus { get { return !IsClient ? NetConnectionStatus.Disconnected : Client.ConnectionStatus; } }
        /// <summary>
        /// The there a server instance?
        /// </summary>
        public static bool IsServer { get { return Server != null; } }
        /// <summary>
        /// Is there a client instance? (Does not mean client is connected, see <see cref="ClientConnectonStatus"/>).
        /// </summary>
        public static bool IsClient { get { return Client != null; } }
        /// <summary>
        /// Are there a client instance and a server instance at the same time? Does not actually check to see if the client
        /// is connected to this local server, it is just assumed.
        /// </summary>
        public static bool IsHost { get { return IsServer && IsClient; } }
        /// <summary>
        /// Gets the number of tracked gameobjects in the networking system at the current time.
        /// </summary>
        public static int TrackedObjectCount { get { return Tracker?.ObjectCount ?? 0; } }
        /// <summary>
        /// Gets the number of registered prefabs.
        /// </summary>
        public static int PrefabCount { get { return Prefabs == null ? 0 : MaxPrefabID - 1; } }
        /// <summary>
        /// Gets the number of incoming bytes in the last second for the currently active client.
        /// Note that when running as a host, this number is often very low or zero, which is a misrepresentation of the real network usage.
        /// This does not include bytes sent, or bytes recieved by the server.
        /// </summary>
        public static int ClientBytesPerSecond { get; private set; }
        /// <summary>
        /// Gets the number of network messages recieved in the last second by the currently active client.
        /// When running as host, this number may be very low or even zero, which is a misrepresentation of real network useage.
        /// </summary>
        public static int ClientMessagesPerSecond { get; private set; }

        public static JNetPlayback Playback { get; private set; }
        internal static WorldStateTracker Tracker;
        internal static int CurrentBytesIn;
        internal static int CurrentMessagesIn;
        private static JNetClient Client;
        private static JNetServer Server;
        private static float ByteTimer;

        /// <summary>
        /// Initializes the JNet system. This must be called before any other JNet functionality can be used.
        /// </summary>
        /// <param name="appID">The application unique indentifier, must not be null or blank. Any leading or trailing whitespace will be trimmed.</param>
        /// <param name="updateClassMap">If false, the class map for NetBehaviours is not refreshed, which can lead to netcode failing to work. Only used for unit testing pruposes internally.</param>
        public static void Init(string appID, bool includeSceneObjects = true)
        {
            if (Initialized)
            {
                Error("Already initialized.");
                return;
            }

            // Validate the app id.
            if (string.IsNullOrWhiteSpace(appID))
            {
                Error("App ID supplied is null or blank, invalid. JNet has not been initialized.");
                return;
            }

            JNet.EnableSceneObjectNetworking = includeSceneObjects;

            AppID = appID.Trim();
            Tracker = new WorldStateTracker();
            Prefabs = new NetObject[ushort.MaxValue + 1];
            Playback = new JNetPlayback();

            NetBehaviour.UpdateClassMap();

            Initialized = true;
        }

        public static void Dispose()
        {
            if (!Initialized)
            {
                Error("Not initialized.");
                return;
            }

            if(IsClient)           
                ShutdownClient("Client process ended.");            

            if(IsServer)
                ShutdownServer("Server process ended.");

            AppID = null;
            Tracker.Reset();
            Tracker = null;
            Prefabs = null;
            Playback.Dispose();
            Playback = null;

            Initialized = false;
        }

        internal static NetObject GetPrefab(ushort id)
        {
            if (id == 0)
                return null;

            return Prefabs[id];
        }

        /// <summary>
        /// Registers a prefab to the network system, allowing it to be replicated accross clients.
        /// </summary>
        /// <param name="prefab"></param>
        public static void RegisterPrefab(NetObject prefab)
        {
            if(prefab == null)
            {
                Error("Tried to register null prefab.");
                return;
            }

            if (prefab.HasNetID)
            {
                Error("Tried to register net prefab that is actually world instance. (already spawned and reigistered)");
                return;
            }

            if (prefab.IsRegisteredPrefab)
            {
                Error(string.Format("Tried to register net prefab '{0}' twice.", prefab));
                return;
            }

            if(MaxPrefabID > ushort.MaxValue)
            {
                Error("Cannot register more than 65534 network prefabs. Nice work getting that many prefabs though.");
                return;
            }

            ushort id = (ushort)MaxPrefabID;
            MaxPrefabID++;

            prefab.PrefabID = id;
            prefab.IsRegisteredPrefab = true;

            Prefabs[id] = prefab;
        }

        /// <summary>
        /// Removes all currently registered net prefabs.
        /// </summary>
        public static void ClearPrefabs()
        {
            for (int i = 0; i < Prefabs.Length; i++)
            {
                var p = Prefabs[i];
                if(p != null)
                {
                    p.PrefabID = 0;
                }
                Prefabs[i] = null;
            }
            MaxPrefabID = 1;
        }

        /// <summary>
        /// Utility that will check if the server is active. If it is not active, an exception is thrown with the given message.
        /// </summary>
        /// <param name="errorMessage"></param>
        public static void CheckServer(string errorMessage)
        {
            if (!JNet.IsServer)
            {
                throw new JNetException(errorMessage);
            }
        }

        /// <summary>
        /// Gets the currently active server, if any.
        /// </summary>
        public static JNetServer GetServer()
        {
            return Server;
        }

        /// <summary>
        /// Gets the currently active client, if any.
        /// </summary>
        /// <returns></returns>
        public static JNetClient GetClient()
        {
            return Client;
        }

        /// <summary>
        /// Updates the active server and client, in that order, if either are present.
        /// </summary>
        public static void Update()
        {
            if (!Initialized)
            {
                Error("Not initialized, cannot update. Call JNet.Init() first.");
                return;
            }

            Playback.Update();

            if (IsServer)
            {
                Server.Update();
            }
            if (IsClient)
            {
                Client.Update();
            }

            if (Tracker != null && IsServer)
                Tracker.SerializeAll();

            ByteTimer += Time.unscaledDeltaTime;
            if(ByteTimer >= 1f)
            {
                ClientBytesPerSecond = CurrentBytesIn;
                ClientMessagesPerSecond = CurrentMessagesIn;
                CurrentBytesIn = 0;
                CurrentMessagesIn = 0;
                ByteTimer -= 1f;
            }
        }

        public static NetOutgoingMessage CreateCustomMessage(bool fromServer, byte id, int initialCapacity = 10)
        {
            if(id < RESERVED_ID_COUNT)
            {
                Error($"Custom message Id is {id}, IDs up to {RESERVED_ID_COUNT - 1} are reserved for internal use. Use a higher ID!");
                return null;
            }

            NetOutgoingMessage msg = null;
            if (fromServer)
            {
                if (!IsServer)
                {
                    Error("Not running server, cannot create custom message. Perhaps fromServer should be false?");
                    return null;
                }
                msg = Server.CreateMessage(initialCapacity: initialCapacity);
            }
            else
            {
                if (!IsClient || ClientConnectonStatus != NetConnectionStatus.Connected)
                {
                    Error("Client not running or not connected, cannot create custom message. Perhaps fromServer should be true?");
                    return null;
                }
                msg = Client.CreateMessage(initialCapacity: initialCapacity);
            }

            msg.Write(id);
            return msg;
        }

        /// <summary>
        /// Sends a custom message created by the client to the server.
        /// </summary>
        public static NetSendResult SendCustomMessageToServer(NetOutgoingMessage msg, NetDeliveryMethod delivery = NetDeliveryMethod.ReliableOrdered, int sequenceChannel = 31)
        {
            if (!IsClient || ClientConnectonStatus != NetConnectionStatus.Connected)
            {
                Error("Client not active or connected, cannot send custom message.");
                return NetSendResult.FailedNotConnected;
            }

            if(msg == null)
            {
                Error("Null custom message.");
                return NetSendResult.Dropped;
            }

            return Client.Send(msg, delivery, sequenceChannel);
        }

        /// <summary>
        /// Sends a custom message created by the server to a particlar connected client.
        /// </summary>
        public static NetSendResult SendCustomMessageToClient(NetConnection n, NetOutgoingMessage msg, NetDeliveryMethod delivery = NetDeliveryMethod.ReliableOrdered, int sequenceChannel = 31)
        {
            if (!IsServer)
            {
                Error("Server not running, cannot send custom message.");
                return NetSendResult.FailedNotConnected;
            }

            if(n == null || n.Status != NetConnectionStatus.Connected)
            {
                Error("Connection is null or no longer connected, cannot send custom data message.");
                return NetSendResult.FailedNotConnected;
            }

            if (msg == null)
            {
                Error("Null custom message.");
                return NetSendResult.Dropped;
            }

            return Server.SendMessage(msg, n, delivery, sequenceChannel);
        }

        /// <summary>
        /// Sends a custom message created by the server to all connected clients, optionally excluding one client.
        /// The 'except' client may be null to send to all.
        /// </summary>
        public static NetSendResult SendCustomMessageToAll(NetConnection except, NetOutgoingMessage msg, NetDeliveryMethod delivery = NetDeliveryMethod.ReliableOrdered, int sequenceChannel = 31)
        {
            if (!IsServer)
            {
                Error("Server not running, cannot send custom message.");
                return NetSendResult.FailedNotConnected;
            }

            if (msg == null)
            {
                Error("Null custom message.");
                return NetSendResult.Dropped;
            }

            if(except == null)
            {
                Server.SendToAll(msg, delivery, sequenceChannel);
                return NetSendResult.Sent;
            }
            else
            {
                Server.SendToAllExcept(except, msg, delivery, sequenceChannel);
                return NetSendResult.Sent;
            }
        }

        /// <summary>
        /// Combines two network messages into one. Requires an active client or server to perform the merge.
        /// </summary>
        /// <param name="a">The message to be placed first.</param>
        /// <param name="b">The message that goes after the first.</param>
        /// <param name="fromServer">If true, then the server is used to merge. If false, the client is used.</param>
        /// <returns>The message that contains the two messages.</returns>
        public static NetOutgoingMessage CreateCombinedMessage(NetOutgoingMessage a, NetOutgoingMessage b, bool fromServer)
        {
            if (a == null || b == null)
            {
                Error("Message A or B are null.");
                return null;
            }

            if (fromServer && !IsServer)
            {
                Error("Server is null, use fromServer=false instead.");
                return null;
            }

            if (!fromServer && !IsClient)
            {
                Error("Client is null, use fromClient=false instead.");
                return null;
            }

            int length = a.LengthBytes + b.LengthBytes;
            NetOutgoingMessage msg = fromServer ? Server.CreateMessage(initialCapacity: length) : Client.CreateMessage(initialCapacity: length);

            msg.Write(a);
            msg.Write(b);

            return msg;
        }

        private static bool GetBit(int bitIndex, byte source)
        {
            if (bitIndex < 0 || bitIndex >= 8)
                throw new Exception("*crying in weeb* OwO why u do this to me?");

            // bit index 0 is msb.
            int inverted = 7 - bitIndex;
            const byte MASK = 0b_0000_0001;

            int shifted = source >> inverted;
            int masked = shifted & MASK;

            return masked == 1;
        }

        private static bool GetBitFromArray(int bitIndex, byte[] source)
        {
            int byteIndex = bitIndex / 8;
            int realBit = bitIndex % 8;

            if (byteIndex >= source.Length)
                Debug.LogError($"byte index {byteIndex}, source length: {source.Length}, {bitIndex}");

            return GetBit(realBit, source[byteIndex]);
        }

        private static void WriteBit(ref byte theByte, int bitIndex, bool value)
        {
            int mask = GetBitMask(bitIndex);            

            // Mask:
            // 0010
            // ~mask
            // 1101
            // theByte:
            // 0110
            // value:
            // false (0)

            // Masked
            // 0100
            // Final
            // 0100

            int masked = theByte & ~mask;
            int final = masked;
            if (value)
                final = final | mask;

            theByte = (byte)final;
        }

        private static int GetBitMask(int bitIndex)
        {
            switch (bitIndex)
            {
                case 0:
                    return 128;
                case 1:
                    return 64;
                case 2:
                    return 32;
                case 3:
                    return 16;
                case 4:
                    return 8;
                case 5:
                    return 4;
                case 6:
                    return 2;
                case 7:
                    return 1;
            }

            return 0;
        }

        private static int GetByteMask(int bitsToTake)
        {
            switch (bitsToTake)
            {
                case 0:
                    return 0b_0000_0000;
                case 1:
                    return 0b_1000_0000;
                case 2:
                    return 0b_1100_0000;
                case 3:
                    return 0b_1110_0000;
                case 4:
                    return 0b_1111_0000;
                case 5:
                    return 0b_1111_1000;
                case 6:
                    return 0b_1111_1100;
                case 7:
                    return 0b_1111_1110;
                case 8:
                    return 0b_1111_1111;
            }

            return 0;
        }

        internal static void Assert(bool condition, string uponFailure)
        {
            if (!condition)
                throw new JNetException(uponFailure);
        }

        /// <summary>
        /// Gets an enumerator for all currently active NetObjects. Fairly slow, so don't call this often.
        /// </summary>
        public static IEnumerable<NetObject> GetCurrentNetObjects()
        {
            if (Tracker == null)
                return null;

            return Tracker.ActiveObjects;
        }

        /// <summary>
        /// Returns the NetObject given that object's NetID. May return null if not found.
        /// </summary>
        /// <param name="netID">The network ID of the object.</param>
        /// <returns>The NetObject or null if not found.</returns>
        public static NetObject GetObject(ushort netID)
        {
            if (Tracker == null)
                return null;

            if (netID == 0)
                return null;

            return Tracker.GetObject(netID);
        }

        /// <summary>
        /// Returns a NetBehaviour on a spawned net object given the object's NetID and the behaviour ID.
        /// </summary>
        /// <param name="objectNetID">The network ID of the object.</param>
        /// <param name="behaviourID">The ID of the behaviour on the object.</param>
        /// <returns>The NetBehaviour or null if the object was not found or the behaviour was not on the object.</returns>
        public static NetBehaviour GetBehaviour(ushort objectNetID, byte behaviourID)
        {
            if (Tracker == null)
                return null;

            return Tracker.GetBehaviour(objectNetID, behaviourID);
        }

        private static void RegisterAllSceneObjects()
        {
            var found = GameObject.FindObjectsOfType<NetObject>();
            foreach (var item in found)
            {
                item.IsSceneLoaded = true;
                Tracker.Register(item);
            }

            Log($"Registered {found.Length} scene-loaded net objects.");
        }

        internal static NetPeerConfiguration GetDefaultConfig()
        {
            var config = new NetPeerConfiguration(AppID);

            return config;
        }

        /// <summary>
        /// Starts up the server. There can only be one active server per application instance, and only one server per port per machine.
        /// </summary>
        /// <param name="name">The name of the server to create. Cannot be null or blank.</param>
        /// <param name="port">The port to use on the server. Don't use any reserved ports!</param>
        /// <param name="maxConnections">The maximum number of connections to allow at once. There is not hard limit.</param>
        public static void StartServer(string name, int port, int maxConnections)
        {
            if (!Initialized)
            {
                Error("Call Init() before starting the server.");
                return;
            }

            NetPeerConfiguration config = GetDefaultConfig();

            config.MaximumConnections = maxConnections;
            config.Port = port;

            StartServer(name, config);
        }

        /// <summary>
        /// Starts up the server. There can only be one active server per application instance,
        /// and only one server per port per machine.
        /// The NetPeerConfiguration allows for more customization and optimisation of the server.
        /// </summary>
        /// <param name="name">The name of the server to create. Cannot be null or blank.</param>
        /// <param name="config">The NetPeerConfiguration to use on the server. Should at least specify port number.</param>
        public static void StartServer(string name, NetPeerConfiguration config)
        {
            if (!Initialized)
            {
                Error("Call Init() before starting the server.");
                return;
            }

            if (IsServer)
            {
                Error("A server is already active. Shutdown the old server before opening this new one.");
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                Error("Name for new server is null, server starting cancelled.");
                return;
            }

            if(config == null)
            {
                Error("Config is null, server starting cancelled.");
                return;
            }

            try
            {
                config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
                config.AcceptIncomingConnections = true;
                JNetServer s = new JNetServer(name, config);
                s.Start();
                JNet.Server = s;

                Log("Server now active on port " + config.Port);
            }
            catch (Exception e)
            {
                throw new JNetException(string.Format("Exception creating and starting server. See inner exception ({0})", e.GetType().FullName), e);
            }

            bool didUnregister = Tracker.Reset();
            if (didUnregister)
            {
                Error("When server was activated, networked objects had to be untracked. Is the client connected to a different remote server?");
            }

            // Register existing scene objects...
            if (EnableSceneObjectNetworking)
            {
                RegisterAllSceneObjects();
            }
        }

        /// <summary>
        /// Shuts down and disposes of the currently active server, disconnecting all clients.
        /// </summary>
        /// <param name="bye">The message to display to disconnected clients.</param>
        public static void ShutdownServer(string bye)
        {
            if (!IsServer)
            {
                Error("Server is already shut down and disposed.");
                return;
            }

            Server.Shutdown(bye);
            Server = null;

            Tracker.Reset();

            Log("Server shut down and disposed of.");
        }

        /// <summary>
        /// Creates a new client. There can only be one client instance per application.
        /// The client can connect to a locally running server or to a remote server.
        /// Note that this does not connect the client to any server, only prepare it for use.
        /// </summary>
        public static void StartClient()
        {
            StartClient(GetDefaultConfig());
        }

        /// <summary>
        /// Creates a new client. There can only be one client instance per application.
        /// The client can connect to a locally running server or to a remote server.
        /// Note that this does not connect the client to any server, only prepare it for use.
        /// The configuration allows for deeper optimization of the client.
        /// </summary>
        /// <param name="config">The configuration that the client will use.</param>
        public static void StartClient(NetPeerConfiguration config)
        {
            if (!Initialized)
            {
                Error("Call Init() before starting the client.");
                return;
            }

            if (IsClient)
            {
                Error("Client already created!");
                return;
            }

            if(config == null)
            {
                Error("Config cannot be null, client not created.");
                return;
            }

            try
            {
                JNetClient c = new JNetClient(config);
                c.Start();
                JNet.Client = c;

                Log("Client created and ready to connect.");
            }
            catch(Exception e)
            {
                throw new JNetException(string.Format("Exception creating client. See inner exception ({0})", e.GetType().FullName), e);
            }
        }

        /// <summary>
        /// Connects the active client [see <see cref="StartClient"/>] to the specified address and port.
        /// If attempting to connect the client to the local device, ALWAYS USE <see cref="ConnectClientToHost(NetOutgoingMessage)"/>.
        /// Only works if the client is not already connected or connecting.
        /// </summary>
        /// <param name="host">The address to connect to. Use 127.0.0.1 to connect to a local server.</param>
        /// <param name="port">The port number to connect on.</param>
        public static void ConnectClientToRemote(string host, int port)
        {
            ConnectClientToRemote(host, port, null);
        }

        /// <summary>
        /// Connects the active client [see <see cref="StartClient"/>] to the specified address and port.
        /// If attempting to connect the client to the local device, ALWAYS USE <see cref="ConnectClientToHost(NetOutgoingMessage)"/>.
        /// Only works if the client is not already connected or connecting.
        /// </summary>
        /// <param name="host">The address to connect to. Use 127.0.0.1 to connect to a local server.</param>
        /// <param name="port">The port number to connect on.</param>
        /// <param name="msg">A hail message. The contents of this message will be read in <see cref="JNetServer.UponConnectionRequest"/> if this behaviour is enabled.</param>
        public static void ConnectClientToRemote(string host, int port, NetOutgoingMessage msg)
        {
            if (!Initialized)
            {
                Error("Call Init() before connecting the client.");
                return;
            }

            if (!IsClient)
            {
                Error("Client is not created. Call StartClient before trying to connect.");
                return;
            }

            if (ClientConnectonStatus != NetConnectionStatus.Disconnected)
            {
                Error("Client is already connected, connecting or disconnecting. Cannot start connect now.");
                return;
            }

            host = host.Trim();
            if(host == "localhost" || host == "127.0.0.1" && IsServer)
            {
                Error("Possibly attempting to connect to local server using ConnectToRemote. Consider ConnectClientToHost instead.");
                // return;
            }

            if (!IsServer)
            {
                Tracker.Reset();
                if (EnableSceneObjectNetworking)
                {
                    RegisterAllSceneObjects();
                }
            }

            NetOutgoingMessage required = Client.CreateMessage();
            required.Write(false);
            required.Write(double.MinValue);

            if (msg != null)
                Client.Connect(host, port, JNet.CreateCombinedMessage(required, msg, false));
            else
                Client.Connect(host, port, required);            

            Log("Started client connect to " + host + " on port " + port);
        }

        /// <summary>
        /// Connects the active client [see <see cref="StartClient"/>] to the locally running server.
        /// It is important to use this method when connecting to the local server since it allows the server
        /// to exclude it from certain messages.
        /// Only works if the client is not already connected or connecting.
        /// </summary>
        /// <param name="msg">The optional hail/connection request message. Can be null.</param>
        public static void ConnectClientToHost(NetOutgoingMessage msg)
        {
            if (!Initialized)
            {
                Error("Call Init() before connecting the client.");
                return;
            }

            if (!IsClient)
            {
                Error("Client is not created. Call StartClient before trying to connect.");
                return;
            }

            if (ClientConnectonStatus != NetConnectionStatus.Disconnected)
            {
                Error("Client is already connected, connecting or disconnecting. Cannot start connect now.");
                return;
            }

            if (!IsServer)
            {
                Error("Server is not running on this instance, cannot connect client as host. Use JNet.ConnectClientToRemote instead.");
                return;
            }

            Client.IsHost = true;

            NetOutgoingMessage required = Client.CreateMessage();
            required.Write(true);
            required.Write(Server.GenNewHostKey());

            if (msg != null)
                Client.Connect("127.0.0.1", Server.Port, JNet.CreateCombinedMessage(required, msg, false));
            else
                Client.Connect("127.0.0.1", Server.Port, required);

            Log("Started client connect to (localhost) on port " + Server.Port);
        }

        /// <summary>
        /// Disconnects the active client from the server it is connected to.
        /// </summary>
        /// <param name="bye">The message to give the server, such as a reason for disconnection.</param>
        public static void DisconnectClient(string bye)
        {
            if (!Initialized)
            {
                Error("Call Init() before disconnecting down the client.");
                return;
            }

            if (!IsClient)
            {
                Error("Client is not created yet, cannot disconnect!");
                return;
            }

            if(Client.ConnectionStatus != NetConnectionStatus.Connected)
            {
                Error(string.Format("Client is not connected ({0}), cannot disconnect!", Client.ConnectionStatus));
                return;
            }

            Client.Disconnect(bye);

            if (!IsServer)
                Tracker.Reset();
        }

        public static void ShutdownClient(string bye)
        {
            if (!Initialized)
            {
                Error("Call Init() before disconnecting down the client.");
                return;
            }

            if (!IsClient)
            {
                Error("Client is not created yet, cannot disconnect!");
                return;
            }

            if (Client.ConnectionStatus == NetConnectionStatus.Connected)
            {
                DisconnectClient(bye);
            }

            Client = null;
        }

        internal static void Error(string s)
        {
            Print(s, true);
        }

        internal static void Log(string s)
        {
            Print(s, false);
        }

        /// <summary>
        /// Makes a spawned object be registered to the networking system, causing it to be 
        /// instantiated on all connected clients.
        /// Note that this does NOT instantiate the object on the server. The object should already be instantiated,
        /// and this method just registers it. In other words, do not pass a prefab to this method.
        /// </summary>
        /// <param name="obj">A behaviour on the net object to spawn.</param>
        public static void Spawn(NetBehaviour behaviour, NetConnection owner = null)
        {
            JNet.Spawn(behaviour.NetObject, owner);
        }

        /// <summary>
        /// Makes a spawned object be registered to the networking system, causing it to be 
        /// instantiated on all connected clients.
        /// Note that this does NOT instantiate the object on the server. The object should already be instantiated,
        /// and this method just registers it. In other words, do not pass a prefab to this method.
        /// </summary>
        /// <param name="obj">The object to spawn. Must have a NetObject component attached to it.</param>
        public static void Spawn(GameObject go, NetConnection owner = null)
        {
            if (go == null)
                return;

            var comp = go.GetComponent<NetObject>();
            if (comp != null)
                Spawn(comp, owner);
        }

        /// <summary>
        /// Makes a spawned object be registered to the networking system, causing it to be 
        /// instantiated on all connected clients.
        /// Note that this does NOT instantiate the object on the server. The object should already be instantiated,
        /// and this method just registers it. In other words, do not pass a prefab to this method.
        /// </summary>
        /// <param name="obj">The net object to spawn.</param>
        public static void Spawn(NetObject obj, NetConnection owner = null)
        {
            if (!Initialized)
            {
                Error("JNet not initialized, cannot spawn object. Call JNet.Init()");
                return;
            }

            if (!IsServer)
            {
                Error("Network spawning should only called on the server.");
            }

            if(obj == null)
            {
                Error("Null object was attempted to be spawned using JNet.Spawn()");
                return;
            }

            if (obj.HasNetID)
            {
                Error(string.Format("Object {0} is already networked registered.", obj.ToString()));
                return;
            }

            if (!obj.IsSceneLoaded && !obj.HasPrefabID)
            {
                Error(string.Format("Object '{0}' does not have a prefab ID, register it's prefab before spawning by calling JNet.RegisterPrefab().", obj));
                return;
            }

            // Set owner.
            if(owner != null)
            {
                obj.OwnerID = owner.RemoteUniqueIdentifier;
                Server.GetClient(owner)?.AddObj(obj);
            }
            else
            {
                obj.OwnerID = 0; // NOTE: Can remote unique identifier actually ever be zero? I have no idea.
            }

            Tracker.Register(obj);

            // Send a message to all clients to spawn this object.
            NetOutgoingMessage msg = Server.CreateMessage(JDataType.SPAWN, 16);

            // Prefab ID.
            msg.Write(obj.PrefabID);

            // Instance ID.
            msg.Write(obj.NetID);

            // Write all behaviours and owner, regardless of net dirty state.
            obj.Serialize(msg, true);

            // Send to all except local.
            Server.SendToAllExcept(Server.LocalClientConnection, msg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        /// <summary>
        /// Sets the OwnerID of the target NetObject to the specified net connection.
        /// </summary>
        /// <param name="obj">The target NetObject. <see cref="JNet.Spawn"/> should have already been called for this object.</param>
        /// <param name="owner">The new owner for this object. Can be null to remove owner.</param>
        public static void SetOwner(NetObject obj, NetConnection owner)
        {
            if (!JNet.IsServer)
            {
                Error("Not on server, cannot set owner.");
                return;
            }

            if (obj == null)
                return;

            long id = 0;
            if (owner != null)
                id = owner.RemoteUniqueIdentifier;

            if(obj.OwnerID != id)
            {
                var client = Server.GetClient(obj.OwnerID);
                if (obj.OwnerID != 0)
                {
                    client?.RemoveObj(obj);
                }

                obj.OwnerID = id;
                if(id != 0)
                    client?.AddObj(obj);

                // Send message.
                NetOutgoingMessage msg = Server.CreateMessage(JDataType.SET_OWNER, 12);
                msg.Write(obj.NetID);
                msg.Write(obj.OwnerID);
                Server.SendToAllExcept(Server.LocalClientConnection, msg, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        internal static void Despawn(NetObject obj)
        {
            if (!IsServer)
                return;

            if (Tracker == null)
                return;

            if(obj.OwnerID != 0)
            {
                Server.GetClient(obj.OwnerID, true)?.RemoveObj(obj);
            }

            // Send a message to all clients to despawn this object.
            NetOutgoingMessage msg = Server.CreateMessage(JDataType.DESPAWN, 16);

            // Instance Net ID.
            msg.Write(obj.NetID);

            // Send to all except local.
            Server.SendToAllExcept(Client?.ServerConnection, msg, NetDeliveryMethod.ReliableOrdered, 0);

            Tracker.Unregister(obj);
        }

        private static void Print(string s, bool error)
        {
            if (USE_CONSOLE_PRINT)
            {
                if (error)
                    System.Console.WriteLine(TAG + (s ?? "null"));
                else
                    System.Console.WriteLine(ERROR_TAG + (s ?? "null"));

            }

            if (error)
                UnityEngine.Debug.LogError(TAG + (s ?? "null"));
            else
                UnityEngine.Debug.Log(TAG + (s ?? "null"));
        }
    }

    public delegate void CustomDataProcess(byte tagID, NetIncomingMessage msg);
}

