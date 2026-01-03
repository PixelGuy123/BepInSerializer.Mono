using System;
using System.Collections.Generic;
using UnityEngine;
using UnitySerializationBridge.Interfaces;

namespace UnitySerializationBridge.Test;

public class TestComponentToSerialize : MonoBehaviour
{
    [SerializeField]
    private SerializableComponent component;

    [SerializeField]
    private GenericSerializableComponent<string> stringComponent;

    static bool shallInstantiate = true;

    void Start()
    {
        if (!shallInstantiate) return;

        component = new()
        {
            value = 2,
            str = "Hello!",
            subComp = new()
            {
                value = 40,
                str = "WELCOME TO"
            },
            genComp = new()
            {
                Value = new()
                {
                    value = 1240891,
                    str = "djiandisoadsa"
                },
                anotherValue = 99
            },
            secondGenComp = new()
            {
                Value = "Testing string",
                anotherValue = 99,
                component = new()
                {
                    testArray = ["Sewe", "afdassa", "iiiio", "128312908"],
                    Value = true,
                    anotherValue = 5322346,
                    component = [
                        new()
                    {
                        Value = "Something else to be tested",
                        anotherValue = 999124
                    },
                    new()
                    {
                        Value = "Something els",
                        anotherValue = 2254223
                    },
                    new()
                    {
                        Value = "Sometadasdasdsadsadsad\\",
                        anotherValue = 241211251
                    },
                    new()
                    {
                        Value = "Haha you got joked els{\\}",
                        anotherValue = 1111231
                    }],
                    listComponents = [
                        new()
                    {
                        Value = "Something else to be tested",
                        anotherValue = 999124
                    },
                    new()
                    {
                        Value = "Something els",
                        anotherValue = 2254223
                    },
                    new()
                    {
                        Value = "Sometadasdasdsadsadsad\\",
                        anotherValue = 241211251
                    },
                    new()
                    {
                        Value = "Haha you got joked els{\\}",
                        anotherValue = 1111231
                    }],
                    hashComponents = [
                        new()
                    {
                        Value = "Something else to be tested",
                        anotherValue = 999124
                    },
                    new()
                    {
                        Value = "Something els",
                        anotherValue = 2254223
                    },
                    new()
                    {
                        Value = "Sometadasdasdsadsadsad\\",
                        anotherValue = 241211251
                    },
                    new()
                    {
                        Value = "Haha you got joked els{\\}",
                        anotherValue = 1111231
                    }],
                    dictoComponents = new()
                    {
                        {"Keying", new() { Value = "Silly joke!", anotherValue = -99 }}
                    }
                }
            }
        };

        stringComponent = new()
        {
            Value = "Hello guys, welcome to my video",
            anotherValue = 8194
        };

        // Instantiates back to see if it copies
        shallInstantiate = false;
        Instantiate(this);
    }
}

[Serializable]
class GenericSerializableComponent<T> : IAutoSerializable
{
    public T Value;
    public int anotherValue = -5;
    public SecondGenericSerializableComponent<bool> component;
}

[Serializable]
class SecondGenericSerializableComponent<T> : IAutoSerializable
{
    public T Value;
    public int anotherValue = -5;
    public string[] testArray;
    public ThirdGenericSerializableComponent<string>[] component;
    public List<ThirdGenericSerializableComponent<string>> listComponents;
    public HashSet<ThirdGenericSerializableComponent<string>> hashComponents;
    public Dictionary<string, ThirdGenericSerializableComponent<string>> dictoComponents;
}

[Serializable]
class ThirdGenericSerializableComponent<T> : IAutoSerializable
{
    public T Value;
    public int anotherValue = -5;
}


[Serializable]
class SerializableComponent : IAutoSerializable
{
    public int value = -1;
    public string str = "This is a default string";
    public SubSerializableComponent subComp = null;
    public GenericSerializableComponent<SubSerializableComponent> genComp = null;
    public GenericSerializableComponent<string> secondGenComp = null;
}

[Serializable]
class SubSerializableComponent : IAutoSerializable
{
    public int value = -99;
    public string str = "HAAAA string";
}