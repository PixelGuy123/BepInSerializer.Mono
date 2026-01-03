using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnitySerializationBridge.Core;

// Holds metadata about which Field in which Component needs bridging
internal struct BridgeTarget
{
    private readonly static Dictionary<FieldInfo, Type> _cachedElementType = [];

    public Type ComponentType;
    public FieldInfo[] Path;
    public bool isCollection;

    public readonly Type GetCollectionElementType()
    {
        var fieldInfo = Path[Path.Length - 1];
        if (_cachedElementType.TryGetValue(fieldInfo, out var type))
            return type;

        var fieldType = fieldInfo.FieldType;
        // Fallback calculation
        if (fieldType.IsArray)
        {
            var elementType = fieldType.GetElementType();
            _cachedElementType.Add(fieldInfo, elementType);
            return elementType;
        }
        // Generic type
        var genericType = fieldType.GetGenericArguments()[0];
        _cachedElementType.Add(fieldInfo, genericType);
        return genericType;
    }
}