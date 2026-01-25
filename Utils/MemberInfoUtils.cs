using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace BepInSerializer.Utils;

internal static class MemberInfoUtils
{
    // Check if the field is a property
    public static bool IsFieldABackingField(this FieldInfo field) => field.IsDefined(typeof(CompilerGeneratedAttribute), false) || field.Name.Contains("k__BackingField");
    // If the Property is static
    public static bool IsStatic(this PropertyInfo property)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));

        // Check the getter method (if it exists)
        var getter = property.GetGetMethod(true);
        if (getter != null && getter.IsStatic)
            return true;

        // Check the setter method (if it exists)
        var setter = property.GetSetMethod(true);
        if (setter != null && setter.IsStatic)
            return true;

        // If neither exists (unlikely for a valid property), return false
        return false;
    }

    // Check if the field can be serialized in general
    public static bool DoesFieldPassUnityValidationRules(this FieldInfo field)
    {
        // If explicitly marked as NonSerializable, skip
        if (field.IsDefined(typeof(NonSerializedAttribute))) return false;

        // The type of the field gotta show that it is serializable in some way
        if (!field.FieldType.IsClassSerializable()) return false;

        // if it is public or marked to be serialized, then it can be serialized
        return field.IsPublic || field.IsDefined(typeof(SerializeField), false) || field.IsDefined(typeof(SerializeReference), false);
    }

    public static bool IsClassSerializable(this Type type) =>
        typeof(IEnumerable).IsAssignableFrom(type) || // An IEnumerable, by default, can be serializable, what matters are its items, not itself
        type.IsDefined(typeof(SerializableAttribute)) || // [Serializable]
        type.CanUnitySerialize() || // If Unity can serialize this, why wouldn't it be serializable?
        type.Assembly.IsUnityAssembly(); // If it's an Unity assembly, it HAS to be serializable
}