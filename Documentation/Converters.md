# Converters

> This document explains about converters at a superficial level and how they are used by **BepInSerializer**.
> If you're aware of how converters work and wishes to know _how to write_ one yourself, refer to the [**Writing Converters**](Writing-Converters.md) documentation.
>
> Everything converter-related lives under the following namespaces: `BepInSerializer.Core.Serialization`, `BepInSerializer.Core.Serialization.Converters` and `BepInSerializer.Core.Serialization.Converters.Models`.

## 🔧 What is a Converter?

Converters are small, focused components that tell the serializer how to create and populate values for specific field types. They derive from `FieldConverter` and implement two methods: `CanConvert(FieldContext)` to declare which fields they handle, and `Convert(FieldContext)` to produce the converted value.

These converters are used by the system in a prioritized loop. You can provide converters globally (via `ConversionRegistry.RegisterConverter`) or per-field using the [`UseConverter`](Attributes.md) attribute.

---

## ⚙️ Built-in converters

### Global Converters

These converters are already built-in into **BepInSerializer** and will be used during conversion by default. The converters are:

- `ClassConverter` – default handler for regular classes.
- `StructConverter` – handler for value types.
- `PseudoStructConverter` – Unity "pseudo-struct" classes (e.g., `AnimationCurve`).
- `UnityObjectConverter` – `UnityEngine.Object` and components.
- `ArrayConverter` – multi-dimensional and single arrays.
- `ListConverter` – `List<T>`.
- `StringConverter` – strings.

These converters are listed in order from least prioritized to the most prioritized; the last converter will be the first to resolve a field's value.

### Local Converters

These converters (currently one available) are not used by the serializer by default, being necessary to trigger them using [**`UseConverter(FieldConverter)`**](Attributes.md#using-[useconverter]fieldconverter). The converters are:

- `DictionaryConverter` – `Dictionary<TKey, TValue>`.

---

## 🔍 How converters are chosen

- **Per-field priority:** converters provided by the `UseConverter` attribute on a field are tried first.
- **Global priority:** converters registered with `ConversionRegistry.RegisterConverter` are tried afterwards (later-registered converters have higher priority in the registry loop).

Converter implementations should be literal: return `false` in `CanConvert` for types they don't truly support, and return `null` from `Convert` if conversion cannot be performed.
