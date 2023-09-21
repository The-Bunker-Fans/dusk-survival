﻿using Robust.Shared.GameStates;

namespace Content.Shared.VendingMachines.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class VendingMachineVisualStateComponent : Component
{
    /// <summary>
    /// RSI state for when the vending machine is unpowered.
    /// Will be displayed on the layer <see cref="VendingMachineVisualLayers.Base"/>
    /// </summary>
    [DataField]
    public string? OffState;

    /// <summary>
    /// RSI state for the screen of the vending machine
    /// Will be displayed on the layer <see cref="VendingMachineVisualLayers.Screen"/>
    /// </summary>
    [DataField]
    public string? ScreenState;

    /// <summary>
    /// RSI state for the vending machine's normal state. Usually a looping animation.
    /// Will be displayed on the layer <see cref="VendingMachineVisualLayers.BaseUnshaded"/>
    /// </summary>
    [DataField]
    public string? NormalState;

    /// <summary>
    /// RSI state for the vending machine's eject animation.
    /// Will be displayed on the layer <see cref="VendingMachineVisualLayers.BaseUnshaded"/>
    /// </summary>
    [DataField]
    public string? EjectState;

    /// <summary>
    /// RSI state for the vending machine's deny animation. Will either be played once as sprite flick
    /// or looped depending on how <see cref="LoopDenyAnimation"/> is set.
    /// Will be displayed on the layer <see cref="VendingMachineVisualLayers.BaseUnshaded"/>
    /// </summary>
    [DataField]
    public string? DenyState;

    /// <summary>
    /// RSI state for when the vending machine is broken.
    /// Will be displayed on the layer <see cref="VendingMachineVisualLayers.Base"/>
    /// </summary>
    [DataField]
    public string? BrokenState;

    /// <summary>
    /// If set to <c>true</c> (default) will loop the animation of the <see cref="DenyState"/> for the duration
    /// of <see cref="VendingMachineEjectComponent.DenyDelay"/>. If set to <c>false</c> will play a sprite
    /// flick animation for the state and then linger on the final frame until the end of the delay.
    /// </summary>
    [DataField]
    public bool LoopDenyAnimation = true;

    public VendingMachineVisualState VisualState;
}
