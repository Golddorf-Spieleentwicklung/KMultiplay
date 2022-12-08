using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KMultiplay.Transport;

namespace KMultiplay.Connections
{
    public static class KMultiplay_Connections
    {
        public static KMultiplay_Connection Self;
        public static KMultiplay_Connection MasterServer;
        public static KMultiplay_Connection Host;

        public static List<KMultiplay_Connection> Connections = new List<KMultiplay_Connection>();
        public delegate void Connected(KMultiplay_Connection connection);
        public static Connected OnConnected;
        public delegate void Disconnected(KMultiplay_Connection connection, DisconnectionReason reason);
        public static Disconnected OnDisconnected;

        public static void SendRPC(KMultiplay_RPC rpc)
        {
            List<KMultiplay_Endpoint> endpoints = GetTargetEndpoints(rpc.Target);
            foreach(KMultiplay_Endpoint endpoint in endpoints)
            {
                KMultiplay_Transport.SendData(rpc, endpoint);
            }
        }

        public static KMultiplay_PendingPacket SendRPC(KMultiplay_RPC rpc, KMultiplay_Endpoint targetEP)
        {
            return KMultiplay_Transport.SendData(rpc, targetEP);
        }

        public static List<KMultiplay_Endpoint> GetTargetEndpoints(KMultiplay_Target target)
        {
            List<KMultiplay_Endpoint> endpoints = new List<KMultiplay_Endpoint>();
            switch(target)
            {
                case KMultiplay_Target.MasterServer:
                    endpoints.Add(MasterServer.Remote);
                    return endpoints;
                case KMultiplay_Target.None:
                    return endpoints;
                case KMultiplay_Target.OnlyHost:
                    if(!Host.IsRemoteEndpoint)
                        endpoints.Add(Host.Local);
                    else
                        endpoints.Add(Host.Remote);
                    return endpoints;
                case KMultiplay_Target.Self:
                    endpoints.Add(Self.Local);
                    return endpoints;
                case KMultiplay_Target.Specific:
                    return endpoints;
            }
            foreach (KMultiplay_Connection connection in Connections)
            {
                switch(target)
                {
                    case KMultiplay_Target.Everyone:
                        endpoints.Add(connection.Remote);
                        break;
                    case KMultiplay_Target.OnlyClients:
                        if (connection.IsRemoteEndpoint && connection != MasterServer)
                            endpoints.Add(connection.Remote);
                        break;
                }
            }
            return endpoints;
        }
    }

    public class KMultiplay_Connection
    {
        public ConnectionType Type;
        public KMultiplay_Endpoint Remote;
        public KMultiplay_Endpoint Local;
        public bool IsRemoteEndpoint
        {
            get
            {
                return Remote != null;
            }
        }
    }

    public enum ConnectionType
    {
        Direct=0,
        Relay=1
    }

    public enum DisconnectionReason
    {
        None=0,
        TimeOut=1
    }
}

