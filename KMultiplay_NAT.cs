using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mono.Nat;
using System;
using System.Threading;
using System.IO;
using KMultiplay;

public static class KMultiplay_NAT
{
    public static List<KMultiplay_NatDevice> Devices = new List<KMultiplay_NatDevice>();
    public static List<KMultiplay_PortMapping> PortMappings = new List<KMultiplay_PortMapping>();
    private static KMultiplay_NatDevice CurrentDeviceIpRequest;
    private static TextWriter Logger;
    public enum KMultiplay_PortMap_Status
    {
        Opened,
        Closed,
        Working,
        Error
    }
    public static string ExternalIP;
    public static bool IsEngaged;

    public static void Start()
    {
        //NatUtility.Logger.Close();
        //Logger = (TextWriter)Debug.unityLogger;
        File.WriteAllText(Application.persistentDataPath + "/NAT_logfile.txt", "");
        NatUtility.Logger =  new StreamWriter(File.OpenWrite(Application.persistentDataPath + "/NAT_logfile.txt"));
        NatUtility.Verbose = true;

        Debug.Log("KMultiplay_NAT - Started");
        NatUtility.DeviceFound += DeviceFound;
        NatUtility.DeviceLost += DeviceLost;
        NatUtility.UnhandledException += UnhandledException;
        //NatUtility.StartDiscovery();
        Kmultiplay_OpenNAT.Start();
    }

    public static void Discover()
    {
        NatUtility.StartDiscovery();
    }

    public static void Kill()
    {
        NatUtility.Logger.Close();
        while (PortMappings.Count > 0)
        {
            ClosePort(PortMappings[0].Local);
        }
        NatUtility.DeviceFound -= DeviceFound;
        NatUtility.DeviceLost -= DeviceLost;
        NatUtility.UnhandledException -= UnhandledException;
       
    }

    public static KMultiplay_PortMapping OpenPort(int localPort, int externalPort)
    {
        Kmultiplay_OpenNAT.OpenPort(localPort);
        return null;

        KMultiplay_PortMapping portMapping = new KMultiplay_PortMapping(localPort, externalPort);
        PortMappings.Add(portMapping);
        if(Devices.Count > 0)
        {
            portMapping.Status = KMultiplay_PortMap_Status.Working;
            foreach (KMultiplay_NatDevice device in Devices)
            {
                portMapping.Open(device.Device);
            }
        }else
        {
            Debug.Log("Failed to open port " + localPort.ToString() + " - no devices found");
            portMapping.Status = KMultiplay_PortMap_Status.Closed;
        }
        return portMapping;
    }

    public static void ClosePort(int localPort)
    {
        KMultiplay_PortMapping portMapping = GetMapping(localPort);
        PortMappings.Remove(portMapping);
        foreach (KMultiplay_NatDevice device in Devices)
        {
            portMapping.Close(device.Device);
        }
    }

    public static KMultiplay_PortMapping GetMapping(int port)
    {
        foreach(KMultiplay_PortMapping portMapping in PortMappings)
        {
            if (portMapping.Local == port)
                return portMapping;
        }
        return null;
    }

    private static void DeviceFound(object sender, DeviceEventArgs args)
    {
        IsEngaged = true;
        Debug.Log("Device found!");
        INatDevice device = args.Device;
        KMultiplay_NatDevice natDevice = new KMultiplay_NatDevice(device);
        Devices.Add(natDevice);
        Debug.Log("Device added to list " + Devices.Count + " devices in total found");
        foreach(KMultiplay_PortMapping portMapping in PortMappings)
        {
            portMapping.Open(device);
        }
        FindPublicAddress(natDevice);
        //KMultiplay_Callbacks.CallNATEngaged(true);
    }

    private static void DeviceLost(object sender, DeviceEventArgs args)
    {
        Debug.Log("Device Lost!");
        INatDevice device = args.Device;
        foreach(KMultiplay_NatDevice natDevice in Devices)
        {
            if (natDevice.Device == device)
                Devices.Remove(natDevice);
        }
    }

    private static void UnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        Debug.Log("UnhandledException!");
        Debug.LogError(args.ExceptionObject.ToString());
    }

    private static void FindPublicAddress(KMultiplay_NatDevice device)
    {
        Debug.Log("Begin finding a public address!");
        CurrentDeviceIpRequest = device;
        try
        {
            device.Device.BeginGetExternalIP(new AsyncCallback(GotExternalIP), null);//ar => 
        }
        catch (Exception exn)
        {
            Debug.Log(exn.ToString());
        }
        /*

            try
            {
                device.ExternalIP = device.Device.EndGetExternalIP(ar);
                Debug.Log("Found external ip: " + device.ExternalIP);
            }
            catch (Exception exn)
            {
                Debug.Log(exn.ToString());
            }
        }, null);
        */
    }

    private static void GotExternalIP(IAsyncResult ar)
    {
        try
        {
            CurrentDeviceIpRequest.ExternalIP = CurrentDeviceIpRequest.Device.EndGetExternalIP(ar);
            Debug.Log("Found external ip: " + CurrentDeviceIpRequest.ExternalIP.MapToIPv4());
        }
        catch (Exception exn)
        {
            Debug.Log(exn.ToString());
        }
    }

    public static string GetReadyExternalIP()
    {
        foreach(KMultiplay_NatDevice device in Devices)
        {
            if(device.ExternalIP != null)
            {
                return device.ExternalIP.ToString();
            }
        }
        return string.Empty;
    }

    public static void ListPortMappings()
    {
        if(Devices.Count == 0)
        {
            Debug.Log("No devices found to read port mappings from");
            return;
        }
        CurrentDeviceIpRequest = Devices[0];
        Devices[0].Device.BeginGetAllMappings(GotPortMaps, null);
    }

    public static string GetActivePortMappingsInfo()
    {
        if (Devices.Count == 0)
        {
            Debug.Log("No devices found to read port mappings from");
            return string.Empty;
        }
        string ret = string.Empty;
        foreach(KMultiplay_PortMapping portMapping in PortMappings)
        {
            ret += portMapping.Local.ToString() + " <> " + portMapping.External.ToString() + " - " + portMapping.Status.ToString() + "\n";
        }
        return ret;
    }

    private static void GotPortMaps(IAsyncResult ar)
    {
        try
        {
            Mapping[] maps = CurrentDeviceIpRequest.Device.EndGetAllMappings(ar);
            Debug.Log("Found: " + maps.Length + " port maps");
            foreach(Mapping map in maps)
            {
                Debug.Log("forwarded local port " + map.PrivatePort + " to external port " + map.PublicPort);
            }
        }
        catch (Exception exn)
        {
            Debug.Log(exn.ToString());
        }
    }

    public class KMultiplay_NatDevice
    {
        public System.Net.IPAddress ExternalIP;
        public INatDevice Device;

        public KMultiplay_NatDevice(INatDevice device)
        {
            Device = device;
        }
    }

    public class KMultiplay_PortMapping
    {
        public int Local;
        public int External;
        public KMultiplay_PortMap_Status Status;
        public Mapping PortForwarding;

        public KMultiplay_PortMapping(int local, int external)
        {
            Local = local;
            External = external;
            Status = KMultiplay_PortMap_Status.Closed;
            PortForwarding = new Mapping(Protocol.Udp, Local, External);
        }

        public void Open(INatDevice device)
        {
            Status = KMultiplay_PortMap_Status.Working;
            device.BeginCreatePortMap(PortForwarding, ar => {

                try
                {
                    device.EndCreatePortMap(ar);
                    Status = KMultiplay_PortMap_Status.Opened;
                    Debug.Log("Opened port " + Local);
                }
                catch (MappingException exn)
                {
                    if (exn.ErrorCode == 718)
                    {
                        Status = KMultiplay_PortMap_Status.Opened;
                        Debug.Log("#Opened port " + Local);
                    }
                    else
                    {
                        Status = KMultiplay_PortMap_Status.Error;
                        Debug.Log(exn.ToString());
                    }
                }
                catch (Exception exn)
                {
                    Status = KMultiplay_PortMap_Status.Error;
                    Debug.Log(exn.ToString());
                }

            }, null);
        }

        public void Close(INatDevice device)
        {
            Status = KMultiplay_PortMap_Status.Closed;
            try
            {
                device.DeletePortMap(new Mapping(Protocol.Udp, Local, External));
            }
            catch (Exception exn)
            {
                Debug.Log(exn.ToString());
            }
        }
    }
}
