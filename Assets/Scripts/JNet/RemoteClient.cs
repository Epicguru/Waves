
using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Text;

namespace JNetworking
{
    public class RemoteClient : IDisposable
    {
        public NetConnection Connection { get; private set; }
        public long ConnectionID { get; private set; }
        public string ConnectionIDHex { get; private set; }
        public IEnumerable<NetObject> OwnedObjects { get { return OwnedObjectsList; } }
        public object Data { get; set; }
        public bool HasData { get { return Data != null; } }
        public float RTT { get { return Connection?.AverageRoundtripTime ?? 0f; } }
        public bool IsLocalClient { get { return JNet.GetServer() == null ? false : Connection == JNet.GetServer().LocalClientConnection; } }

        internal readonly List<NetObject> OwnedObjectsList = new List<NetObject>();

        public bool IsActive
        {
            get
            {
                return Connection != null && Connection.Status == NetConnectionStatus.Connected;
            }
        }

        internal RemoteClient(NetConnection connection)
        {
            this.Connection = connection;
            this.ConnectionID = connection.RemoteUniqueIdentifier;

            this.ConnectionIDHex = System.Convert.ToString(ConnectionID, 16).ToUpper().PadLeft(16, '0');
            StringBuilder str = new StringBuilder();
            for (int i = 0; i < 4; i++)
            {
                string sub = ConnectionIDHex.Substring(i * 4, 4);
                str.Append(sub);
                if(i != 3)
                    str.Append(':');
            }
            ConnectionIDHex = str.ToString();
        }

        internal void AddObj(NetObject o)
        {
            if (o == null)
                return;

            if (OwnedObjectsList.Contains(o))
                return;

            OwnedObjectsList.Add(o);
        }

        internal void RemoveObj(NetObject o)
        {
            if (o == null)
                return;

            if (OwnedObjectsList.Contains(o))
                OwnedObjectsList.Remove(o);
        }

        public T GetData<T>() where T : class
        {
            if (Data == null)
                return default;

            try
            {
                return Data as T;
            }
            catch
            {
                JNet.Error(string.Format("Data is of type {0}, cannot be cast to {1}", Data.GetType().FullName, typeof(T).FullName));
                return default;
            }
        }

        public void Dispose()
        {
            Connection = null;
            Data = null;
        }
    }
}
