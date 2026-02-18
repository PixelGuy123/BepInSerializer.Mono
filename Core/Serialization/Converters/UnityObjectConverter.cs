using BepInSerializer.Core.Serialization.Converters.Models;
using UnityEngine;

namespace BepInSerializer.Core.Serialization.Converters;

// UnityObjectConverter (internal)
// Convert Unity Objects according to reference rules
internal sealed class UnityObjectConverter : FieldConverter
{
    public override bool CanConvert(FieldContext context)
    {
        var type = context.ValueType;
        return typeof(Object).IsAssignableFrom(type);
    }

    // Tries to get the mapped Unity Object, or return null if no map was found (to prevent referencing a wrong object)
    public override object Convert(FieldContext context)
    {
        // If it's null, just return null
        if (context.OriginalValue == null)
            return null;

        // Edge case for textures
        if (context.OriginalValue is Texture tex && !tex.isReadable)
            return context.OriginalValue;

        // If the object can be copied, prioritize that
        if (TryCopyNewObject(context, out var newObject))
            return newObject;

        // Otherwise, try to get the mapped object from the hierarchy (must be a component)
        return TryGetMappedUnityObject(context, out var mappedObject) ? mappedObject : context.OriginalValue;
    }
}