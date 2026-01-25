using System;
using System.Collections.Generic;
using UnityEngine;

namespace BepInSerializer.Core.Models;

/// <summary>
/// Transient state holding the serialized data for a specific component instance.
/// Replaces the runtime overhead of the SerializationHandler component.
/// </summary>
internal class ComponentSerializationState
{
    // Real constructor to get the type of the component
    internal ComponentSerializationState(Component component) : this(component, component.GetType()) { }
    private ComponentSerializationState(Component component, Type type)
    {
        Component = component;
        ComponentType = type;
    }
    public Component Component { get; }
    public Type ComponentType { get; }
    public List<SerializedFieldData> Fields { get; } = new(8);
}
