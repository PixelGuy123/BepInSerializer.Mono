using System;

namespace BepInSerializer.Core.Serialization.Converters.Models;


// Helper struct to handle the scope lifecycle
internal readonly struct DependencyScope(CircularDependencyDetector detector, object value, Type type = null) : IDependencyScope
{
    private readonly CircularDependencyDetector _detector = detector;
    private readonly object _value = value;
    private readonly Type _type = type;

    public bool DoesScopeContainsType(Type type) => type != null && (_detector?.DependencyContainsType(type) ?? false);
    public void Dispose()
    {
        _detector?.Unregister(_value, _type);
    }
}