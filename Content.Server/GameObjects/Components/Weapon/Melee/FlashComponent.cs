﻿using System.Collections.Generic;
using System.Threading;
using Content.Server.GameObjects.Components.Mobs;
using Content.Server.GameObjects.EntitySystems.Click;
using Content.Server.Interfaces.GameObjects.Components.Interaction;
using Content.Shared.GameObjects.Components.Mobs;
using Content.Shared.Interfaces;
using Robust.Server.GameObjects;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;
using Timer = Robust.Shared.Timers.Timer;

namespace Content.Server.GameObjects.Components.Weapon.Melee
{
    [RegisterComponent]
    public class FlashComponent : MeleeWeaponComponent, IUse, IExamine
    {
#pragma warning disable 649
        [Dependency] private readonly ILocalizationManager _localizationManager;
        [Dependency] private readonly IEntityManager _entityManager;
        [Dependency] private readonly ISharedNotifyManager _notifyManager;
#pragma warning restore 649

        public override string Name => "Flash";

        [ViewVariables(VVAccess.ReadWrite)] private int _flashDuration = 5000;
        [ViewVariables(VVAccess.ReadWrite)] private int _uses = 5;
        [ViewVariables(VVAccess.ReadWrite)] private float _range = 3f;
        [ViewVariables(VVAccess.ReadWrite)] private int _aoeFlashDuration = 5000 / 3;
        [ViewVariables(VVAccess.ReadWrite)] private float _slowTo = 0.75f;
        private bool _flashing;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private int Uses
        {
            get => _uses;
            set
            {
                _uses = value;
                Dirty();
            }
        }

        private bool HasUses => _uses > 0;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataField(ref _flashDuration, "duration", 5000);
            serializer.DataField(ref _uses, "uses", 5);
            serializer.DataField(ref _range, "range", 7f);
            serializer.DataField(ref _aoeFlashDuration, "aoeFlashDuration", _flashDuration / 3);
            serializer.DataField(ref _slowTo, "slowTo", 0.75f);
        }

        protected override bool OnHitEntities(IReadOnlyList<IEntity> entities, AttackEventArgs eventArgs)
        {
            if (entities.Count == 0)
            {
                return false;
            }

            if (!Use(eventArgs.User))
            {
                return false;
            }

            foreach (var entity in entities)
            {
                Flash(entity, eventArgs.User);
            }

            return true;
        }

        public bool UseEntity(UseEntityEventArgs eventArgs)
        {
            if (!Use(eventArgs.User))
            {
                return false;
            }

            foreach (var entity in _entityManager.GetEntitiesInRange(Owner.Transform.GridPosition, _range))
            {
                Flash(entity, eventArgs.User, _aoeFlashDuration);
            }

            return true;
        }

        private bool Use(IEntity user)
        {
            if (HasUses)
            {
                var sprite = Owner.GetComponent<SpriteComponent>();
                if (--Uses == 0)
                {
                    sprite.LayerSetState(0, "burnt");

                    _notifyManager.PopupMessage(Owner, user, "The flash burns out!");
                }
                else if (!_flashing)
                {
                    int animLayer = sprite.AddLayerWithState("flashing");
                    _flashing = true;

                    Timer.Spawn(400, () =>
                    {
                        sprite.RemoveLayer(animLayer);
                        _flashing = false;
                    });
                }

                EntitySystem.Get<AudioSystem>().PlayAtCoords("/Audio/Weapons/flash.ogg", Owner.Transform.GridPosition,
                    AudioParams.Default);

                return true;
            }

            return false;
        }

        private void Flash(IEntity entity, IEntity user)
        {
            Flash(entity, user, _flashDuration);
        }

        // TODO: Check if target can be flashed (e.g. things like sunglasses would block a flash)
        private void Flash(IEntity entity, IEntity user, int flashDuration)
        {
            if (entity.TryGetComponent(out ServerOverlayEffectsComponent overlayEffectsComponent))
            {
                if (!overlayEffectsComponent.TryModifyOverlay(nameof(SharedOverlayID.FlashOverlay),
                    overlay =>
                    {
                        if (overlay.TryGetOverlayParameter<TimedOverlayParameter>(out var timed))
                        {
                            timed.Length += flashDuration;
                        }
                    }))
                {
                    var container = new OverlayContainer(SharedOverlayID.FlashOverlay, new TimedOverlayParameter(flashDuration));
                    overlayEffectsComponent.AddOverlay(container);
                }
            }

            if (entity.TryGetComponent(out StunnableComponent stunnableComponent))
            {
                stunnableComponent.Slowdown(flashDuration / 1000f, _slowTo, _slowTo);
            }

            if (entity != user)
            {
                _notifyManager.PopupMessage(user, entity, $"{user.Name} blinds you with the {Owner.Name}");
            }
        }

        public void Examine(FormattedMessage message, bool inDetailsRange)
        {
            if (!HasUses)
            {
                message.AddText("It's burnt out.");
                return;
            }

            if (inDetailsRange)
            {
                message.AddMarkup(_localizationManager.GetString(
                    $"The flash has [color=green]{Uses}[/color] {_localizationManager.GetPluralString("use", "uses", Uses)} remaining."));
            }
        }
    }
}
