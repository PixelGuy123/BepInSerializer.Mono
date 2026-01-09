using HarmonyLib;
using UnityEngine;


namespace UnitySerializationBridge.Patches.Serialization;

[HarmonyPatch(typeof(Object))]
static class GameObjectMapper_DestroyCheck // Important to clean up cache
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(Object.Destroy), [typeof(Object)])]
    static void RemoveCache(Object __0) // To make sure the component is properly serialized when added
    {
        // If the type is a GameObject, then we don't check for component below
        if (__0 is GameObject go)
        {
            // Remove the entire tree from below (literally cutting down the branch)
            while (GameObjectMapper.ContainerMap.TryGetValue(go, out var childPair))
            {
                // Removes the parent from container
                GameObjectMapper.ContainerMap.Remove(go);

                // Set the reference to the child and see if the root goes deeper on the next iteration
                var child = childPair.Go;
                if (child)
                    go = child;
            }

            // Remove all the components too (very expensive)
            var components = go.GetComponentsInChildren<Component>();
            for (int i = 0; i < components.Length; i++)
                ComponentMapper.RemoveComponentFromMap(components[i]);

            ComponentMapper.Prune();
            return;
        }

        // Just remove this single component
        if (__0 is Component component)
        {
            ComponentMapper.RemoveComponentFromMap(component);
            ComponentMapper.Prune();
        }
    }
}