using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;

namespace KMultiplay.Transport
{
    public static class KMultiplay_Transport
    {
        private static ConcurrentQueue<KMultiplay_Packet> OutgoingPackets = new ConcurrentQueue<KMultiplay_Packet>();
        public static ConcurrentQueue<KMultiplay_Packet> IncomingPackets = new ConcurrentQueue<KMultiplay_Packet>();
        private static List<KMultiplay_Socket> Sockets = new List<KMultiplay_Socket>();
        private static Action<KMultiplay_Packet> OnRecievedPacket;
        private static Thread SendingThread;
        public static bool IsRunning;
        public static int PacketIndex;
        public static int NextPacketIndex
        {
            get
            {
                PacketIndex++;
                return PacketIndex;
            }
        }

        #region Control

        public static void Start()
        {
            IsRunning = true;
            Sockets = new List<KMultiplay_Socket>();
            OutgoingPackets = new ConcurrentQueue<KMultiplay_Packet>();
            SendingThread = new Thread(Sending);
            SendingThread.Start();
        }

        public static void Update()
        {

        }

        public static void Stop()
        {
            IsRunning = false;
            Sockets.Clear();
            OutgoingPackets.Clear();
            SendingThread.Abort();
        }

        #endregion

        #region Outgoing

        public static KMultiplay_PendingPacket SendData(KMultiplay_RPC rpc, KMultiplay_Endpoint endpoint)
        {
            List<KMultiplay_Packet> packets = KMultiplay_Packet.Raise_Send(rpc, endpoint);
            KMultiplay_PendingPacket pending = new KMultiplay_PendingPacket(packets);
            foreach(KMultiplay_Packet packet in packets)
            {
                OutgoingPackets.Enqueue(packet);
            }
            return pending;
        }

        private static void Sending()
        {
            while (IsRunning)
            {
                if (OutgoingPackets.IsEmpty)
                    return;

                KMultiplay_Packet packet = null;
                if (!OutgoingPackets.TryDequeue(out packet))
                    return;
                for (int i = 0; i < KMultiplay_Settings.PacketsPerSecond; i++)
                {
                    foreach (KMultiplay_Socket socket in Sockets)
                    {
                        if (packet.Endpoint.IsRelay)
                        {
                            // create relay packet here and send it to relay
                        }
                        else
                        {
                            socket.SendStringTo(packet.Packed, packet.Endpoint.Remote);
                        }
                    }
                }
            }
        }

        #endregion

        #region Sockets

        public static KMultiplay_Socket OpenSocket(KMultiplay_Endpoint endpoint, string socketDescription = "")
        {
            if(ContainsSocket(endpoint))
            {
                Debug.Log("KMultiplay - Socket " + endpoint.ToString() + " already open");
                return null;
            }
            KMultiplay_Socket socket = new KMultiplay_Socket(socketDescription);
            socket.Start(endpoint, ReadSocketData);
            Sockets.Add(socket);
            return socket;
        }

        public static KMultiplay_Socket OpenSocket(int port, string socketDescription = "")
        { 
            if (ContainsSocket(port))
            {
                Debug.Log("KMultiplay - Socket on port " + port.ToString() + " already open");
                return null;
            }
            KMultiplay_Endpoint endpoint = new KMultiplay_Endpoint(IPAddress.Any, port);
            KMultiplay_Socket socket = new KMultiplay_Socket(socketDescription);
            socket.Start(endpoint, ReadSocketData);
            Sockets.Add(socket);
            return socket;
        }

        public static void CloseSocket(int port)
        {
            KMultiplay_Socket socket = GetSocket(port);
            if (socket == null)
            {
                Debug.Log("KMultiplay - Socket " + port.ToString() + " does not exist.");
                return;
            }
            socket.Close();
            Sockets.Remove(socket);
        }

        public static void CloseSocket(KMultiplay_Endpoint endpoint)
        {
            KMultiplay_Socket socket = GetSocket(endpoint);
            if (socket == null)
            {
                Debug.Log("KMultiplay - Socket " + endpoint.ToString() + " does not exist.");
                return;
            }
            socket.Close();
            Sockets.Remove(socket);
        }

        private static bool ContainsSocket(KMultiplay_Endpoint endpoint)
        {
            foreach(KMultiplay_Socket socket in Sockets)
            {
                if(endpoint.LocalIP.Equals(socket.LocalEndpoint))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ContainsSocket(int port)
        {
            foreach (KMultiplay_Socket socket in Sockets)
            {
                if (port == socket.LocalEndpoint.Port)
                {
                    return true;
                }
            }
            return false;
        }

        private static KMultiplay_Socket GetSocket(KMultiplay_Endpoint endpoint)
        {
            foreach (KMultiplay_Socket socket in Sockets)
            {
                if (endpoint.LocalIP.Equals(socket.LocalEndpoint))
                {
                    return socket;
                }
            }
            return null;
        }

        private static KMultiplay_Socket GetSocket(int port)
        {
            foreach (KMultiplay_Socket socket in Sockets)
            {
                if (port == socket.LocalEndpoint.Port)
                {
                    return socket;
                }
            }
            return null;
        }

        #endregion

        #region Incoming

        public static void ReadSocketData(string rawData, EndPoint from, KMultiplay_Socket source)
        {
            // check for relay packet here and retrive relayEP and originalEP
            KMultiplay_Endpoint endpoint = new KMultiplay_Endpoint(source.LocalEndpoint, (IPEndPoint)from, null);
            KMultiplay_Packet packet = KMultiplay_Packet.Raise_Recieved(rawData, from, source.LocalEndpoint);
            KMultiplay_Endpoint target = new KMultiplay_Endpoint(source.LocalEndpoint, (IPEndPoint)from);
            SendAck(packet.PacketIndex, target);
        }

        private static void SendAck(int pIndex, KMultiplay_Endpoint target)
        {
            PacketAck ack = new PacketAck(pIndex);
            ack.Send(ack, target);
        }

        #endregion 

    }

    [System.Serializable]
    public class KMultiplay_Packet : IDisposable
    {
        [System.NonSerialized]
        public KMultiplay_Endpoint Endpoint;
        public string RPCData;
        public string Packed
        {
           get
            {
                return Serialize();
            }
        }
        public delegate void Sent(KMultiplay_Packet packet);
        public Sent OnSent;
        public Action<KMultiplay_Packet> PendingChange;

        // important Packet info, should be replicated on recieve
        public int PacketIndex;
        public KMultiplay_PacketType PacketType;
        public int LFTIndex;
        public int LFTPartIndex;
        public int LFTMaxParts;

        public KMultiplay_Packet(string rpcData, int packetIndex, KMultiplay_PacketType packetType, KMultiplay_Endpoint endpoint)
        {
            RPCData = rpcData;
            PacketType = packetType;
            PacketIndex = packetIndex;
            Endpoint = endpoint;
        }

        public static List<KMultiplay_Packet> Raise_Send(KMultiplay_RPC rpc, KMultiplay_Endpoint endpoint)
        {
            List<KMultiplay_Packet> packets = new List<KMultiplay_Packet>();

            KMultiplay_Packet main = new KMultiplay_Packet(rpc.Serialize(), KMultiplay_Transport.NextPacketIndex, KMultiplay_PacketType.SFT, endpoint);

            string mainPacked = main.Packed;
            // check if the packet is a large file
            if(mainPacked.Length > KMultiplay_Settings.MaxFileSize)
            {
                int maxPackets = Mathf.CeilToInt(mainPacked.Length / KMultiplay_Settings.MaxFileSize);
                int currentDataIndex = 0;
                for(int i = 0; i < maxPackets; i++)
                {
                    int subLength = KMultiplay_Settings.MaxFileSize;
                    if (currentDataIndex + KMultiplay_Settings.MaxFileSize > mainPacked.Length)
                        subLength = currentDataIndex + KMultiplay_Settings.MaxFileSize - mainPacked.Length;

                    string partData = mainPacked.Substring(currentDataIndex, subLength);
                    KMultiplay_Packet part = new KMultiplay_Packet(partData, KMultiplay_Transport.NextPacketIndex, KMultiplay_PacketType.LFT, endpoint);
                    currentDataIndex += KMultiplay_Settings.MaxFileSize;
                    part.LFTMaxParts = maxPackets;
                    part.LFTPartIndex = i;
                    part.LFTIndex = main.PacketIndex;
                    packets.Add(part);
                }
            }
            else
            {
                packets.Add(main);
            }

            return packets;
        }

        public static KMultiplay_Packet Raise(string packetData, KMultiplay_Endpoint endpoint)
        {
            KMultiplay_Packet packet = JsonUtility.FromJson<KMultiplay_Packet>(packetData);
            packet.Endpoint = endpoint;
            return packet;
        }

        public static KMultiplay_Packet Raise_Recieved(string packetData, EndPoint from, IPEndPoint source)
        {
            KMultiplay_Packet packet = JsonUtility.FromJson<KMultiplay_Packet>(packetData);
            packet.Endpoint = new KMultiplay_Endpoint(source, (IPEndPoint)from);
            return packet;
        }

        public bool IsValid(string serializedPacket)
        {
            bool valid = false;
            int supposedSize = GetPacketLength(serializedPacket);
            int size = serializedPacket.Length;
            valid = size == supposedSize;
            return valid;
        }

        private static int GetPacketLength(string serialized)
        {
            int size = 0;
            string serializedSize = serialized.Split("{"[0])[0];
            int.TryParse(serializedSize, out size);
            return size;
        }

        public string Serialize()
        {
            string json = JsonUtility.ToJson(this);
            int size = json.Length;
            size += size.ToString().Length;
            json = size.ToString() + json;
            return json;
        }

        private static KMultiplay_Packet Deserialize(string serialized)
        {
            int sizePrefix = GetPacketLength(serialized);
            string jsonSubsTring = serialized.Substring(sizePrefix.ToString().Length, serialized.Length - sizePrefix.ToString().Length);
            KMultiplay_Packet package = JsonUtility.FromJson<KMultiplay_Packet>(jsonSubsTring);
            return package;
        }

        public void Dispose()
        {
            Endpoint = null;
            RPCData = null;
        }

        public void CallSent()
        {
            if (OnSent != null)
                OnSent(this);
            if (PendingChange != null)
                PendingChange.Invoke(this);
        }
    }

    [System.Serializable]
    public class KMultiplay_RelayPacket
    {
        public IPEndPoint Origin;
        public KMultiplay_Packet Packet;
    }

    public class KMultiplay_PendingPacket
    {
        private int MaxPackets;
        public List<KMultiplay_Packet> Pending = new List<KMultiplay_Packet>();
        public List<KMultiplay_Packet> Sent = new List<KMultiplay_Packet>();
        public float Progress
        {
            get
            {
                return MaxPackets / Sent.Count;
            }
        }
        public delegate void ProgressChanged(float progress);
        public ProgressChanged OnProgressChanged;

        public KMultiplay_PendingPacket(List<KMultiplay_Packet> packets)
        {
            Pending = packets;
            foreach(KMultiplay_Packet packet in packets)
            {
                packet.OnSent += OnPacketSent;
            }
        }

        private void OnPacketSent(KMultiplay_Packet packet)
        {
            packet.OnSent -= OnPacketSent;
            Sent.Add(packet);
            Pending.Remove(packet);
        }
    }

    public enum KMultiplay_PendingStatus
    {
        Finished,
        InProgress,

    }

    public enum KMultiplay_PacketType
    {
        SFT=0,
        LFT=1
    }

    public enum KMultiplay_Reliability
    {
        Reliable=0,
        Unreliable=1
    }

    public enum KMultiplay_Target
    {
        Everyone=0,
        Self=1,
        OnlyHost=2,
        OnlyClients=3,
        MasterServer=4,
        Specific=5,
        None=6
    }

    [System.Serializable]
    public class KMultiplay_Endpoint : IDisposable
    {
        public EndPoint Local;
        public IPEndPoint LocalIP;

        public EndPoint Remote;
        public IPEndPoint RemoteIP;

        public EndPoint Relay;
        public IPEndPoint RelayIP;

        public bool IsRemote
        {
            get
            {
                return Remote != null;
            }
        }
        public bool IsRelay
        {
            get
            {
                return Relay != null;
            }
        }

        public KMultiplay_Endpoint(IPAddress ip, int port)
        {
            LocalIP = new IPEndPoint(ip, port);
            Local = LocalIP;
        }

        public KMultiplay_Endpoint(IPEndPoint local)
        {
            Local = local;
            LocalIP = local;
        }

        public KMultiplay_Endpoint(IPEndPoint local, IPEndPoint remote)
        {
            Local = local;
            LocalIP = local;
            Remote = remote;
            RemoteIP = remote;
        }

        public KMultiplay_Endpoint(IPEndPoint local, IPEndPoint remote, IPEndPoint relay)
        {
            Local = local;
            LocalIP = local;
            Remote = remote;
            RemoteIP = remote;
            Relay = relay;
            RelayIP = relay;
        }

        public void Dispose()
        {
            Local = null;
            LocalIP = null;
            Remote = null;
            RemoteIP = null;
            Relay = null;
            RelayIP = null;
        }
    }

    public class KMultiplay_Socket
    {
        private Socket _socket;
        private EndPoint Source = new IPEndPoint(IPAddress.Any, 0);
        private AsyncCallback RecievingCallback = null;
        public Action<string, EndPoint, KMultiplay_Socket> OnRecieved;
        private bool IsFirstTimeSending;
        public IPEndPoint LocalEndpoint;
        public string Descripion;
        public class State : IDisposable
        {
            public byte[] buffer = new byte[KMultiplay_Settings.PacketsPerSecond];

            public void Dispose()
            {
                buffer = null;
            }
        }

        public KMultiplay_Socket(string description)
        {
            Descripion = description;
        }

        public void Start(KMultiplay_Endpoint endPoint, Action<string, EndPoint, KMultiplay_Socket> recieveCallback)
        {
            OnRecieved = recieveCallback;
            _socket = new Socket(endPoint.LocalIP.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            _socket.SendTimeout = 1000;
            _socket.ReceiveTimeout = 1000;
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            try
            {
                _socket.Bind(endPoint.Local);
         
            }
            catch(Exception ex)
            {
                Debug.LogError("KMultiplay - Unable to bind socket '" + Descripion + "' to endpoint: " + endPoint.LocalIP.ToString());
                return;
            }
            LocalEndpoint = (IPEndPoint)_socket.LocalEndPoint;
            Debug.Log("KMultiplay - UDP-Socket '" + Descripion + "' is ready to send/recieve data through endpoint: " + endPoint.LocalIP.ToString());
            Receive();
        }

        public void Close()
        {
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
            }
            catch (Exception ex)
            {
                Debug.LogError("KMultiplay - UDP-Socket Error when closing: \n" + ex.ToString());
            }
        }

        public void SendStringTo(string text, EndPoint to)
        {
            using State state = new State();
            byte[] data = Encoding.UTF8.GetBytes(text);
            _socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, to, (ar) =>
            {
                using (State so = (State)ar.AsyncState) // this might break the send function;
                {
                    int bytes = _socket.EndSend(ar);
                }   
            }, state);
        }

        public void SendBytesTo(byte[] data, EndPoint to)
        {
            using State state = new State();
            _socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, to, (ar) =>
            {
                using (State so = (State)ar.AsyncState) // this might break the send function;
                {
                    int bytes = _socket.EndSend(ar);
                }     
            }, state);

            if (IsFirstTimeSending)
            {
                IsFirstTimeSending = false;
                Receive();
            }
        }

        private void Receive()
        {
            using State state = new State();
            if (!IsFirstTimeSending)
                Debug.Log("KMultiplay - UDP-Socket is listening on local endpoint: " + _socket.LocalEndPoint.ToString());
            _socket.BeginReceiveFrom(state.buffer, 0, KMultiplay_Settings.SocketBuffer, SocketFlags.None, ref Source, RecievingCallback = (ar) =>
            {
                State so = (State)ar.AsyncState;
                int bytes = _socket.EndReceiveFrom(ar, ref Source);
                _socket.BeginReceiveFrom(so.buffer, 0, KMultiplay_Settings.SocketBuffer, SocketFlags.None, ref Source, RecievingCallback, so);
                string message = Encoding.UTF8.GetString(so.buffer, 0, bytes);
                if (OnRecieved != null)
                {
                    OnRecieved(message, Source, this);
                }
            }, state);
        }
    }

    public class PacketAck: KMultiplay_RPC
    {
        public int PacketIndex;
        
        public PacketAck(int pIndex)
        {
            PacketIndex = pIndex;
        }
    }
}