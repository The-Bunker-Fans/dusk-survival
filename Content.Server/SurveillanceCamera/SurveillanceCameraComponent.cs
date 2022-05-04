namespace Content.Server.SurveillanceCamera;

public sealed class SurveillanceCameraComponent : Component
{
    // AIs shouldn't be added here,
    // if I can get this one thing to work...
    //
    // The idea is that AIs will instead view cameras similar to
    // how lights work in SS14, where it's a NOT against the
    // dark background, but still have the ability to do multi-cam
    // (n amount multicam, but limited arbitrarily for balance
    // purposes)

    // List of active viewers. This is for bookkeeping purposes,
    // so that when a camera shuts down, any entity viewing it
    // will immediately have their subscription revoked.
    public HashSet<EntityUid> ActiveViewers { get; } = new();

    // Monitors != Viewers, as viewers are entities that are tied
    // to a player session that's viewing from this camera
    //
    // Monitors are grouped sets of viewers, and may be
    // completely different monitor types (e.g., monitor console,
    // AI, etc.)
    public HashSet<EntityUid> ActiveMonitors { get; } = new();

    // This one isn't easy to deal with. Will require a UI
    // to change/set this so mapping these in isn't
    // the most terrible thing possible.
    [ViewVariables(VVAccess.ReadWrite)]
    public string Id { get; } = default!;

    // This should probably be dependent on ApcDeviceNet,
    // which in turn routes to something connected
    // both to the ApcDeviceNet and global DeviceNet
    //
    // something something router boxes
    [ViewVariables(VVAccess.ReadWrite)]
    public string Subnet { get; } = default!;
}
