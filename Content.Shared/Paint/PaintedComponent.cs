using Robust.Shared.GameStates;
using Content.Shared.Decals;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Audio;

namespace Content.Shared.Paint;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PaintedComponent : Component
{

    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public Color Color = Color.FromHex("#2cdbd5");

    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField, AutoNetworkedField]
    public string ShaderName = "Greyscale";

    [DataField, AutoNetworkedField]
    public Color BeforePaintedColor;

    [DataField, AutoNetworkedField]
    public string ShaderRemove = "";

}

