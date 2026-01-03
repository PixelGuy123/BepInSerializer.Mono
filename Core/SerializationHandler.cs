using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using HarmonyLib;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Text;
using UnitySerializationBridge.Utils;

namespace UnitySerializationBridge.Core;

// from Prefab -> Instance
internal class SerializationHandler : MonoBehaviour, ISerializationCallbackReceiver
{
    // Cache for reflection calls
    private static readonly ConcurrentDictionary<Type, Func<object>> ClassActivatorPair = [];
    private static readonly ConcurrentDictionary<FieldInfo, Func<object, object>> FieldInfoGetterCache = [];
    private static readonly ConcurrentDictionary<Type, Func<SerializationHandler, object, string>> CollectionSerializationInvokerCache = [];
    private static readonly ConcurrentDictionary<Type, Func<SerializationHandler, string, bool, object>> CollectionDeSerializationInvokerCache = [];
    private static readonly ConcurrentDictionary<FieldInfo[], string> FieldInfoPathCache = [];
    private static readonly ConcurrentDictionary<(Type, string), FieldInfo> NamedTypeToFieldInfoCache = [];
    private static readonly ConcurrentDictionary<FieldInfo, Action<object, object>> FieldInfoSetterCache = [];
    private static readonly ConcurrentDictionary<Type, MethodInfo> ElementTypeMethodCache = [];

    // Bunch of strings for everything
    [SerializeField]
    private List<string> _serializedData = [];
    [SerializeField]
    private List<string> _fields = []; // joined string, to store the path, since Unity can't serialize FieldInfo[]
    [SerializeField]
    private List<string> _componentNames = [];
    [SerializeField]
    private List<bool> _isCollection = [];

    // BEFORE SERIALIZATION
    public void OnBeforeSerialize()
    {

        // Clear up the data
        _serializedData.Clear();
        _fields.Clear();
        _componentNames.Clear();
        _isCollection.Clear();

        // Check all registered targets from this GameObject
        try
        {
            foreach (var target in SerializationRegistry.RegisteredTargets)
            {
                // Get the Root Component
                var rootComponent = GetComponent(target.ComponentType);
                if (!rootComponent) continue;

                // Get to the final path
                object currentValue = rootComponent; // Current value is the class to be serialized
                bool pathValid = true;

                foreach (var field in target.Path)
                {
                    if (currentValue == null)
                    {
                        pathValid = false;
                        break;
                    }

                    BridgeManager.Instance.Log($"Value: {currentValue.GetType().Name}");
                    currentValue = CreateFieldGetter(field)(currentValue);
                }


                // Serialize if valid
                if (pathValid && currentValue != null)
                {
                    BridgeManager.Instance.Log($"Gone to final value: {currentValue.GetType().Name}");
                    bool isCollection = target.isCollection;
                    string json;
                    if (isCollection)
                    {
                        // Cached delegate call
                        json = CreateCollectionSerializerInvoker(
                            target.GetCollectionElementType()
                        )(this, currentValue);
                    }
                    else
                    {
                        // Works for references and values, NOT collections
                        json = JsonUtility.ToJson(currentValue);
                    }

                    // Log
                    if (BridgeManager.Instance.enableDebugLogs.Value)
                        BridgeManager.Instance.Log($"Serializing {target.ComponentType.Name} [{target.Path[target.Path.Length - 1].Name}]: {json}");

                    _serializedData.Add(json);
                    // Get cached path
                    if (!FieldInfoPathCache.TryGetValue(target.Path, out var path))
                    {
                        path = string.Join("/", Array.ConvertAll(target.Path, f => f.Name));
                        FieldInfoPathCache.TryAdd(target.Path, path);
                    }
                    _fields.Add(path);
                    _componentNames.Add(target.ComponentType.FullName);
                    _isCollection.Add(isCollection);
                    continue;
                }
            }
        }
        catch (Exception e)
        {
            // Just throws exception, then clear up again the data to prevent incomplete serialization 
            Debug.LogException(e);

            _serializedData.Clear();
            _fields.Clear();
            _componentNames.Clear();
            _isCollection.Clear();
        }
    }
    public void OnAfterDeserialize() { } // Not used

    // Restoration
    private void Awake()
    {
        if (_serializedData.Count != _fields.Count) return;

        for (int i = 0; i < _serializedData.Count; i++)
            ApplyJsonToPath(_componentNames[i], _fields[i], _serializedData[i], _isCollection[i]);
    }

    // Basically draw a path to get the each JSON from this path
    private void ApplyJsonToPath(string compName, string pathString, string json, bool isCollection)
    {
        var root = GetComponent(compName);
        // If null, skip this
        if (!root) return;

        string[] fieldPathNames = pathString.Split('/');
        object currentObject = root;
        Type currentType = root.GetType();

        BridgeManager.Instance.Log($"Parent type: {currentType.Name}");

        // Traverse down to the PARENT of the target field
        for (int i = 0; i < fieldPathNames.Length; i++)
        {
            string fieldName = fieldPathNames[i];
            BridgeManager.Instance.Log($"Field ({fieldName})");

            // Get the field info
            if (!NamedTypeToFieldInfoCache.TryGetValue((currentType, fieldName), out var field))
            {
                field = AccessTools.Field(currentType, fieldName);
                NamedTypeToFieldInfoCache.TryAdd((currentType, fieldName), field);
            }

            BridgeManager.Instance.Log($"Type: {field.FieldType.Name}");

            if (field == null) return; // Path broken somehow

            // If we are at the last node, this is the object to be overriden
            if (i == fieldPathNames.Length - 1)
            {
                Type fieldType = field.FieldType;

                if (isCollection)
                {
                    // Handle Deserialization for Collections
                    Type elementType = fieldType.IsArray ? fieldType.GetElementType() : fieldType.GetGenericArguments()[0];

                    // Loads list through cached delegate call
                    object loadedList = CreateCollectionDeSerializerInvoker(
                        elementType
                        )(this, json, fieldType.IsArray);
                    CreateFieldSetter(field)(currentObject, loadedList);
                }
                else
                {
                    // Standard Object
                    object targetInstance = CreateFieldGetter(field)(currentObject);
                    if (targetInstance == null)
                    {
                        targetInstance = GetActivator(fieldType)();
                        CreateFieldSetter(field)(currentObject, targetInstance);
                    }
                    JsonUtility.FromJsonOverwrite(json, targetInstance);
                }
            }
            else
            {
                // Go deeper
                object nextObject = CreateFieldGetter(field)(currentObject);
                if (nextObject == null)
                {
                    // If the parent path is null, we must create it to reach the child
                    nextObject = GetActivator(field.FieldType)();
                    CreateFieldSetter(field)(currentObject, nextObject);
                }
                currentObject = nextObject;
                currentType = nextObject.GetType();
            }
        }
    }

    // ==========
    // Here are all the helper methods to what is needed inside this whole componen
    // ==========

    // Wrap list with json utility because it works
    private string SerializeCollection<T>(object collectionObj)
    {
        if (collectionObj == null) return string.Empty;

        // Both List<T> and T[] implement IEnumerable
        if (collectionObj is System.Collections.IEnumerable enumerable)
            return enumerable.ToMultiJson();

        return string.Empty;
    }
    // One to deserialize the collection
    private object DeserializeCollection<T>(string json, bool asArray)
    {
        List<T> list = [];

        // Helper to split the string based on balanced braces
        var jsonChunks = JsonUtils.UnpackMultiJson(json);

        var activator = GetActivator(typeof(T));

        foreach (string chunk in jsonChunks)
        {
            // Create a new instance for this item
            object instance = activator();

            // If we have an instance, populate it
            if (instance != null)
            {
                BridgeManager.Instance.Log($"Deserializing collection JSON: {chunk}");
                JsonUtility.FromJsonOverwrite(chunk, instance);
                list.Add((T)instance);
            }
        }

        if (asArray) return list.ToArray();

        return list;
    }

    private Func<object> GetActivator(Type type)
    {
        if (ClassActivatorPair.TryGetValue(type, out var func)) return func;

        if (type.IsAbstract || type.IsInterface)
        {
            // Cannot instantiate abstract/interface. Return null.
            func = () => null;
        }
        else
        {
            // Check if there is a parameterless constructor
            var ctor = type.GetConstructor(Type.EmptyTypes);

            // If there's a ctor, use it
            if (type.IsValueType || ctor != null)
            {
                var expression = Expression.New(type);
                var lambda = Expression.Lambda<Func<object>>(expression);
                func = lambda.Compile();
            }
            else
            {
                // Literally never heard of this class before searching it up
                func = () => FormatterServices.GetUninitializedObject(type);
            }
        }

        ClassActivatorPair.TryAdd(type, func);
        return func;
    }

    private Func<object, object> CreateFieldGetter(FieldInfo fieldInfo)
    {
        BridgeManager.Instance.Log($"({fieldInfo.DeclaringType.Name}::{fieldInfo.Name}). ValueType? {fieldInfo.DeclaringType.IsValueType}");
        if (FieldInfoGetterCache.TryGetValue(fieldInfo, out var getter)) return getter;

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
        FieldInfoGetterCache.TryAdd(fieldInfo, lambda);
        return lambda;
    }

    private Action<object, object> CreateFieldSetter(FieldInfo fieldInfo)
    {
        if (FieldInfoSetterCache.TryGetValue(fieldInfo, out var setter)) return setter;

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
        FieldInfoSetterCache.TryAdd(fieldInfo, lambda);
        return lambda;
    }

    private Func<SerializationHandler, object, string> CreateCollectionSerializerInvoker(Type elementType)
    {
        if (CollectionSerializationInvokerCache.TryGetValue(elementType, out var func)) return func;

        // Get the generic method info
        MethodInfo method = typeof(SerializationHandler)
            .GetMethod(nameof(SerializeCollection), BindingFlags.NonPublic | BindingFlags.Instance)
            .MakeGenericMethod(elementType);

        // (instance, arg) => (string)instance.SerializeCollection<T>(arg)
        ParameterExpression instanceParam = Expression.Parameter(typeof(SerializationHandler), "instance");
        ParameterExpression argumentParam = Expression.Parameter(typeof(object), "arg");

        // Create method call expression
        MethodCallExpression methodCall = Expression.Call(
            instanceParam,
            method,
            argumentParam
        );

        var lambda = Expression.Lambda<Func<SerializationHandler, object, string>>(
            methodCall,
            instanceParam,
            argumentParam
        );

        func = lambda.Compile();
        CollectionSerializationInvokerCache.TryAdd(elementType, func);

        return func;
    }

    private Func<SerializationHandler, string, bool, object> CreateCollectionDeSerializerInvoker(Type elementType)
    {
        if (CollectionDeSerializationInvokerCache.TryGetValue(elementType, out var func)) return func;

        // Get the generic method info
        MethodInfo method = typeof(SerializationHandler)
            .GetMethod(nameof(DeserializeCollection), BindingFlags.NonPublic | BindingFlags.Instance)
            .MakeGenericMethod(elementType);

        // (instance, arg) => (string)instance.SerializeCollection<T>(arg)
        ParameterExpression instanceParam = Expression.Parameter(typeof(SerializationHandler), "instance");
        ParameterExpression jsonParam = Expression.Parameter(typeof(string), "json");
        ParameterExpression arrayParam = Expression.Parameter(typeof(bool), "isArray");

        // Create method call expression
        MethodCallExpression methodCall = Expression.Call(
            instanceParam,
            method,
            jsonParam,
            arrayParam
        );

        var lambda = Expression.Lambda<Func<SerializationHandler, string, bool, object>>(
            methodCall,
            instanceParam,
            jsonParam,
            arrayParam
        );

        func = lambda.Compile();
        CollectionDeSerializationInvokerCache.TryAdd(elementType, func);
        return func;
    }
}