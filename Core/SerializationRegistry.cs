using System.Collections.Generic;
using UnitySerializationBridge.Interfaces;
using System;
using HarmonyLib;
using System.Reflection;
using System.Collections;

namespace UnitySerializationBridge.Core;

internal static class SerializationRegistry
{
    internal readonly static List<BridgeTarget> RegisteredTargets = [];
    internal readonly static HashSet<Type> componentTypesToAddBridgeSerializer = [];

    public static void Register(Type componentType)
    {
        BridgeManager.Instance.Log($"===== Registering Root ({componentType.FullName}) =====");

        // Start recursive scan
        // Path is initially empty because we are at the component root
        ScanRecursively(componentType, componentType, []);
    }

    private static void ScanRecursively(Type rootComponentType, Type currentScanType, List<FieldInfo> currentPath)
    {
        // No field can't go that deep, right?
        if (currentPath.Count > 150)
            throw new OverflowException($"Circular dependency detected on component ({rootComponentType.FullName})!");

        var fields = AccessTools.GetDeclaredFields(currentScanType);

        foreach (var field in fields)
        {
            // Static is irrelevant
            if (field.IsStatic) continue;

            Type fieldType = field.FieldType;
            Type elementType = null;
            bool isCollection = false;

            // Check if this is an T[] or List<T>
            if (fieldType.IsArray)
            {
                elementType = fieldType.GetElementType();
                isCollection = true;
            }
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                elementType = fieldType.GetGenericArguments()[0];
                isCollection = true;
            }
            else if (fieldType is IEnumerable) // if it is not the above listed types, then this is an useless IEnumerable to be skipped
                continue;

            // Get the right type to look for
            Type typeToCheck = isCollection ? elementType : fieldType;

            // BridgeManager.Instance.Log($"Checking for: {typeToCheck.Name}");
            // Should be serializable
            if (typeof(IAutoSerializable).IsAssignableFrom(typeToCheck))
            {
                FieldInfo[] fullPath = [.. currentPath, field];
                RegisteredTargets.Add(new BridgeTarget
                {
                    ComponentType = rootComponentType,
                    Path = fullPath,
                    isCollection = isCollection
                });

                if (BridgeManager.Instance.enableDebugLogs.Value)
                {
                    string debugSuffix = isCollection ? $"[] of {elementType.Name}" : fieldType.Name;
                    BridgeManager.Instance.Log($"Registered: {rootComponentType.Name}.{field.Name} -> {debugSuffix}");
                }
                // Recursion
                if (!isCollection && IsTraversable(typeToCheck))
                    ScanRecursively(rootComponentType, fieldType, [.. currentPath, field]); // New field added
            }
        }
    }

    private static bool IsTraversable(Type type)
    {
        // Filter out primitives, they aren't classes
        if (type.IsPrimitive || type == typeof(string) || type.IsEnum) return false;
        // Deriveds from Object are already serializable
        if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return false;

        // Only traverse custom classes
        return type.IsClass || type.IsValueType;
    }
}