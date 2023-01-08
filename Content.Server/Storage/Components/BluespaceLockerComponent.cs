﻿using System.Threading;

namespace Content.Server.Storage.Components;

[RegisterComponent]
public sealed class BluespaceLockerComponent : Component
{
    /// <summary>
    /// Determines if gas will be transported.
    /// </summary>
    [DataField("transportGas"), ViewVariables(VVAccess.ReadWrite)]
    public bool TransportGas = true;

    /// <summary>
    /// Determines if entities will be transported.
    /// </summary>
    [DataField("transportEntities"), ViewVariables(VVAccess.ReadWrite)]
    public bool TransportEntities = true;

    /// <summary>
    /// Determines if entities with a Mind component will be transported.
    /// </summary>
    [DataField("transportSentient"), ViewVariables(VVAccess.ReadWrite)]
    public bool TransportSentient = true;

    /// <summary>
    /// If length > 0, when something is added to the storage, it will instead be teleported to a random storage
    /// from the list and the other storage will be opened.
    /// </summary>
    [DataField("bluespaceLinks"), ViewVariables(VVAccess.ReadOnly)]
    public HashSet<EntityUid> BluespaceLinks = new();

    /// <summary>
    /// Each time the system attempts to get a link, it will link additional lockers to ensure the minimum amount
    /// are linked.
    /// </summary>
    [DataField("minBluespaceLinks"), ViewVariables(VVAccess.ReadWrite)]
    public uint MinBluespaceLinks;

    /// <summary>
    /// Determines if links automatically added are restricted to the same map
    /// </summary>
    [DataField("pickLinksFromSameMap"), ViewVariables(VVAccess.ReadWrite)]
    public bool PickLinksFromSameMap;

    /// <summary>
    /// Determines if links automatically added must have ResistLockerComponent
    /// </summary>
    [DataField("pickLinksFromResistLockers"), ViewVariables(VVAccess.ReadWrite)]
    public bool PickLinksFromResistLockers = true;

    /// <summary>
    /// Determines if links automatically added are restricted to being on a station
    /// </summary>
    [DataField("pickLinksFromStationGrids"), ViewVariables(VVAccess.ReadWrite)]
    public bool PickLinksFromStationGrids = true;

    /// <summary>
    /// Determines if links automatically added are restricted to having the same access
    /// </summary>
    [DataField("pickLinksFromSameAccess"), ViewVariables(VVAccess.ReadWrite)]
    public bool PickLinksFromSameAccess = true;

    /// <summary>
    /// Delay in seconds to wait after closing before transporting
    /// </summary>
    [DataField("delay"), ViewVariables(VVAccess.ReadWrite)]
    public int Delay;
    public CancellationTokenSource? CancelToken;

    /// <summary>
    /// Determines if links automatically added are bidirectional
    /// </summary>
    [DataField("autoLinksBidirectional"), ViewVariables(VVAccess.ReadWrite)]
    public bool AutoLinksBidirectional;
}
