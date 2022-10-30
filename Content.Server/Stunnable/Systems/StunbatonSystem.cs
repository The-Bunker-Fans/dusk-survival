using System.Linq;
using Content.Server.Damage.Components;
using Content.Server.Damage.Events;
using Content.Server.Power.Components;
using Content.Server.Power.Events;
using Content.Server.Speech.EntitySystems;
using Content.Server.Stunnable.Components;
using Content.Server.Weapons.Melee.Events;
using Content.Shared.Audio;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Jittering;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Throwing;
using Content.Shared.Toggleable;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Stunnable.Systems
{
    public sealed class StunbatonSystem : EntitySystem
    {
        [Dependency] private readonly SharedItemSystem _item = default!;
        [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
        [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<StunbatonComponent, UseInHandEvent>(OnUseInHand);
            SubscribeLocalEvent<StunbatonComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<StunbatonComponent, StaminaDamageOnHitAttemptEvent>(OnStaminaHitAttempt);
            SubscribeLocalEvent<StunbatonComponent, ItemMeleeDamageEvent>(OnMeleeHit);
        }

        private void OnMeleeHit(EntityUid uid, StunbatonComponent component, ItemMeleeDamageEvent args)
        {
            if (!component.Activated) return;

            // Don't apply damage if it's activated; just do stamina damage.
            args.BonusDamage -= args.BaseDamage;
        }

        private void OnStaminaHitAttempt(EntityUid uid, StunbatonComponent component, ref StaminaDamageOnHitAttemptEvent args)
        {
            if (!component.Activated ||
                !TryComp<BatteryComponent>(uid, out var battery) || !battery.TryUseCharge(component.EnergyPerUse))
            {
                args.Cancelled = true;
                return;
            }

            args.HitSoundOverride = component.StunSound;

            if (battery.CurrentCharge < component.EnergyPerUse)
            {
                _audioSystem.Play(component.SparksSound, Filter.Pvs(component.Owner, entityManager: EntityManager), uid, AudioParams.Default.WithVariation(0.25f));
                TurnOff(component);
            }
        }

        private void OnUseInHand(EntityUid uid, StunbatonComponent comp, UseInHandEvent args)
        {
            if (comp.Activated)
            {
                TurnOff(comp);
            }
            else
            {
                TurnOn(comp, args.User);
            }
        }

        private void OnExamined(EntityUid uid, StunbatonComponent comp, ExaminedEvent args)
        {
            var msg = comp.Activated
                ? Loc.GetString("comp-stunbaton-examined-on")
                : Loc.GetString("comp-stunbaton-examined-off");
            args.PushMarkup(msg);
            if(TryComp<BatteryComponent>(uid, out var battery))
                args.PushMarkup(Loc.GetString("stunbaton-component-on-examine-charge",
                    ("charge", (int)((battery.CurrentCharge/battery.MaxCharge) * 100))));
        }

        private void TurnOff(StunbatonComponent comp)
        {
            if (!comp.Activated)
                return;

            if (TryComp<AppearanceComponent>(comp.Owner, out var appearance) &&
                TryComp<ItemComponent>(comp.Owner, out var item))
            {
                _item.SetHeldPrefix(comp.Owner, "off", item);
                _appearanceSystem.SetData(appearance.Owner, ToggleVisuals.Toggled, false, appearance);
            }

            _audioSystem.Play(comp.SparksSound, Filter.Pvs(comp.Owner), comp.Owner, AudioParams.Default.WithVariation(0.25f));

            comp.Activated = false;
        }

        private void TurnOn(StunbatonComponent comp, EntityUid user)
        {
            if (comp.Activated)
                return;

            var playerFilter = Filter.Pvs(comp.Owner, entityManager: EntityManager);
            if (!TryComp<BatteryComponent>(comp.Owner, out var battery) || battery.CurrentCharge < comp.EnergyPerUse)
            {
                _audioSystem.Play(comp.TurnOnFailSound, playerFilter, comp.Owner, AudioParams.Default.WithVariation(0.25f));
                _popupSystem.PopupEntity(Loc.GetString("stunbaton-component-low-charge"), user, Filter.Entities(user));
                return;
            }

            if (EntityManager.TryGetComponent<AppearanceComponent>(comp.Owner, out var appearance) &&
                EntityManager.TryGetComponent<ItemComponent>(comp.Owner, out var item))
            {
                _item.SetHeldPrefix(comp.Owner, "on", item);
                _appearanceSystem.SetData(appearance.Owner, ToggleVisuals.Toggled, true, appearance);
            }

            _audioSystem.Play(comp.SparksSound, playerFilter, comp.Owner, AudioParams.Default.WithVariation(0.25f));
            comp.Activated = true;
        }

        private void SendPowerPulse(EntityUid target, EntityUid? user, EntityUid used)
        {
            RaiseLocalEvent(target, new PowerPulseEvent()
            {
                Used = used,
                User = user
            }, false);
        }
    }
}
