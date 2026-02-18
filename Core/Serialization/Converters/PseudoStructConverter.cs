using System;
using System.Collections;
using BepInSerializer.Core.Serialization.Converters.Models;

namespace BepInSerializer.Core.Serialization.Converters;

// PseudoStructConverter (internal)
// Convert Unity types (properties) that essentially act as structs, but are reference types with properties instead of fields
internal sealed class PseudoStructConverter : FieldConverter
{
    public override bool CanConvert(FieldContext context)
    {
        var type = context.ValueType;

        // Must be a class (AnimationCurve/Gradient are classes)
        // Must NOT be a value type (structs like Vector3 should be handled elsewhere)
        if (!type.IsClass || type.IsValueType) return false;

        // Exclude Delegates (This blocks System.Action, Func, etc.)
        if (typeof(Delegate).IsAssignableFrom(type)) return false;

        // Exclude Collections (This blocks MemoryStream, List, etc.)
        if (typeof(IEnumerable).IsAssignableFrom(type)) return false;

        // Must be from a Unity Assembly (using the fixed helper above)
        if (!IsUnityAssembly(type)) return false;

        // Namespace check (it needs to be from UnityEngine somewhere)
        if (type.Namespace == null || !type.Namespace.StartsWith("UnityEngine")) return false;
        return true;
    }

    public override object Convert(FieldContext context)
    {
        // If the field has SerializeReference, nothing needs to change; the Serializer can copy the field
        if (context.ContainsSerializeReference)
            return context.OriginalValue;

        // Attempt to create object through a parameterless constructor
        if (TryConstructNewObject(context, out var newConvert))
        {
            // Just like in ClassConverter, return the new object
            if (context.OriginalValue == null)
                return newConvert;
            // Go through each field to convert them as well
            ManagePropertiesFromType(context, newConvert.GetType(), (newContext, setValue) =>
            {
                // Create new context for the field
                setValue(newConvert, ReConvert(newContext));
            });

            return newConvert;
        }

        // If construction failed, just default to the same old value
        return context.OriginalValue;
    }
}