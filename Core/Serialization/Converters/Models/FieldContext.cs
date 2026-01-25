using System;
using System.Reflection;
using BepInSerializer.Utils;
using UnityEngine;

namespace BepInSerializer.Core.Serialization.Converters.Models;

/// <summary>
/// Context information for a field during conversion. This class is your primary way to interact with field data during conversion.
/// </summary>
public partial class FieldContext
{
    // ----- Public API -----
    /// <summary>
    /// Creates a new instance of the <see cref="FieldContext"/> class with original context.
    /// </summary>
    /// <param name="originalContext">The original context used from a previous conversion iteration.</param>
    /// <param name="fieldInfo">The current field to be held by.</param>
    /// <returns>Returns a context based on the field passed.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static FieldContext CreateSubContext(FieldContext originalContext, FieldInfo fieldInfo)
    {
        if (originalContext == null) throw new ArgumentNullException(nameof(originalContext));
        return new FieldContext
        {
            OriginalContext = originalContext,
            ValueType = fieldInfo.FieldType,
            OriginalValue = fieldInfo.CreateFieldGetter()(originalContext.OriginalValue),
            ContainsSerializeReference = fieldInfo.IsDefined(typeof(SerializeReference)),
            CircularDependencyDetector = originalContext.CircularDependencyDetector
        };
    }
    /// <summary>
    /// Creates a new instance of the <see cref="FieldContext"/> class for a property.
    /// </summary>
    /// <param name="originalContext">The original context used from a previous conversion iteration.</param>
    /// <param name="propInfo">The property to be used in the context.</param>
    /// <returns>Returns a context based on the property passed.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <remarks>This is useful for creating contexts for properties from Unity types (or from any other type in general).</remarks>
    public static FieldContext CreateSubContext(FieldContext originalContext, PropertyInfo propInfo)
    {
        if (originalContext == null) throw new ArgumentNullException(nameof(originalContext));
        var getter = propInfo.CreatePropertyGetter() ?? throw new ArgumentException("Property must have a getter method.", nameof(propInfo));
        var originalValue = getter(originalContext.OriginalValue);
        return new FieldContext
        {
            OriginalContext = originalContext,
            ValueType = originalValue.GetType(),
            OriginalValue = originalValue,
            ContainsSerializeReference = propInfo.IsDefined(typeof(SerializeReference)),
            CircularDependencyDetector = originalContext.CircularDependencyDetector
        };
    }
    /// <summary>
    /// Creates a new instance of the <see cref="FieldContext"/> class for a remote value.
    /// </summary>
    /// <param name="originalContext">The original context used from a previous conversion iteration.</param>
    /// <param name="originalValue">The ORIGINAL value of the original context to be used in the new context.</param>
    /// <param name="originalType">A backup type to be used as reference in case originalValue is null. Default Type is null.</param>
    /// <returns>Returns a context tied to its original value.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <remarks>This is useful for creating contexts for values that are not directly tied to a specific field, such as elements within a collection.</remarks>
    public static FieldContext CreateRemoteContext(FieldContext originalContext, object originalValue, Type originalType = null)
    {
        if (originalContext == null) throw new ArgumentNullException(nameof(originalContext));
        return new FieldContext
        {
            OriginalContext = originalContext,
            ValueType = originalValue?.GetType() ?? originalType,
            OriginalValue = originalValue,
            ContainsSerializeReference = originalContext.ContainsSerializeReference,
            CircularDependencyDetector = originalContext.CircularDependencyDetector
        };
    }

    /// <summary>
    /// Attempts to enter a dependency tracking scope for the current value.
    /// </summary>
    /// <param name="scope">A disposable scope. If <see langword="true"/> is returned, this must be disposed when the conversion of this value is complete.</param>
    /// <returns><see langword="true"/> if the value is safe to process (not circular); <see langword="false"/> if a circular dependency is detected.</returns>
    public bool TryBeginDependencyScope(out IDependencyScope scope)
    {
        if (CircularDependencyDetector.HasCircularDependency(OriginalValue, ValueType))
        {
            scope = null;
            return false;
        }

        scope = new DependencyScope(CircularDependencyDetector, OriginalValue, ValueType);
        return true;
    }

    /// <summary>
    /// The current original value of the field in this context.
    /// </summary>
    public object OriginalValue { get; private set; }
    /// <summary>
    /// The type of the field in this context.
    /// </summary>
    public Type ValueType { get; private set; }
    /// <summary>
    /// The previous original value of a field in a prior context.
    /// </summary>
    public object PreviousOriginalValue => OriginalContext?.OriginalValue;
    /// <summary>
    /// The previous typeof a field in a prior context.
    /// </summary>
    public Type PreviousValueType => OriginalContext?.ValueType;
    /// <summary>
    /// Indicates whether the field has the <see cref="SerializeReference"/> attribute.
    /// </summary>
    public bool ContainsSerializeReference { get; private set; }

    // ----- Internal API ------
    /// <summary>
    /// Creates a new instance of the <see cref="FieldContext"/> class without original context.
    /// </summary>
    /// <param name="fieldInfo">The current field to be held by.</param>
    /// <param name="originalValue">The current value of this field.</param>
    /// <returns>Returns a context with a field and its original value.</returns>
    internal static FieldContext CreatePrimaryContext(FieldInfo fieldInfo, object originalValue)
    {
        return new FieldContext
        {
            ValueType = fieldInfo.FieldType,
            OriginalValue = originalValue,
            ContainsSerializeReference = fieldInfo.IsDefined(typeof(SerializeReference)),
            CircularDependencyDetector = new CircularDependencyDetector()
        };
    }

    /// <summary>
    /// The circular dependency detector for this conversion process.
    /// </summary>
    internal CircularDependencyDetector CircularDependencyDetector { get; private set; }
    /// <summary>
    /// The original context used before any conversions were applied.
    /// </summary>
    internal FieldContext OriginalContext { get; private set; }

    // ----- Private API -----
    private FieldContext() { } // Only accessible through static constructors
}