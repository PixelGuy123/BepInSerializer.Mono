namespace BepInSerializer.Core.Serialization.Interfaces;

/// <summary>
/// A replication of <see cref="UnityEngine.ISerializationCallbackReceiver"/> that is not called by Unity and can be controlled by the serializer.
/// </summary>
/// <remarks>Replace <see cref="UnityEngine.ISerializationCallbackReceiver"/> with this interface whenever you can.</remarks>
public interface ISafeSerializationCallbackReceiver
{
    /// <summary>
    /// Called before the serializer saves the data. Useful to do operations to prepare the data.
    /// </summary>
    void OnBeforeSerialize();
    /// <summary>
    /// Called after the serializer deserializes the clone of the object. Useful to prepare the unpacked data.
    /// </summary>
    void OnAfterDeserialize();
}