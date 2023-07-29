using Content.Shared.Damage;


// Damages the held item by a set amount when it hits someone. Can be used to make melee items limited-use.
namespace Content.Server.Damage.Components;

[RegisterComponent]
public sealed class DamageOnHitComponent : Component
{
    [DataField("resistanceReductionValue")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float? ResistanceReductionValue = 1f;

    [DataField("damage", required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier Damage = default!;
}

