using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnitySerializationBridge.Patches.Serialization;

[HarmonyPatch]
static partial class GameObjectMapper
{
    static GameObjectMapper()
    {
        SceneManager.sceneUnloaded += (_) =>
        {
            Prune(); // Clean up all dead references
        };
    }

    // Called periodically to remove dead references
    public static void Prune()
    {
        var keysToRemove = new List<GameObject>(); // If the key is null, remove it from the 

        // Get the null references
        foreach (var kvp in ContainerMap)
        {
            if (!kvp.Key)
                keysToRemove.Add(kvp.Key);
        }

        // Remove them
        for (int i = 0; i < keysToRemove.Count; i++)
            ContainerMap.Remove(keysToRemove[i]);
    }

    // Basic class to handle Child data
    internal class ChildGameObject(GameObject go, bool canBeSerialized)
    {
        public GameObject Go = go;
        public bool CanBeSerialized = canBeSerialized;
    }
    // Stores the relationship: Key = Parent, Value = (Child, IsWorthInteractingWithSerializationHandler)
    public static Dictionary<GameObject, ChildGameObject> ContainerMap = [];
}