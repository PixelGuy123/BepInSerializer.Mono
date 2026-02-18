# Writing Converters

> This document explains, in detail, how to write a converter from the ground up.
> If you haven't been introduced to converters yet, it's highly recommended to check out the [**Converters**](Converters.md) page before diving into this guide.
>
> Everything converter-related lives under the following namespaces: `BepInSerializer.Core.Serialization`, `BepInSerializer.Core.Serialization.Converters` and `BepInSerializer.Core.Serialization.Converters.Models`.

The serialization system in **BepInSerializer** is built around a pipeline of `FieldConverter` instances. Each converter is responsible for deciding whether it can handle a particular field (based on its type, attributes, or assembly) and then producing a deep copy or transformed version of that field's value. This guide explains the core concepts and walks you through creating a custom converter for `HashSet<T>`.

---

## 💡 Core Concepts

Before writing a converter, you should understand a few key types and helper methods that are available to every converter.

For starters, here's a component we'll use during this tutorial to be an example of how the converter interacts with this class during conversion:

```csharp
// The component to be instantiated (in our guide)
public class OurComponent : MonoBehaviour
{
    public float myValue;
    [SerializeReference]
    private DataStructure dataA, dataB;

    public DataStructure soleData;
}

// A basic data structure
[System.Serializable]
public class DataStructure
{
    public int someValue;
    [SerializeField]
    private string myString;
}
```

---

### 🔍 Understanding `FieldContext`

> **"Conversion"** means taking the reference of the original object and of the clone object, and **assign/convert** every value from the fields inside the original instance to the clone, according to the **conversion rules** established by each unique converter in this system.

When the conversion starts, through `FieldConverter.Convert(FieldContext)`, a `FieldContext` object is always passed. This class contains all the data you need to handle the value of the targeted field.

Below are the most important `FieldContext` members and factory helpers you will use in converters.

|Member / Factory|What it contains / does|Example (using `OurComponent`)|
|---|---|---|
|`CreateSubContext(FieldInfo)`|Create a child context for a specific field. `OriginalValue` becomes the field's value and `ValueType` the field's declared type.|When converting `OurComponent.soleData` call `FieldContext.CreateSubContext(parentCtx, fieldInfo)` — the returned `ValueType` is `typeof(DataStructure)` and `OriginalValue` is that instance.|
|`CreateSubContext(PropertyInfo)`|Create a context for a property (useful for Unity properties with getters).|Used when converting a Unity `Transform` property obtained via reflection.|
|`CreateRemoteContext(originalContext, originalValue, originalType = null)`|Create a context for values that are not tied to a `FieldInfo` (collection elements, dictionary keys/values, etc.).|When iterating a `List<DataStructure>`, build a remote context for each element with the collection's parent context as `originalContext`.|
|`TryBeginDependencyScope(out IDependencyScope)`|Begin a dependency scope tracked by the conversion's `CircularDependencyDetector`. Returns `false` if a circular dependency is detected. Always `Dispose()` the returned scope when finished.|Before running a recursion into a nested object: `if (!ctx.TryBeginDependencyScope(out var s)) return; using (s) { /* recurse */ }`.|
|`OriginalValue`|The actual runtime value being converted (can be `null`).|`0.25f` for `OurComponent.myValue`.|
|`ValueType`|The declared or inferred type used for conversion.|`typeof(DataStructure)` for `soleData`.|
|`PreviousOriginalValue` / `PreviousValueType`|The value/type from the parent (previous) context; useful to detect A→B→A scenarios or to make decisions based on the parent type.|When converting nested objects you can inspect `PreviousValueType` to avoid recreating nodes unnecessarily.|
|`ContainsSerializeReference`|`true` if the field/property is marked with `[SerializeReference]`.|In `OurComponent`, `dataA` and `dataB` would share the same object reference.|
|`ContainsAllowCollectionNesting`|`true` if the field/property is marked with `[AllowCollectionNesting]` (permits nested collection population).|Allows `List<HashSet<T>>` or `HashSet<List<T>>` elements to be recursively populated by collection converters.|

---

### 🔁 What is Circular Dependency Detection?

The framework provides a `CircularDependencyDetector` and a `IDependencyScope` pattern. Before you recursively process an object (like a class/struct field), you should call `context.TryBeginDependencyScope(out var scope)`. If it returns `false`, a circular reference has been detected, and you should **not** process that value further. Otherwise, wrap the recursive work in a `using` block on the returned scope. This automatically unregisters the object when you are done.

To concretize the idea, here's a snippet of how `ClassConverter` uses the detector:

```csharp
// excerpt from ClassConverter.Convert
if (context.TryBeginDependencyScope(out var objectScope))
{
    using (objectScope)
    {
        if (context.OriginalValue == null)
        {
            if (objectScope.DoesScopeContainsType(context.PreviousValueType))
                return null; // avoid infinitely creating nodes in A -> B -> null scenarios
            return TryConstructNewObject(context, out newConvert) ? newConvert : null;
        }
    }
}

// later, when handling each field:
if (!newContext.TryBeginDependencyScope(out var fieldScope))
    return; // detected circular reference for this specific field
using (fieldScope)
{
    newConvert = setValue(newConvert, ReConvert(newContext));
}
```

_Why does this matter:_

- Top-level scope prevents creation of repeated object graphs (A → B → A).
- Per-field scope prevents descending into values that would create immediate cycles (A → B → A).
- Use `DependencyScope.DoesScopeContainsType(...)` when a `null` encountered could otherwise lead to infinite object creation.
  
---

### 🧰 Helper Methods in `FieldConverter`

The `FieldConverter` base supplies several protected helpers you will call frequently. The table below shows what each helper does and a `OurComponent`-style example.

|Helper|Purpose|Example (using `OurComponent` / `DataStructure`)|
|---|---|---|
|`ReConvert(FieldContext)`|Send a sub-context back into the global conversion pipeline and return the converted value. Always prefer this over calling converters directly.|Convert `soleData`'s `DataStructure` field by calling `ReConvert(FieldContext.CreateSubContext(...))`.|
|`TryInvokeMethod(object, string, params object[])`|Invoke a method on an instance using cached invokers (faster than reflection). Useful for collection `Add`/`Clear` calls.|`TryInvokeMethod(newList, "Add", convertedItem)` when populating a `List<T>` or `HashSet<T>`.|
|`TryGetMappedUnityObject(FieldContext, out UnityEngine.Object)`|Resolve Unity object mapping (used when cloning GameObjects/Components).|When converting a `GameObject`-backed field, this returns the mapped child object if available.|
|`TryConstructNewObject(FieldContext, bool, out object)` / `TryConstructNewObject(FieldContext, out object)`|Create a new instance via parameterless constructor or **(if allowed by the caller)** `FormatterServices.GetUninitializedObject` fallback.|Used by `ClassConverter` to create a new `DataStructure` instance before populating fields.|
|`TryCopyNewObject(FieldContext, out object)`|Clone a `UnityEngine.Object` using a self-activator or `Instantiate` fallback.|Used to copy `Component`/`ScriptableObject` instances where supported.|
|`TryConstructNewArray(FieldContext, int[] lengths, out Array)`|Construct multi-dimensional arrays of the `ValueType` element type.|Used when converting `int[,]` or `Vector3[,]` arrays.|
|`ManageFieldsFromType(FieldContext, Type, Action<FieldContext, SetValue>)`|Iterate all serializable fields on a `Type`, create subcontexts and invoke the provided action.|`ClassConverter` uses this to walk every serializable field of a class and `ReConvert` them.|
|`ManagePropertiesFromType(FieldContext, Type, Action<FieldContext, SetValue>)`|Same as above for properties (uses property getters/setters).|Useful for Unity types that expose serializable properties.|
|`IsUnityAssembly(Type)`|Helper check to detect Unity assemblies/types.|Avoid special handling if `IsUnityAssembly(type)` is `true`.|

---

## 🛠️ Writing a Custom Converter: `IEnumerable`

The framework already includes converters for `List<T>` and `Dictionary<TKey,TValue>`, both follows a very specific pattern. This pattern can be generalized and incorporated into a `IEnumerable` converter, turning into the perfect example we can follow to understand the construction of a converter — specialized for collections.

### Step 1: Create the Converter Class

Derive from `FieldConverter` and give it a meaningful name.

```csharp
using System;
using System.Collections.Generic;
using BepInSerializer.Core.Serialization.Converters.Models;

namespace BepInSerializer.Core.Serialization.Converters
{
    internal class IEnumerableConverter : FieldConverter
    {
        public override bool CanConvert(FieldContext context)
        {
            // Context check goes here
        }

        public override object Convert(FieldContext context)
        {
            // Implementation goes here
        }
    }
}
```

### Step 2: Implement `CanConvert`

After making the class, we stumble into the first **problem:** what _types_ could be accepted by this converter?

Since this converter accepts anything that inherits `IEnumerable`, we've got a lot of types to consider here — for example, `UnityEngine.Transform` inherits `IEnumerable`, so we cannot include it.

To keep things simple, **let's delimitate our scope to `HashSet<T>` collections only.** In order to do that, we must assure that this converter selects **only** `HashSet<T>` objects. We can do that by checking its generic type definition:

```csharp
using System;
using System.Collections.Generic;
using BepInSerializer.Core.Serialization.Converters.Models;

namespace BepInSerializer.Core.Serialization.Converters
{
    internal class IEnumerableConverter : FieldConverter
    {
        public override bool CanConvert(FieldContext context)
        {
            // Get the generic type definition (e.g., HashSet<>)
            var type = context.ValueType;
            if (type.IsGenericType)
                type = type.GetGenericTypeDefinition();

            return type == typeof(HashSet<>); // This way, we can confirm that the type is exclusively a HashSet<T>
        }

        public override object Convert(FieldContext context)
        {
            // Implementation goes here
        }
    }
}
```

> If you wish to support more collections, you can always insert more types into the `CanConvert(FieldContext)` body.

### Step 3: Implement `Convert`

With `CanConvert(FieldContext)` completed, the last step for building the converter is _writing the converter_. In other words, write an algorithm for the `IEnumerable` interface.

To save you some time explaining things step-by-step, the full implementation is pasted below; a summarized list representing what `Convert(FieldContext)` is doing follows up right after the snippet.

Here is the full implementation:

```csharp
internal class IEnumerableConverter : FieldConverter
{
    public override bool CanConvert(FieldContext context)
    {
        var type = context.ValueType;
        // Make sure to get the generic definition first
        if (type.IsGenericType)
            type = type.GetGenericTypeDefinition();

        // Then, check specifically for HashSet<>
        return typeof(HashSet<>) == type;
    }

    public override object Convert(FieldContext context)
    {
        // Declare a new object to hold the new object
        object newObject;

        // 1.
        // If the original value is null, use the standard serialize reference check
        if (context.OriginalValue == null)
            return !context.ContainsSerializeReference && TryConstructNewObject(context, allowUninitialized: false, out newObject) ? newObject : null;

        // 2.
        // Generic argument from HashSet<T>
        var genericType = context.ValueType.GetGenericArguments()[0];

        // 3.
        // If the collection is not allowed to be populated, return null
        if (!context.ContainsAllowCollectionNesting && !CanHashSetBeRecursivelyPopulated(genericType))
            return null;

        // 4.
        // Make a new HashSet (object)
        if (TryConstructNewObject(context, out var newObject))
        {
            // 5.
            // Copy the original items to this new HashSet, by using ReConvert
            foreach (var item in context.OriginalValue as IEnumerable)
            {
                // Make a remote context for each item and add to the new collection
                var convertedItem = ReConvert(FieldContext.CreateRemoteContext(context, item, genericType));
                TryInvokeMethod(newObject, "Add", convertedItem);
            }

            // 6.
            return newObject;
        }

        // If no HashSet has been given, return null
        return null;
    }

    // Whether the element type can be recursively populated.
    // Prevents infinite recursion for nested collections unless explicitly allowed.
    protected virtual bool CanHashSetBeRecursivelyPopulated(Type elementType) =>
        elementType == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(elementType); // If the type is not an IEnumerable (except strings), continue
}
```

Here's the summary of what is going on above:

1. Guard for `null`; if the original value was null, then do a simple check with `context.ContainsSerializeReference` to ensure the right behavior is executed for null classes in that situation.
2. Resolve the element type (`genericType`) from `context.ValueType`, so each item can be converted with the correct expected type.
3. Respect collection-nesting policy. If `ContainsAllowCollectionNesting` is `false` and the element type is itself a `IEnumerable` (except `string`), the converter declines to populate to align with Unity's serialization rules.
4. Construct the target `HashSet<T>` using `TryConstructNewObject`. If construction fails, the converter returns `null`.
5. Iterate the original collection (`context.OriginalValue as IEnumerable`) and for each element:
   - Create a `FieldContext.CreateRemoteContext` for that element (so it inherits the parent's circular-detector and flags).
   - Call `ReConvert` on the remote context to let the registry pick the appropriate converter for the element.
   - Add the converted item to the new `HashSet` using `TryInvokeMethod(newObject, "Add", convertedItem)`.
6. Return the newly populated `HashSet<T>`.

### Step 4: Register the Converter

Register the converter so the `ConversionRegistry` will consider it globally, or apply it per-field with `[UseConverter]` when you need field-level overrides.

- Global registration (recommended for general-purpose converters):

```csharp
// Run once at initialization
ConversionRegistry.RegisterConverter(new IEnumerableConverter());
```

- Per-field registration (overrides global order for that field):

```csharp
[UseConverter(typeof(IEnumerableConverter))]
public HashSet<DataStructure> mySet;
```

Notes about priority:

- Converters supplied via `[UseConverter]` are always tried before the global `ConversionRegistry` list.
- `ConversionRegistry.RegisterConverter` appends converters; converters **registered later** have higher priority in the registry loop.

---

## 🔬 Testing Your Converter

This is the one of the most important steps when making a converter: **test it.**

> **_Does it work with certain values?_ Or, in the case of generics, _does it convert properly this type with this generic type?_**
> Questions such as these are essential for assuring your converter is ready to be used.

**Always test** with scenarios that include (general checklist):

- [ ] **Null handling:** verify `null` fields remain `null` and non-null are copied/constructed correctly.
- [ ] **Circular references:** ensure `TryBeginDependencyScope` prevents infinite recursion (A→B→A and deeper cycles).
- [ ] **Collections & nesting:** test nested collections and confirm `ContainsAllowCollectionNesting` behavior (if your converter is for that purpose).
- [ ] **Types without parameterless constructors:** ensure `TryConstructNewObject` fallback behavior is correct (or the converter returns `null`).
- [ ] **Read-only properties / backing-field-only scenarios:** ensure `ManageFieldsFromType`/`ManagePropertiesFromType` handle setters or skip when appropriate.
- [ ] **Unity objects:** identity mapping vs cloning (use `TryGetMappedUnityObject` / `TryCopyNewObject`).
- [ ] **Generic and polymorphic types:** confirm `ValueType` / runtime type handling for `SerializeReference` and polymorphic lists.

### 🛡️ Tests For `HashSet<T>`

Coming back to our example, this is what we'd usually **look for** when checking if the converter is functioning:

- [ ] Primitive elements (ints, enums).
- [ ] Reference-type elements (objects with their own serializable fields).
- [ ] Ensure resulting `HashSet` respects `IEqualityComparer<T>` semantics (duplicates removed).
- [ ] Null elements (where allowed) remain `null` after conversion.
- [ ] Nested collections inside elements (test `AllowCollectionNesting`).
- [ ] Unity objects inside a `HashSet<T>`, aiming to verify mapping/instantiation rules.
