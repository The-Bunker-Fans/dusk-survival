﻿using Robust.Shared.Serialization;

namespace Content.Shared.Ame;

[Virtual]
public partial class SharedAmeControllerComponent : Component
{
}

[Serializable, NetSerializable]
public sealed class AmeControllerBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly bool HasPower;
    public readonly bool IsMaster;
    public readonly bool Injecting;
    public readonly bool HasFuelJar;
    public readonly int FuelAmount;
    public readonly int InjectionAmount;
    public readonly int CoreCount;
    public readonly bool SafteyProtocols;
    public readonly bool OverloadWarning;

    public AmeControllerBoundUserInterfaceState(bool hasPower, bool isMaster, bool injecting, bool hasFuelJar, int fuelAmount, int injectionAmount, int coreCount, bool safteyProtocols, bool overloadWarning)
    {
        HasPower = hasPower;
        IsMaster = isMaster;
        Injecting = injecting;
        HasFuelJar = hasFuelJar;
        FuelAmount = fuelAmount;
        InjectionAmount = injectionAmount;
        CoreCount = coreCount;
        SafteyProtocols = safteyProtocols;
        OverloadWarning = overloadWarning;

    }
}

[Serializable, NetSerializable]
public sealed class UiButtonPressedMessage : BoundUserInterfaceMessage
{
    public readonly UiButton Button;

    public UiButtonPressedMessage(UiButton button)
    {
        Button = button;
    }
}

[Serializable, NetSerializable]
public enum AmeControllerUiKey
{
    Key
}

public enum UiButton
{
    Eject,
    ToggleInjection,
    IncreaseFuel,
    DecreaseFuel,
}

[Serializable, NetSerializable]
public enum AmeControllerVisuals
{
    DisplayState,
}

[Serializable, NetSerializable]
public enum AmeControllerState
{
    On,
    Critical,
    Fuck,
    Off,
}
