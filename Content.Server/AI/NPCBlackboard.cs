using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Utility;

namespace Content.Server.AI;

[DataDefinition]
public sealed class NPCBlackboard
{
    private static readonly Dictionary<string, object> BlackboardDefaults = new()
    {
        {"MaximumIdleTime", 7f},
        {"MinimumIdleTime", 2f},
        {"VisionRadius", 7f},
        {"MeleeRange", 1f},
    };

    private Dictionary<string, object> _blackboard = new();

    /// <summary>
    /// Should we allow setting values on the blackboard. This is true when we are planning.
    /// <remarks>
    /// The effects get stored separately so they can potentially be re-applied during execution.
    /// </remarks>
    /// </summary>
    public bool ReadOnly = false;

    public NPCBlackboard ShallowClone()
    {
        var dict = new NPCBlackboard();
        foreach (var item in _blackboard)
        {
            dict.SetValue(item.Key, item.Value);
        }
        return dict;
    }

    public bool ContainsKey(string key)
    {
        return _blackboard.ContainsKey(key);
    }

    /// <summary>
    /// Get the blackboard data for a particular key.
    /// </summary>
    public T GetValue<T>(string key)
    {
        return (T) _blackboard[key];
    }

    /// <summary>
    /// Tries to get the blackboard data for a particular key. Returns default if not found
    /// </summary>
    public T? GetValueOrDefault<T>(string key)
    {
        if (_blackboard.TryGetValue(key, out var value))
        {
            return (T) value;
        }

        if (TryGetEntityDefault(key, out value))
        {
            return (T) value;
        }

        if (BlackboardDefaults.TryGetValue(key, out value))
        {
            return (T) value;
        }

        return default;
    }

    /// <summary>
    /// Tries to get the blackboard data for a particular key.
    /// </summary>
    public bool TryGetValue<T>(string key, [NotNullWhen(true)] out T? value)
    {
        if (_blackboard.TryGetValue(key, out var data))
        {
            value = (T) data;
            return true;
        }

        if (TryGetEntityDefault(key, out data))
        {
            value = (T) data;
            return true;
        }

        if (BlackboardDefaults.TryGetValue(key, out data))
        {
            value = (T) data;
            return true;
        }

        value = default;
        return false;
    }

    public void SetValue(string key, object value)
    {
        if (ReadOnly)
        {
            AssertReadonly();
            return;
        }

        _blackboard[key] = value;
    }

    private void AssertReadonly()
    {
        DebugTools.Assert(false, $"Tried to write to an NPC blackboard that is readonly!");
    }

    private bool TryGetEntityDefault(string key, [NotNullWhen(true)] out object? value, IEntityManager? entManager = null)
    {
        // TODO: Pass this in
        IoCManager.Resolve(ref entManager);
        value = default;

        switch (key)
        {
            case OwnerCoordinates:
                if (!TryGetValue<EntityUid>(Owner, out var owner))
                {
                    return false;
                }

                if (entManager.TryGetComponent<TransformComponent>(owner, out var xform))
                {
                    value = xform.Coordinates;
                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    public bool Remove<T>(string key)
    {
        DebugTools.Assert(!_blackboard.ContainsKey(key) || _blackboard[key] is T);
        return _blackboard.Remove(key);
    }

    /*
    * Constants to make development easier
    */

    public const string Owner = "Owner";
    public const string OwnerCoordinates = "OwnerCoordinates";
    public const string MovementTarget = "MovementTarget";
    public const string VisionRadius = "VisionRadius";
    public const float MeleeRange = 1f;
}
