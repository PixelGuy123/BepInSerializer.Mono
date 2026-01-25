using System.Reflection;
using System;
using System.Linq.Expressions;
using HarmonyLib;
using System.Collections.Generic;
using BepInSerializer.Core.Models;
using System.Linq;

namespace BepInSerializer.Utils;

internal static class ReflectionUtils
{
    // Delegates for specific constructors
    public delegate Array ArrayConstructorDelegate(params int[] lengths);
    // Structs for caching keys
    internal record struct BaseTypeElementTypeItem(Type Base, Type[] Elements);
    internal record struct BaseTypeRankLengthItem(Type Base, int RankCount);
    // Caching system
    internal static LRUCache<FieldInfo, Func<object, object>> FieldInfoGetterCache;
    internal static LRUCache<PropertyInfo, Func<object, object>> PropertyInfoGetterCache;
    internal static LRUCache<FieldInfo, Action<object, object>> FieldInfoSetterCache;
    internal static LRUCache<PropertyInfo, Action<object, object>> PropertyInfoSetterCache;
    internal static LRUCache<string, Type> TypeNameCache;
    internal static LRUCache<string, Func<object, object>> ConstructorCache;
    internal static LRUCache<BaseTypeElementTypeItem, Func<object>> GenericActivatorConstructorCache;
    internal static LRUCache<Type, Func<object>> ParameterlessActivatorConstructorCache;
    internal static LRUCache<BaseTypeRankLengthItem, ArrayConstructorDelegate> ArrayActivatorConstructorCache;
    internal static LRUCache<Type, Func<object, object>> SelfActivatorConstructorCache;
    internal static LRUCache<Type, List<FieldInfo>> TypeToFieldsInfoCache;
    internal static LRUCache<Type, List<PropertyInfo>> TypeToPropertiesInfoCache;
    internal static LRUCache<Type, Dictionary<string, FieldInfo>> FieldInfoCache;
    public static List<FieldInfo> GetSerializableFieldInfos(this Type type)
    {
        if (TypeToFieldsInfoCache.NullableTryGetValue(type, out var fields))
            return fields;

        // Cache fields for this specific type call
        fields = AccessTools.GetDeclaredFields(type);
        bool isDebugEnabled = BridgeManager.enableDebugLogs.Value;
        bool isDeclaringTypeAComponent = type.IsUnityComponentType(); // If the declaring type is a component, Unity serialization rules apply

        // Filter out the fields that cannot be serialized
        for (int i = fields.Count - 1; i >= 0; i--)
        {
            var field = fields[i];

            // Static or non-writeable is irrelevant
            if (field.IsStatic || field.IsLiteral || field.IsInitOnly)
            {
                fields.RemoveAt(i);
                continue;
            }

            Type fieldType = field.FieldType;

            // Apply Serialization implementation (private fields can't be serialized, like in Unity)
            if (!field.DoesFieldPassUnityValidationRules())
            {
                // Skip if it's a primitive
                if (isDebugEnabled)
                {
                    BridgeManager.logger.LogInfo($"{field.Name} SKIPPED.");
                }
                fields.RemoveAt(i);
                continue;
            }

            if (isDeclaringTypeAComponent && fieldType.CanUnitySerialize())
            {
                // Skip if it can be serialized by default
                if (isDebugEnabled)
                {
                    BridgeManager.logger.LogInfo($"{field.Name} SKIPPED.");
                }
                fields.RemoveAt(i);
                continue;
            }
        }

        // Try to get fields from base types
        var baseType = type.BaseType;
        if (baseType != null && baseType != typeof(object))
        {
            var baseFields = baseType.GetSerializableFieldInfos();
            fields.AddRange(baseFields);
        }

        TypeToFieldsInfoCache.NullableAdd(type, fields);
        return fields;
    }

    public static List<PropertyInfo> GetSerializablePropertyInfos(this Type type)
    {
        // If no cache, do expensive part
        if (TypeToPropertiesInfoCache.NullableTryGetValue(type, out var properties))
            return properties;

        // Cache fields for this specific type call
        properties = AccessTools.GetDeclaredProperties(type);

        // Filter out the fields that cannot be serialized
        for (int i = properties.Count - 1; i >= 0; i--)
        {
            var property = properties[i];

            // Static is irrelevant
            if (property.IsStatic())
            {
                properties.RemoveAt(i);
                continue;
            }

            // The property must have getter and setter, and one getter that's without parameters (there's get_Item(int32))
            if (!property.CanWrite || property.GetGetMethod(true) == null || property.GetGetMethod(true).GetParameters().Length != 0)
            {
                properties.RemoveAt(i);
                continue;
            }
        }

        // Get the base type for properties
        var baseType = type.BaseType;
        if (baseType != null && baseType != typeof(object))
        {
            var baseProperties = baseType.GetSerializablePropertyInfos();
            properties.AddRange(baseProperties);
        }

        TypeToPropertiesInfoCache.NullableAdd(type, properties);
        return properties;
    }

    public static Func<object, object> CreateFieldGetter(this FieldInfo fieldInfo)
    {
        if (FieldInfoGetterCache.NullableTryGetValue(fieldInfo, out var getter)) return getter;

        var instanceParam = Expression.Parameter(typeof(object), "instance");

        // Convert instance to the declaring type
        var typedInstance = Expression.Convert(instanceParam, fieldInfo.DeclaringType);

        // Access the field
        var fieldExp = Expression.Field(typedInstance, fieldInfo);

        // Convert result to object if needed
        var resultExp = fieldInfo.FieldType.IsValueType ?
            Expression.Convert(fieldExp, typeof(object)) :
            (Expression)fieldExp;

        var lambda = Expression.Lambda<Func<object, object>>(resultExp, instanceParam).Compile();
        FieldInfoGetterCache.NullableAdd(fieldInfo, lambda);
        return lambda;
    }

    public static Func<object, object> CreatePropertyGetter(this PropertyInfo propertyInfo)
    {
        if (PropertyInfoGetterCache.NullableTryGetValue(propertyInfo, out var getter)) return getter;

        var instanceParam = Expression.Parameter(typeof(object), "instance");

        // Convert instance to the declaring type
        var typedInstance = Expression.Convert(instanceParam, propertyInfo.DeclaringType);

        // Access the property
        var propertyExp = Expression.Property(typedInstance, propertyInfo);

        // Convert result to object if needed
        var resultExp = propertyInfo.PropertyType.IsValueType ?
            Expression.Convert(propertyExp, typeof(object)) :
            (Expression)propertyExp;

        var lambda = Expression.Lambda<Func<object, object>>(resultExp, instanceParam).Compile();
        PropertyInfoGetterCache.NullableAdd(propertyInfo, lambda);
        return lambda;
    }

    public static Action<object, object> CreateFieldSetter(this FieldInfo fieldInfo)
    {
        if (FieldInfoSetterCache.NullableTryGetValue(fieldInfo, out var setter)) return setter;

        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var valueParam = Expression.Parameter(typeof(object), "value");

        // Convert instance to the declaring type
        var typedInstance = Expression.Convert(instanceParam, fieldInfo.DeclaringType);
        var typedValue = Expression.Convert(valueParam, fieldInfo.FieldType);

        // (Assign) Set the field
        var assignExp = Expression.Assign(
            Expression.Field(typedInstance, fieldInfo),
            typedValue
        );

        var lambda = Expression.Lambda<Action<object, object>>(assignExp, instanceParam, valueParam).Compile();
        FieldInfoSetterCache.NullableAdd(fieldInfo, lambda);
        return lambda;
    }

    public static Action<object, object> CreatePropertySetter(this PropertyInfo propertyInfo)
    {
        if (PropertyInfoSetterCache.NullableTryGetValue(propertyInfo, out var setter)) return setter;

        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var valueParam = Expression.Parameter(typeof(object), "value");

        // Convert instance to the declaring type
        var typedInstance = Expression.Convert(instanceParam, propertyInfo.DeclaringType);
        var typedValue = Expression.Convert(valueParam, propertyInfo.PropertyType);

        // (Assign) Set the property
        var assignExp = Expression.Assign(
            Expression.Property(typedInstance, propertyInfo),
            typedValue
        );

        var lambda = Expression.Lambda<Action<object, object>>(assignExp, instanceParam, valueParam).Compile();
        PropertyInfoSetterCache.NullableAdd(propertyInfo, lambda);
        return lambda;
    }


    // There are some Unity components that have their own constructor for duplication (new Material(Material))
    public static bool TryGetSelfActivator(this Type type, out Func<object, object> func)
    {
        if (SelfActivatorConstructorCache.NullableTryGetValue(type, out func)) return true;

        var selfConstructor = type.GetConstructor([type]); // Get a constructor that is itself
        if (selfConstructor == null)
        {
            func = null;
            return false;
        }

        // Get the parameter as object
        var parameter = Expression.Parameter(typeof(object), "self"); // (object self) => { }
        // Cast the parameter as desired type
        var typedParameter = Expression.Convert(parameter, type); // (object self) => { (Material)self }
        // Put this parameter to be used inside the constructor
        var newExpression = Expression.New(selfConstructor, typedParameter); // (object self) => new Material((Material)self);
        // Compile expression
        func = Expression.Lambda<Func<object, object>>(newExpression, parameter).Compile();
        SelfActivatorConstructorCache.NullableAdd(type, func);
        return true;
    }

    public static Func<object> GetParameterlessConstructor(this Type type)
    {
        // Check if the type has a generic constructor to use the other function instead
        if (type.IsGenericType || type.IsGenericTypeDefinition)
        {
            // BridgeManager.logger.LogInfo($"This is a parameterless generic type: {type}. Using generic parameterless constructor.");
            return type.GetGenericParameterlessConstructor();
        }

        // Check for cache
        if (ParameterlessActivatorConstructorCache.NullableTryGetValue(type, out var func)) return func;
        var parameterlessConstructor = type.GetConstructor(Type.EmptyTypes);
        if (parameterlessConstructor == null)
        {
            ParameterlessActivatorConstructorCache.NullableAdd(type, null);
            return null;
        }

        // Create the Expression: () => new Type()
        NewExpression newExp = Expression.New(parameterlessConstructor);

        // Cast to object so the delegate is compatible with Func<object>
        UnaryExpression castExp = Expression.Convert(newExp, typeof(object));

        // Compile it into a reusable delegate
        func = Expression.Lambda<Func<object>>(castExp).Compile(); // () => (object)new Type();
        ParameterlessActivatorConstructorCache.NullableAdd(type, func);
        return func;
    }
    public static Func<object> GetGenericParameterlessConstructor(this Type genericDefinition, params Type[] elementTypes)
    {
        // If it is not already a type definition, make it one
        if (!genericDefinition.IsGenericTypeDefinition)
            return genericDefinition.GetGenericTypeDefinition().GetGenericParameterlessConstructor(genericDefinition.GetGenericArguments());

        var typeElement = new BaseTypeElementTypeItem(genericDefinition, elementTypes);

        if (GenericActivatorConstructorCache.NullableTryGetValue(typeElement, out var func))
            return func;

        // Create the constructed generic type: List<T> becomes List<int>
        if (elementTypes.Length != genericDefinition.GetGenericArguments().Length)
            throw new ArgumentException($"Number of type arguments ({elementTypes.Length}) doesn't match the generic type definition's arity ({genericDefinition.GetGenericArguments().Length})");

        Type constructedType = genericDefinition.MakeGenericType(elementTypes);

        // Get the appropriate constructor
        var constructor = constructedType.GetConstructor(Type.EmptyTypes);

        Expression newExp;

        if (constructor != null)
        {
            // Class with explicit parameterless constructor or struct with explicit parameterless constructor (C# 10+)
            newExp = Expression.New(constructor);
        }
        else if (constructedType.IsValueType)
        {
            // using Expression.Default, which creates the default value for value types
            newExp = Expression.Default(constructedType);
        }
        else
        {
            // If it doesn't contain a parameterless constructor and it's a class, just return a null func
            func = null;
            GenericActivatorConstructorCache.NullableAdd(typeElement, func);
            return func;
        }

        // Cast to object so the delegate is compatible with Func<object>
        UnaryExpression castExp = Expression.Convert(newExp, typeof(object));

        // Compile it into a reusable delegate
        func = Expression.Lambda<Func<object>>(castExp).Compile();
        GenericActivatorConstructorCache.NullableAdd(typeElement, func);

        return func;
    }

    public static ArrayConstructorDelegate GetArrayConstructor(this Type elementType, int rankCount)
    {
        if (rankCount < 1) throw new ArgumentException("Rank count must be at least 1.");
        var rankLengthItem = new BaseTypeRankLengthItem(elementType, rankCount);
        if (ArrayActivatorConstructorCache.NullableTryGetValue(rankLengthItem, out var func))
            return func;

        // Create parameter expression for the array lengths
        ParameterExpression lengthsParam = Expression.Parameter(typeof(int[]), "lengths");

        // Create new array expression: new T[lengths[0], lengths[1], ..., lengths[rankCount-1]]
        NewArrayExpression newArrayExp = Expression.NewArrayBounds(elementType,
            Enumerable.Range(0, rankCount).Select(i =>
                Expression.ArrayIndex(lengthsParam, Expression.Constant(i))
            )
        );

        // Cast the T[,] as System.Array
        UnaryExpression castExp = Expression.TypeAs(newArrayExp, typeof(Array));

        // Compile the lambda: (int[] lengths) => (Array)new T[lengths[0], lengths[1], ..., lengths[rankCount-1]]
        func = Expression.Lambda<ArrayConstructorDelegate>(castExp, lengthsParam).Compile();
        ArrayActivatorConstructorCache.NullableAdd(rankLengthItem, func);

        return func;
    }


    public static Type GetFastType(string compName)
    {
        // Expensive lookup if no cache available
        if (TypeNameCache == null)
            return Type.GetType(compName);

        // Fast Type Lookup
        if (!TypeNameCache.TryGetValue(compName, out Type compType))
        {
            compType = Type.GetType(compName);
            if (compType != null) TypeNameCache.Add(compName, compType);
        }
        return compType;
    }

    public static FieldInfo GetFastField(this Type compType, string fieldName)
    {
        if (FieldInfoCache == null) return AccessTools.Field(compType, fieldName);

        var fields = FieldInfoCache.GetValue(compType, t => []);
        if (!fields.TryGetValue(fieldName, out var field))
        {
            field = AccessTools.Field(compType, fieldName);
            fields[fieldName] = field;
        }
        return field;
    }

    public static bool IsTypeHierarchyGeneric(this Type type)
    {
        Type t = type;
        while (t != null && t != typeof(object) && t != typeof(UnityEngine.Object))
        {
            if (t.IsGenericType) return true;
            t = t.BaseType;
        }
        return false;
    }
}