using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnitySerializationBridge.Core;

namespace UnitySerializationBridge.Patches;

[HarmonyPatch]
static class InstantiatePatch
{
    [HarmonyTargetMethods]
    static IEnumerable<MethodInfo> GetInstantiationMethods()
    {
        // Fetch all methods named "Instantiate"
        var methods = AccessTools.GetDeclaredMethods(typeof(Object))
            .Where(m => m.Name == "Instantiate");

        foreach (var method in methods)
        {
            if (method.IsGenericMethodDefinition)
            {
                // Possible generic definitions
                yield return method.MakeGenericMethod(typeof(GameObject));
                yield return method.MakeGenericMethod(typeof(MonoBehaviour));
                yield return method.MakeGenericMethod(typeof(Object));
            }
            else
            {
                yield return method;
            }
        }
    }

    [HarmonyPrefix]
    static void AttemptToAddSerializationHandler(object __0) // first argument should always be the object itself
    {
        if (__0 is not MonoBehaviour comp) return;

        BridgeManager.Instance.Log("MonoBehaviour: " + comp.name);
        if (!comp.GetComponent<SerializationHandler>())
        {
            BridgeManager.Instance.Log("Added handler!");
            comp.gameObject.AddComponent<SerializationHandler>();
        }
    }
}