using System.Collections;
using System.Collections.Generic;
using BepInSerializer.Core.Serialization.Converters.Models;

namespace BepInSerializer.Core.Serialization.Converters;

// DictionaryConverter (internal)
internal class DictionaryConverter : FieldConverter
{
    public override bool CanConvert(FieldContext context)
    {
        var type = context.ValueType;
        // Make sure to get the generic definition first
        if (type.IsGenericType)
            type = type.GetGenericTypeDefinition();

        // Then, check specifically for List<>
        return typeof(Dictionary<,>) == type;
    }

    public override object Convert(FieldContext context)
    {
        if (context.OriginalValue is not IDictionary originalDictionary) return null;
        // Make a new list (object)
        if (TryConstructNewObject(context, out var newObject))
        {
            // Failsafe to be an actual list
            if (newObject is not IDictionary newDictionary) return null;

            // Generic argument from Dictionary<TKey, TValue>
            var genericArgs = context.ValueType.GetGenericArguments();
            var genericKeyType = genericArgs[0];
            var genericValueType = genericArgs[1];

            // Copy the original items to this new list, by using ReConvert
            foreach (DictionaryEntry kvp in originalDictionary)
            {
                // Convert key
                var newKey = ReConvert(FieldContext.CreateRemoteContext(context, kvp.Key, genericKeyType));
                // If the key is null, don't add it back to the dictionary
                if (newKey == null)
                {
                    if (BridgeManager.enableDebugLogs.Value) BridgeManager.logger.LogWarning($"[DictionaryConverter] Dictionary Key from ('{originalDictionary}') is null. Removing entry.");
                    continue;
                }
                // Convert value
                var newValue = ReConvert(FieldContext.CreateRemoteContext(context, kvp.Value, genericValueType));

                // Make a remote context for each item and copy back to this new list
                newDictionary.Add(newKey, newValue);
            }
            return newDictionary;
        }

        // If no list has been given, return null
        return null;
    }
}