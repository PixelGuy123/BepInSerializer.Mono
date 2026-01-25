using System.Collections.Generic;
using UnityEngine;

namespace BepInSerializer.Core.Models;

/// <summary>
/// A pre-computed map of the hierarchy of transforms of a GameObject. Useful for making sure the parent always comes first in the array.
/// </summary>
internal class PrecomputedHierarchyTransformOrder
{
    private record struct OrderVersion(int Order, int Version);
    // Core data structures
    private readonly Dictionary<Transform, OrderVersion> _orderMap;
    private readonly Transform _root;
    private readonly ArrayFactory<Transform> _arrayFactory;
    private readonly List<Transform> _transformChildrenBuffer;
    private int _currentVersion = 0;

    // Change detection state
    private int _lastKnownTransformCount;
    private long _hierarchyChecksum;
    private bool _isDirty = true;

    public PrecomputedHierarchyTransformOrder(Transform root)
    {
        _root = root;
        _orderMap = [];
        _arrayFactory = new();
        _transformChildrenBuffer = new(16);
        ForceRecompute(); // Initial computation
    }

    // ---- Public API ----
    public Transform[] GetOrderedTransforms()
    {
        // Check if hierarchy changed since last access
        if (DetectHierarchyChanges())
        {
            ForceRecompute();
        }

        // Build sorted array from order map
        var buffer = _arrayFactory.RetrieveArray(_orderMap.Count);
        foreach (var kvp in _orderMap)
        {
            if (kvp.Value.Order < buffer.Length)
            {
                buffer[kvp.Value.Order] = kvp.Key;
            }
        }
        return buffer;
    }

    // Get order for specific transform
    public int GetOrder(Transform transform)
    {
        // Check changes before returning any data
        if (DetectHierarchyChanges())
        {
            ForceRecompute();
        }

        return _orderMap.TryGetValue(transform, out var info) ? info.Order : -1;
    }

    // =================== AUTOMATIC CHANGE DETECTION ===================

    private bool DetectHierarchyChanges()
    {
        // Quick check: transform count
        int currentCount = CountAllTransforms();
        if (currentCount != _lastKnownTransformCount)
        {
            // BridgeManager.logger.LogWarning($"Change detected: Transform count changed ({_lastKnownTransformCount} -> {currentCount})");
            return true;
        }

        // lightweight checksum (fast for most cases)
        long quickChecksum = ComputeQuickChecksum();
        if (quickChecksum != _hierarchyChecksum)
        {
            // BridgeManager.logger.LogWarning($"Change detected: Hierarchy checksum mismatch");
            return true;
        }

        _isDirty = true;
        return false;
    }

    private int CountAllTransforms()
    {
        // GetComponentsInChildren is expensive - we cache the result
        if (_isDirty)
        {
            _transformChildrenBuffer.Clear();
            _root.GetComponentsInChildren(true, _transformChildrenBuffer);
            _lastKnownTransformCount = _transformChildrenBuffer.Count;
        }
        return _lastKnownTransformCount;
    }

    private long ComputeQuickChecksum()
    {
        // Create a checksum based on transform properties that change with hierarchy
        long checksum = 0;

        // Breadth-first traversal for consistent order
        Queue<Transform> queue = new();
        queue.Enqueue(_root);

        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();

            unchecked
            {
                // Incorporate transform's properties into checksum
                checksum = (checksum * 397) ^ current.GetInstanceID();
                checksum = (checksum * 397) ^ current.childCount;
            }

            // Add children to queue
            for (int i = 0; i < current.childCount; i++)
            {
                queue.Enqueue(current.GetChild(i));
            }
        }

        return checksum;
    }

    // =================== RECOMPUTATION LOGIC ===================

    private void ForceRecompute()
    {
        _currentVersion++;
        _orderMap.Clear();

        // Build new order map
        int order = 0;
        Queue<Transform> queue = new();
        queue.Enqueue(_root);

        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();

            // Add to order map with current version
            _orderMap[current] = new(order, _currentVersion);
            order++;

            // Enqueue children in sibling order
            for (int i = 0; i < current.childCount; i++)
            {
                queue.Enqueue(current.GetChild(i));
            }
        }

        // Update tracking data
        _lastKnownTransformCount = _orderMap.Count;
        _hierarchyChecksum = ComputeQuickChecksum();
        _isDirty = false;

        // BridgeManager.logger.LogWarning($"Recomputed hierarchy order (version {_currentVersion}, {_orderMap.Count} transforms)");
    }
}