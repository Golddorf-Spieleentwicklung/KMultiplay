using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KMultiplay.ProcedureCalls;
using System;
using KMultiplay.Transport;
using KMultiplay.Connections;
using System.Net;

public class KMultiplay_Interface : MonoBehaviour
{
    private void Start()
    {
        CustomRPC rpc = new CustomRPC();
        rpc.Send(rpc, KMultiplay_Target.Everyone);
    }
}


public class KMultiplay_RPC : IDisposable
{
    public string CoreName;
    public string CoreData;
    public KMultiplay_Target Target;

    public void Dispose()
    {
        CoreData = null;
        CoreName = null;
        Target = KMultiplay_Target.None;
    }

    public void Send(object rpcObject, KMultiplay_Target target)
    {
        CoreName = rpcObject.GetType().Name;
        CoreData = JsonUtility.ToJson(rpcObject);
        KMultiplay_Connections.SendRPC(this);
    }

    public void Send(object rpcObject, EndPoint target)
    {
        CoreName = rpcObject.GetType().Name;
        CoreData = JsonUtility.ToJson(rpcObject);
        KMultiplay_Transport.SendData(this, target);
    }

    public void Send(object rpcObject, KMultiplay_Endpoint endpoint)
    {
        Target = KMultiplay_Target.Specific;
        CoreName = rpcObject.GetType().Name;
        CoreData = JsonUtility.ToJson(rpcObject);
        KMultiplay_Connections.SendRPC(this, endpoint);
    }

    public string Serialize()
    {
        return JsonUtility.ToJson(this);
    }
}


public class CustomRPC : KMultiplay_RPC
{
    public float SendFloat;

    public CustomRPC()
    {
        SendFloat = 0.123456f;
    }
}
