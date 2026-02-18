# Attributes

> All attributes are located under the namespace: `BepInSerializer.Core.Serialization.Attributes`.

Aside from the serialization feature, the serializer provides a few extra features to make the life of a developer easier when it comes to saving data of _specific data structures_ whose Unity cannot solve natively.

These **C# attributes** are used majorly for specific tasks that'd require an additional `ISerializationCallbackReceiver` to solve the problem. So, consider this a courtesy from the project.

---

## Using `[UseConverter(FieldConverter)]`

**BepInSerializer**'s core feature relies on [Converters](Converters.md), a set of classes specialized to convert a specific set of types in the way Unity would do, which also enables the developer to [implement their own converters](Writing-Converters.md) to solve their specific issues during modding.

By default, Converters have a global list to determine which one to be used first and which to be used last. However, the developer can **force the usage** of one or more converters (including ones which are not registered) through the addition of this attribute to a field.

Here's a snippet of how that works:

```csharp
using BepInSerializer.Core.Serialization.Attributes;

public class MyComponent : MonoBehaviour
{
    [SerializeField]
    [UseConverter(MyDictionaryConverter)] // Test this converter first, then go to the global list
    private Dictionary<string, int> someDictionary = new();

    // Note how you can stack both together
    // The priority system reads top-to-bottom (declaration order)
    [UseConverter(IntConverter)] // First
    [UseConverter(FloatConverter)] // Second
    [SerializeField]
    private float myNumber = 1f;
}
```

## Using `[AllowCollectionNesting]`

Normally, collection nesting is strictly unsupported by Unity. Though, due to how the serializer works internally, **this feature is natively supported**, resolving this problem.

With the purpose to activate this feature for your field, you can simply add `[AllowCollectionNesting]` to your field.

Here's a snippet of that in action:

```csharp
using BepInSerializer.Core.Serialization.Attributes;

public class MyComponent : MonoBehaviour
{
    [SerializeField]
    [AllowCollectionNesting] // See how complex this nesting is? Even that can be serialized!
    private List<string[][]> nestedJaggedArray = new();
}
```
