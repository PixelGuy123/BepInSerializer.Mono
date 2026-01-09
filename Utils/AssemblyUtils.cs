using System.Reflection;
using System.IO;
using UnitySerializationBridge.Core.Serialization;
using System.Runtime.CompilerServices;
using System;
using UnityEngine;
using System.Collections.Generic;

namespace UnitySerializationBridge.Utils;

internal static class AssemblyUtils
{
    internal static ConditionalWeakTable<Assembly, StrongBox<bool>> TypeIsManagedCache;
    internal static ConditionalWeakTable<Assembly, StrongBox<bool>> TypeIsUnityManagedCache;

    public static bool IsFromGameAssemblies(this Type type)
    {
        // Obviously the handler shouldn't be accounted at all
        if (type == typeof(SerializationHandler) || type == typeof(ComponentMap))
            return true;

        var assembly = type.Assembly;

        if (typeof(BepInEx.BaseUnityPlugin).IsAssignableFrom(type)) // Never a plugin
            return true;

        return assembly.IsGameAssembly();
    }

    public static bool IsGameAssembly(this Assembly assembly)
    {
        // if the assembly is already known, return the value
        if (TypeIsManagedCache.TryGetValue(assembly, out var box))
            return box.Value;

        // Cache the managed part if it is not detected
        bool isManaged = assembly.Location.EndsWith($"Managed{Path.DirectorySeparatorChar}{assembly.GetName().Name}.dll");
        TypeIsManagedCache.Add(assembly, new(isManaged));

        return isManaged;
    }

    public static bool IsUnityAssembly(this Assembly assembly)
    {
        // if the assembly is already known, return the value
        if (TypeIsUnityManagedCache.TryGetValue(assembly, out var box))
            return box.Value;

        // Cache the managed part if it is not detected
        bool isManaged = assembly.Location.EndsWith($"Managed{Path.DirectorySeparatorChar}{assembly.GetName().Name}.dll") && Path.GetFileName(assembly.Location).StartsWith("Unity");
        TypeIsUnityManagedCache.Add(assembly, new(isManaged));

        return isManaged;
    }

    public static bool IsUnityExclusive(this Type type)
    {
        Type objType = typeof(UnityEngine.Object);
        // Check the type
        return objType.IsAssignableFrom(type.GetTypeFromArray());
    }

    public static bool IsGameAssemblyType(this Type type)
    {
        // If the type is from System itself, then return false
        if (type.IsPrimitive || type == typeof(string) || type.IsEnum) return false;

        // If these aren't classes, they are nothing
        if (!type.IsClass && !type.IsValueType) return false;

        // Check the type itself IF it is the type the one from assemblies; otherwise, go to collection check
        if (!type.IsStandardCollection() && type.IsFromGameAssemblies()) return true;

        // Check generic arguments for collections
        return type.GetTypeFromArray().IsFromGameAssemblies();
    }

    public static bool IsUnityComponentType(this Type type)
    {
        var elementType = type.GetTypeFromArray(); // Update the type used
        // Check the type itself IF it is the type the one from assemblies; otherwise, go to collection check
        return !type.IsStandardCollection() && (typeof(GameObject) == elementType || typeof(Component).IsAssignableFrom(elementType));
    }

    // Expect the most basic collection types to be checked, not IEnumerable in general
    public static bool IsStandardCollection(this Type t) => typeof(Array).IsAssignableFrom(t) || typeof(List<>).IsAssignableFrom(t);

    public static Type GetTypeFromArray(this Type collectionType, int layersToCheck = -1) =>
        collectionType.GetTypeFromArray(layersToCheck, 0);
    private static Type GetTypeFromArray(this Type collectionType, int layersToCheck, int currentLayer)
    {
        if ((layersToCheck > 0 && currentLayer >= layersToCheck) || !collectionType.IsStandardCollection())
            return collectionType;


        Type elementType;
        // Must be a list
        if (collectionType.IsGenericType)
        {
            elementType = collectionType.GetGenericArguments()[0];
            return elementType.GetTypeFromArray(layersToCheck, currentLayer + 1); // Recursive call
        }
        if (!collectionType.IsArray) return collectionType; // Default to its own type

        elementType = collectionType.GetElementType();
        return elementType.GetTypeFromArray(layersToCheck, currentLayer + 1);
    }
}