using System;

namespace BepInSerializer.Core.Serialization.Converters.Models;

/// <summary>
/// The scope from the circular dependency detection system.
/// </summary>
public interface IDependencyScope : IDisposable
{
    /// <summary>
    /// Whether this scope has a type from the context or not (not related to object identification).
    /// </summary>
    /// <param name="type">The type to be checked in this scope.</param>
    /// <returns><see langword="true"/> if the scope does contain such type; otherwise, <see langword="false"/>.</returns>
    public bool DoesScopeContainsType(Type type);
}