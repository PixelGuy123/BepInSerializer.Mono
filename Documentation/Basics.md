# Basics

In general, there's not much difference when working with the serializer. For _most_ use cases, you can create custom components and implement injected data structures like usual.

---

## 📌 Dependency Inclusion

> GUID: `pixelguy.pixelmodding.bepinex.serializer`

It's recommended to include a **soft** `[BepInDependency()]` to your plugin, especially if, in `Awake()`, any process that **requires** serialization is done.

Here's a quick snippet for that:

```csharp
[BepInDependency("pixelguy.pixelmodding.bepinex.serializer")]
```

If the intention is to use all the **features** from the **BepInSerializer** (refer to the [content table in the README](../README.md#for-plugin-developers)), then you'll definitely need to add `BepInSerializer.dll` as a dependency to your plugin's project.

Here's a snippet for this purpose:

```xml
<ItemGroup>
  <Reference Include="BepInSerializer">
    <HintPath>BepInSerializer.dll</HintPath>
  </Reference>
</ItemGroup>
```

---

## 🔨 Basic Usage

In a usual development of a component, this is how you would implement a `MonoBehaviour` class with its data structures using the serializer:

```csharp
// This component will be instantiated
public class ExampleComponent : MonoBehaviour
{
    public MyStruct myStruct;
    public MyClass myClass;
}

[System.Serializable]
public struct MyStruct
{
    public int id;
    public float value;
    public string name;
}

[System.Serializable]
public class MyClass
{
    public int intValue;
    public float floatValue;
    public string text;
    public MyStruct nested;
}
```

However, there is a special **edge case** we'll address below.

---

## 🔗 Integration with `ISerializationCallbackReceiver`

> [`ISerializationCallbackReceiver`](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ISerializationCallbackReceiver.html) is an interface Unity provides for components to handle whatever they need before being serialized and after being deserialized.

**BepInSerializer** can override the "before" and "after" of an instantiation call (thanks to a [HarmonyX feature](https://github.com/BepInEx/HarmonyX/wiki/Difference-between-Harmony-and-HarmonyX#patching-extern-methods-as-managed) that allows patching `extern` functions); yet, it cannot inspect the internal body of this procedure, which adds a completely unavoidable obstacle when it comes to handling this interface. Because of this obstacle, there's no guarantee to tell when `ISerializationCallbackReceiver.OnBeforeSerialize` and `ISerializationCallbackReceiver.OnAfterDeserialize` can be triggered, and this could break the order of what's invoked first.

To solve this problem, the serializer provides **two separate solutions** that basically do the same thing, but one requires the mod to directly depend on the assembly to properly function.

### 1. `ISafeSerializationCallbackReceiver`

> This interface is located under: `BepInSerializer.Core.Serialization.Interfaces.ISafeSerializationCallbackReceiver`.

|Upside|Downside|
|-------|---------|
|**Same Body Definition:** this interface contains the exact definition of its Unity counterpart.|**Assembly Dependency:** if one plugin is designed to support more than one serialization method, this approach does not suit it.|

|**How to Implement**|
|------------------|
|1. Simply make your component inherit `ISafeSerializationCallbackReceiver`.|

_Code Example:_

```csharp
using BepInSerializer.Core.Serialization.Interfaces;

// Reuses MyStruct and MyClass from the core example above
public class ExampleComponentSafe : MonoBehaviour, ISafeSerializationCallbackReceiver
{
    public MyStruct myStruct;
    public MyClass myClass;

    // Called by BepInSerializer (safe) before serialization
    public void OnBeforeSerialize()
    {
        // prepare or validate data prior to serialization
    }

    // Called by BepInSerializer (safe) after deserialization
    public void OnAfterDeserialize()
    {
        // restore runtime-only state after deserialization
    }
}
```

### 2. Special Boolean Field

> The field name must be exactly: **`"__BepInSerializer_BlockSerializationCallbackAction"`**.

|Upside|Downside|
|-------|---------|
|**Assembly Independency:** this alternative does not require a plugin to alter their dependencies.|**Close to an Anti-Pattern:** although it is simple, it may be confusing to have a field that is used by another application _and accessed through reflection._|

|**How to Implement**|
|------------------|
|1. Add a **private boolean-type** field with the `[SerializeField]` attribute and name it `"__BepInSerializer_BlockSerializationCallbackAction"`.|
|2. Add `if` blocks in the interface's callbacks to `return` if such field is true.|

_Code Example:_

```csharp
public class ExampleComponentBoolGuard : MonoBehaviour, ISerializationCallbackReceiver
{
    // Special boolean field — it needs to contain this EXACT name
    [SerializeField] // <-- Important to not lose data of this field during the instantiation procedure
    private bool __BepInSerializer_BlockSerializationCallbackAction;

    // Some fields with data
    public MyStruct myStruct;
    public MyClass myClass;

    public void OnBeforeSerialize()
    {
        // Add these if blocks to both callbacks from ISerializationCallbackReceiver
        if (__BepInSerializer_BlockSerializationCallbackAction) return;
        // normal pre-serialization logic
    }

    public void OnAfterDeserialize()
    {
        if (__BepInSerializer_BlockSerializationCallbackAction) return;
        // normal post-deserialization logic
    }
}
```
