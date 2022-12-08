using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;

public static class KMultiplay_Serializer
{
    public static string SerializeRPC(object RPC)
    {
        Type rpcType = RPC.GetType();
        FieldInfo[] fields = rpcType.GetFields();
        foreach (FieldInfo field in fields)
        {
            if (field.IsStatic) // ignore static values
                continue;
            if (!field.IsPublic) // ignore private values
                continue;
            List<CustomAttributeData> customAttributes = new List<CustomAttributeData>(field.CustomAttributes);
            if (customAttributes.Count == 0) // ignore fields without attributes
                continue;
            if (!ContainsValidAttribute(customAttributes)) // ignore fields which are not marked with the KRPC_Property attribute
                continue;
            string attributes = "";
            foreach (CustomAttributeData attribute in customAttributes)
                attributes += attribute.ToString() + ", ";

            bool isRecursive = field.GetValue(RPC).GetType().IsClass;

            Debug.Log("Found field on RPC: " + field.Name + " value:" + field.GetValue(RPC).ToString()+ " attributes: " + attributes + " isRecursive: " + isRecursive);
        }
        return JsonUtility.ToJson(RPC);
    }

    private static bool ContainsValidAttribute(List<CustomAttributeData> customAttributes)
    {
        foreach (CustomAttributeData attribute in customAttributes)
        {
            if (attribute.ToString().Contains("KRPC_Property"))
                return true;
        }
        return false;
    }
}