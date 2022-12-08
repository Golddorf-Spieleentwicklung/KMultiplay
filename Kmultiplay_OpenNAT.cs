using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Open.Nat;

public static class Kmultiplay_OpenNAT
{
    public static void Start()
    {
        DiscoverThread();
    }

    public static async void DiscoverThread()
    {
        var discoverer = new NatDiscoverer();
        var device = await discoverer.DiscoverDeviceAsync();
        Debug.Log("Kmultiplay_OpenNAT - Device Found! " + device.HostEndPoint.ToString());
        var ip = await device.GetExternalIPAsync();
        Debug.Log(string.Format("The external IP Address is: {0} ", ip));
    }

    public static async void OpenPort(int port)
    {
        var discoverer = new NatDiscoverer();

        // using SSDP protocol, it discovers NAT device.
        var device = await discoverer.DiscoverDeviceAsync();

        var ip = await device.GetExternalIPAsync();
        // display the NAT's IP address
        Debug.Log(string.Format("Kmultiplay_OpenNAT - The external IP Address is: {0} ", ip));

        // create a new mapping in the router [external_ip:1702 -> host_machine:1602]
        await device.CreatePortMapAsync(new Mapping(Protocol.Udp, port, port, "UDP Traffic"));
        Debug.Log(string.Format("Kmultiplay_OpenNAT - Opend port", port));
    }
}
