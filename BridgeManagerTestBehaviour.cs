using System;
using UnityEngine;

namespace BepInSerializer;

internal class BridgeManagerTestBehaviour : MonoBehaviour
{
    [Serializable]
    internal class MyTestClass
    {
        public int value;
        public string str;
    }

    public MyTestClass myTestClass; // Should be serialized by Unity if allowed by some external source
}