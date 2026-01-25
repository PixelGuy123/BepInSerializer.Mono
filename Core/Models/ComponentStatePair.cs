using UnityEngine;

namespace BepInSerializer.Core.Models;


internal struct ComponentStatePair()
{
    public Component Component;
    public ComponentSerializationState State
    {
        readonly get => _state;
        set
        {
            _state = value;
            HasStateDefined = value != null;
        }
    }
    public bool HasStateDefined { get; private set; }
    private ComponentSerializationState _state;
}