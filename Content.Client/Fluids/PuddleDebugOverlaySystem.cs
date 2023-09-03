﻿using System.Collections;
using Content.Shared.Fluids;
using Robust.Client.Graphics;

namespace Content.Client.Fluids;

public sealed partial class PuddleDebugOverlaySystem : SharedPuddleDebugOverlaySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    public readonly Dictionary<EntityUid, PuddleOverlayDebugMessage> TileData = new();
    private PuddleOverlay? _overlay;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PuddleOverlayDisableMessage>(DisableOverlay);
        SubscribeNetworkEvent<PuddleOverlayDebugMessage>(RenderDebugData);
    }

    private void RenderDebugData(PuddleOverlayDebugMessage message)
    {
        TileData[message.GridUid] = message;
        if (_overlay != null)
            return;

        _overlay = new PuddleOverlay();
        _overlayManager.AddOverlay(_overlay);
    }

    private void DisableOverlay(PuddleOverlayDisableMessage message)
    {
        TileData.Clear();
        if (_overlay == null)
            return;

        _overlayManager.RemoveOverlay(_overlay);
        _overlay = null;
    }

    public PuddleDebugOverlayData[] GetData(EntityUid mapGridGridEntityId)
    {
        return TileData[mapGridGridEntityId].OverlayData;
    }
}
