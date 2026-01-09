using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnitySerializationBridge.Utils;
using Object = UnityEngine.Object;

namespace UnitySerializationBridge.Core.JSON;

internal class UniversalUnityReferenceValueConverter : JsonConverter
{
    const string objectHashRef = "$hash";

    // Don't try to wrap primitives, strings, or enums in reference containers
    public override bool CanConvert(Type objectType)
    {
        if (typeof(Object).IsAssignableFrom(objectType)) return true;
        if (objectType.IsStandardCollection() && objectType != typeof(string)) return true;
        return false;
    }
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        // Handle Single Unity Object
        if (value is Object unityObj)
        {
            new JObject()
            {
                { objectHashRef, unityObj.GetInstanceID() }
            }.WriteTo(writer);
        }
        // Handle Collections (Arrays, Lists, Nested Lists)
        else if (value.GetType().IsStandardCollection())
        {
            writer.WriteStartArray();
            foreach (var item in (IEnumerable)value)
            {
                // Collections can be null
                if (item == null)
                {
                    writer.WriteNull();
                    continue;
                }

                if (item is Object itemUnityObj)
                {
                    writer.WriteValue(itemUnityObj ? itemUnityObj.GetInstanceID() : 0);
                }
                else if (item.GetType().IsStandardCollection())
                {
                    // If it's a nested collection
                    // Recursion
                    WriteJson(writer, item, serializer);
                }
                // Ignore completely this item
            }
            writer.WriteEndArray();
        }
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;

        // Load the token
        JToken token = JToken.Load(reader);

        return ReadRecursively(token, objectType);
    }

    private object ReadRecursively(JToken token, Type targetType)
    {
        // If it's an array
        if (token.Type == JTokenType.Array)
        {
            var getCollection = DeserializeCollection((JArray)token, targetType);
            return getCollection;
        }

        // Either a raw integer ID from a compact array or a JObject with hash_ref
        int instanceId = 0;
        bool foundId = false;

        if (token.Type == JTokenType.Integer)
        {
            instanceId = token.ToObject<int>();
            foundId = true;
        }
        else if (token.Type == JTokenType.Object && token is JObject jo)
        {
            if (jo.ContainsKey(objectHashRef))
            {
                instanceId = jo[objectHashRef].ToObject<int>();
                foundId = true;
            }
        }

        if (foundId)
        {
            // Get Object ID
            var unityObject = Object.FindObjectFromInstanceID(instanceId);
            if (!unityObject) return null;
            var unityType = unityObject.GetType();
            // If a Component is detected, then this must be referenced directly, not instantiated
            if (unityType.IsUnityComponentType())
            {
                return unityObject;
            }

            // Try to get a constructor of same type to clone
            if (unityType.TryGetSelfActivator(out var constructor))
                return constructor(unityObject);
            // Instantiate the object
            return Object.Instantiate(unityObject);
        }

        return null;
    }

    private object DeserializeCollection(JArray jArray, Type collectionType)
    {
        // Determine the type of element inside this collection
        Type elementType = collectionType.GetTypeFromArray(1); // Check just one layer (List<List<Object>> -> List<Object>)

        // Create a generic List<ElementType> to hold values temporarily
        IList results = (IList)typeof(List<>).GetGenericConstructor(elementType)();

        // 3. Iterate over the JSON array
        foreach (JToken childToken in jArray)
        {
            // RECURSION: Call ReadRecursively for every child. 
            object parsedItem = ReadRecursively(childToken, elementType);
            results.Add(parsedItem);
        }

        // Convert back to target type (Array or List)
        if (collectionType.IsArray)
        {
            Array finalArray = Array.CreateInstance(elementType, results.Count);
            for (int i = 0; i < results.Count; i++) finalArray.SetValue(results[i], i);
            return finalArray;
        }

        return results;
    }
}