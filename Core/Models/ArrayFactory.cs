using System.Collections.Generic;

namespace BepInSerializer.Core.Models;

internal class ArrayFactory<T> // Basic Array Factory based on Array Length (used by PrecomputedHierarchyTransformOrder)
{
    private readonly Dictionary<int, T[]> _arrayMap = new(8);

    public T[] RetrieveArray(int length)
    {
        if (_arrayMap.TryGetValue(length, out var array)) return array;
        array = new T[length];
        _arrayMap[length] = array;
        return array;
    }
}