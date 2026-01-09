using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnitySerializationBridge.Utils;

namespace UnitySerializationBridge.Core.JSON;

internal class UnityContractResolver : DefaultContractResolver
{
    // Cache to know what properties to look for after the first lookup
    internal static ConditionalWeakTable<Type, IList<JsonProperty>> propsCache;
    private static readonly UniversalUnityReferenceValueConverter UnityValueConverter = new();

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        // Absolute Ignorance Filters
        if (member.IsDefined(typeof(NonSerializedAttribute)))
            return LogSkipped(member.Name, "marked as NonSerialized");

        bool isUnityStruct = member is PropertyInfo p && p.PropertyType.IsValueType && member.DeclaringType.Assembly.IsUnityAssembly();
        bool isForbiddenProperty = (member.MemberType == MemberTypes.Property || member.IsFieldABackingField()) && !isUnityStruct;

        if (isForbiddenProperty)
            return LogSkipped(member.Name, "a property/backing field");

        // Initialize Property
        JsonProperty property = base.CreateProperty(member, memberSerialization);
        bool isUnityExclusive = property.PropertyType.IsUnityExclusive();
        Debug.Log($"Value in question: {property.PropertyType}");
        bool hasSerializeReference = property.AttributeProvider.HasAttribute<SerializeReference>();

        // Handle SerializeReference for Non-Unity types (Standard JSON Referencing)
        if (hasSerializeReference && !isUnityExclusive)
        {
            property.IsReference = property.ItemIsReference = true;
            return LogAssigned(property.PropertyName, "Standard Referencing", property);
        }

        // Apply the UnityValueConverter if it's a Unity type, UNLESS:
        // It's a Component being serialized inside another Component (let Unity handle that natively)
        if (isUnityExclusive)
        {
            Debug.Log($"[{property.PropertyName}] is a exclusive type.");
            bool isInsideComponent = typeof(Component).IsAssignableFrom(property.DeclaringType);
            Debug.Log($"[{property.PropertyName}] is inside component: {isInsideComponent} ({property.DeclaringType})");
            // If it has [SerializeReference], or isn't a nested Component relationship, use the converter
            if (hasSerializeReference || !isInsideComponent)
            {
                property.Converter = UnityValueConverter;
                return LogAssigned(property.PropertyName, "UnityValueConverter", property);
            }

            return LogAssigned(property.PropertyName, "Native Unity conversion", property);
        }

        return LogAssigned(property.PropertyName, "Standard serialization", property);

        // Helpers to clean up the main method
        static JsonProperty LogSkipped(string name, string reason)
        {
            if (BridgeManager.enableDebugLogs.Value) Debug.Log($"[{name}] is {reason}. Skipping.");
            return null;
        }

        static JsonProperty LogAssigned(string name, string strategy, JsonProperty prop)
        {
            if (BridgeManager.enableDebugLogs.Value) Debug.Log($"[{name}] Assigned Strategy: {strategy}.");
            return prop;
        }
    }


    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        // Get standard properties
        var props = GetPropertiesFromCache(type, memberSerialization, out bool usedCache);
        if (usedCache) // If it used caching, all of these properties are already properly defined for the right use-case
            return props;

        // Prepare deduplication set
        var addedPropertyNames = new HashSet<string>(StringComparer.Ordinal);
        int baseCount = props.Count;
        for (int i = 0; i < baseCount; i++)
            addedPropertyNames.Add(GetUniquePropertyName(props[i]));

        Type currentType = type;
        while (currentType != null && currentType != typeof(object))
        {
            var fields = AccessTools.GetDeclaredFields(currentType);

            // Make private fields public if possible
            foreach (var field in fields)
            {
                if (field.IsStatic || field.IsPublic) continue;

                // We only care about private fields marked for Unity serialization
                bool isSerializeField = field.IsDefined(typeof(SerializeField), false);
                bool isSerializeReference = field.IsDefined(typeof(SerializeReference), false);

                if (isSerializeField || isSerializeReference)
                {
                    // If this is a component and it is attempting to serialize another component, Unity can already do that; the serializer ignores this
                    if (field.DeclaringType.IsUnityComponentType() && field.FieldType.IsUnityComponentType())
                    {
                        if (BridgeManager.enableDebugLogs.Value) Debug.Log($"[{field.Name}] field has been detected as serialized private and REMOVED!");
                        continue;
                    }

                    // Create property (this calls our overridden CreateProperty above)
                    JsonProperty jsonProp = CreateProperty(field, memberSerialization);
                    if (jsonProp == null)
                    {
                        if (BridgeManager.enableDebugLogs.Value) Debug.Log($"[{field.Name}] field has been detected as serialized private and REMOVED!");
                        continue;
                    }

                    if (BridgeManager.enableDebugLogs.Value) Debug.Log($"[{field.Name}] field has been detected as serialized private and INCLUDED!");

                    // Force visibility for private fields
                    jsonProp.Readable = true;
                    jsonProp.Writable = true;

                    if (addedPropertyNames.Add(GetUniquePropertyName(jsonProp)))
                    {
                        props.Add(jsonProp);
                    }
                }
            }
            currentType = currentType.BaseType;
        }

        return props;

        static string GetUniquePropertyName(JsonProperty prop) => $"{prop.DeclaringType.FullName}.{prop.PropertyName}";
    }


    // Private field helpers

    private IList<JsonProperty> GetPropertiesFromCache(Type type, MemberSerialization memberSerialization, out bool usedCache)
    {
        if (propsCache != null && propsCache.TryGetValue(type, out var props))
        {
            usedCache = true;
            return props;
        }

        props = base.CreateProperties(type, memberSerialization);

        // Filter out any property declared in a Unity assembly
        for (int i = props.Count - 1; i >= 0; i--)
        {
            // If this is a component and it's trying to serialize one, remove it from here
            if (props[i].DeclaringType.IsUnityComponentType() && props[i].PropertyType.IsUnityComponentType())
            {
                if (BridgeManager.enableDebugLogs.Value) Debug.Log($"[{props[i].PropertyName}] has been detected and REMOVED from the properties.");
                props.RemoveAt(i);
                continue;
            }

            if (BridgeManager.enableDebugLogs.Value) Debug.Log($"[{props[i].PropertyName}] has been INCLUDED.");
        }
        propsCache.Add(type, props);

        usedCache = false;
        return props;
    }
}