using System;
using System.Reflection;
using System.Linq.Expressions;
using BepInSerializer.Core.Models;
using BepInSerializer.Utils;
using HarmonyLib;

namespace BepInSerializer.Core.Serialization;

internal static class DelegateProvider
{
    internal record struct DelegateType(DelegateMethod Method, Type Type)
    {
        public override readonly string ToString() => $"{Type.FullDescription()}::{Method.GetDelegateMethodName()}";
    }
    // --- State ---
    // Caches for the delegates we will invoke manually later
    internal static LRUCache<DelegateType, Action<object>> _methodCache;
    // Other fields
    private const BindingFlags MethodBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    public static Action<object> GetMethodInvoker(Type t, DelegateMethod method) => GetCachedDelegate(t, method);

    // --- Internal Logic ---
    private static Action<object> GetCachedDelegate(Type type, DelegateMethod supMethod)
    {
        if (supMethod == DelegateMethod.Null) return null;
        var typeSuppress = new DelegateType(supMethod, type);

        if (_methodCache.NullableTryGetValue(typeSuppress, out var action)) return action;

        var method = GetMethodRecursive(type, supMethod.GetDelegateMethodName());
        if (method != null)
        {
            // Create open delegate (Action<object>)
            var param = Expression.Parameter(typeof(object), "obj");
            var cast = Expression.Convert(param, type);
            var call = Expression.Call(cast, method);
            var compiledAction = Expression.Lambda<Action<object>>(call, param).Compile();

            // Make a safe try-catch wrapper to specify which method threw the error
            action = (obj) =>
            {
                try
                {
                    compiledAction(obj);
                }
                catch (TargetInvocationException e)
                {
                    BridgeManager.logger.LogError($"Error thrown in method: {method.FullDescription()}");
                    BridgeManager.logger.LogError(e.InnerException); // Get the inner exception
                }
                catch (Exception e)
                {
                    BridgeManager.logger.LogError($"Error thrown in method: {method.FullDescription()}");
                    BridgeManager.logger.LogError(e);
                }
            };
        }

        _methodCache.NullableAdd(typeSuppress, action); // Cache result (even if null)
        return action;
    }

    // Helper Method
    private static string GetDelegateMethodName(this DelegateMethod method) => method switch
    {
        DelegateMethod.Awake => "Awake",
        DelegateMethod.OnEnable => "OnEnable",
        _ or DelegateMethod.Null => string.Empty
    };

    private static MethodInfo GetMethodRecursive(Type type, string name) // Type.GetMethod cannot returned INHERITED PRIVATE methods, so we manually do that
    {
        while (type != null && type != typeof(object) && type != typeof(UnityEngine.Object))
        {
            // Basically search for void() and that's it
            var m = type.GetMethod(name, MethodBindingFlags, null, Type.EmptyTypes, []);
            if (m != null)
            {
                if (m.ReturnType != typeof(void)) return null; // There won't be other types above, so return null already
                return m;
            }
            type = type.BaseType;
        }
        return null;
    }
}