using Content.Shared.CharacterAppearance;
using Content.Shared.Markings;
using Content.Shared.Preferences;

namespace Content.Shared.Humanoid;

/// <summary>
///     HumanoidSystem. Primarily deals with the appearance and visual data
///     of a humanoid entity. HumanoidVisualizer is what deals with actually
///     organizing the sprites and setting up the sprite component's layers.
///
///     This is a shared system, because while it is server authoritative,
///     you still need a local copy so that players can set up their
///     characters.
/// </summary>
public abstract class SharedHumanoidSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public const string DefaultSpecies = "Human";

    public void SetAppearance(EntityUid uid,
        string species,
        Dictionary<HumanoidVisualLayers, SharedHumanoidComponent.CustomBaseLayerInfo> customBaseLayer,
        Color skinColor,
        List<HumanoidVisualLayers> visLayers,
        List<Marking> markings)
    {
        var data = new HumanoidVisualizerData(species, customBaseLayer, skinColor, visLayers, markings);

        // Locally raise an event for this, because there might be some systems interested
        // in this.
        RaiseLocalEvent(uid, new HumanoidAppearanceUpdateEvent(data), true);
        _appearance.SetData(uid, HumanoidVisualizerKey.Key, data);
    }
}

public sealed class HumanoidAppearanceUpdateEvent : EntityEventArgs
{
    public HumanoidVisualizerData Data { get; }

    public HumanoidAppearanceUpdateEvent(HumanoidVisualizerData data)
    {
        Data = data;
    }
}
