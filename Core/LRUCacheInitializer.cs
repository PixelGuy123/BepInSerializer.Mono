using BepInSerializer.Core.Serialization;
using BepInSerializer.Utils;

namespace BepInSerializer.Core;

// Basically only triggered by the Plugin to initialize the cache after the configurations are all set in
internal static class LRUCacheInitializer
{
    public static void InitializeCacheValues()
    {
        int sizeForTypesCache = BridgeManager.sizeForTypesReflectionCache.Value;
        int sizeForMemberAccessCache = BridgeManager.sizeForMemberAccessReflectionCache.Value;
        int controlledSizeForTypes = sizeForTypesCache > 450 ? sizeForTypesCache / 5 : sizeForTypesCache / 2;

        // Reflection Utils
        ReflectionUtils.FieldInfoGetterCache = new(sizeForMemberAccessCache);
        ReflectionUtils.PropertyInfoGetterCache = new(sizeForMemberAccessCache);
        ReflectionUtils.FieldInfoSetterCache = new(sizeForMemberAccessCache);
        ReflectionUtils.PropertyInfoSetterCache = new(sizeForMemberAccessCache);
        ReflectionUtils.TypeNameCache = new(sizeForTypesCache);
        ReflectionUtils.GenericActivatorConstructorCache = new(controlledSizeForTypes);
        ReflectionUtils.ConstructorCache = new(controlledSizeForTypes);
        ReflectionUtils.ParameterlessActivatorConstructorCache = new(controlledSizeForTypes);
        ReflectionUtils.ArrayActivatorConstructorCache = new(controlledSizeForTypes);
        ReflectionUtils.SelfActivatorConstructorCache = new(controlledSizeForTypes);
        ReflectionUtils.TypeToFieldsInfoCache = new(controlledSizeForTypes);
        ReflectionUtils.TypeToPropertiesInfoCache = new(controlledSizeForTypes);
        ReflectionUtils.FieldInfoCache = new(controlledSizeForTypes);

        // DelegateProvider
        DelegateProvider._methodCache = new(controlledSizeForTypes);

        // SerializationRegistry
        SerializationRegistry.LongHierarchyComponentFieldMap = new(controlledSizeForTypes);

        // Assembly Utils
        AssemblyUtils.CollectionNestedElementTypesCache = new(controlledSizeForTypes);
        AssemblyUtils.TypeIsManagedCache = new(controlledSizeForTypes);
        AssemblyUtils.TypeIsUnityManagedCache = new(controlledSizeForTypes);
        AssemblyUtils._cacheIsAvailable = true;
    }
}