using Content.Shared.Audio;
using Content.Shared.Cabinet;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Player;

namespace Content.Server.Cabinet
{
    public class ItemCabinetSystem : EntitySystem
    {
        [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<ItemCabinetComponent, ComponentInit>(OnComponentInit);
            SubscribeLocalEvent<ItemCabinetComponent, ComponentStartup>(OnComponentStartup);
            SubscribeLocalEvent<ItemCabinetComponent, ComponentShutdown>(OnComponentShutdown);

            SubscribeLocalEvent<ItemCabinetComponent, ActivateInWorldEvent>(OnActivateInWorld);
            SubscribeLocalEvent<ItemCabinetComponent, GetActivationVerbsEvent>(AddToggleOpenVerb);

            SubscribeLocalEvent<ItemCabinetComponent, EntInsertedIntoContainerMessage>(OnContainerModified);
            SubscribeLocalEvent<ItemCabinetComponent, EntRemovedFromContainerMessage>(OnContainerModified);
        }

        private void OnComponentInit(EntityUid uid, ItemCabinetComponent cabinet, ComponentInit args)
        {
            _itemSlotsSystem.AddItemSlot(uid, cabinet.Name, cabinet.CabinetSlot);
        }

        private void OnComponentStartup(EntityUid uid, ItemCabinetComponent cabinet, ComponentStartup args)
        {
            UpdateAppearance(uid, cabinet);
            _itemSlotsSystem.SetLock(uid, cabinet.CabinetSlot.ID, !cabinet.Opened);
        }

        private void OnComponentShutdown(EntityUid uid, ItemCabinetComponent cabinet, ComponentShutdown args)
        {
            _itemSlotsSystem.RemoveItemSlot(uid, cabinet.CabinetSlot);
        }

        private void UpdateAppearance(EntityUid uid,
            ItemCabinetComponent? cabinet = null,
            SharedAppearanceComponent? appearance = null)
        {
            if (!Resolve(uid, ref cabinet, ref appearance, false))
                return;

            appearance.SetData(ItemCabinetVisuals.IsOpen, cabinet.Opened);
            appearance.SetData(ItemCabinetVisuals.ContainsItem, cabinet.CabinetSlot.HasItem);
        }

        private void OnContainerModified(EntityUid uid, ItemCabinetComponent cabinet, ContainerModifiedMessage args)
        {
            if (args.Container.ID == cabinet.CabinetSlot.ID)
                UpdateAppearance(uid, cabinet);
        }

        private void AddToggleOpenVerb(EntityUid uid, ItemCabinetComponent cabinet, GetActivationVerbsEvent args)
        {
            if (args.Hands == null || !args.CanAccess || !args.CanInteract)
                return;

            // Toggle open verb
            Verb toggleVerb = new();
            toggleVerb.Act = () => ToggleItemCabinet(uid, cabinet);
            if (cabinet.Opened)
            {
                toggleVerb.Text = Loc.GetString("verb-categories-close");
                toggleVerb.IconTexture = "/Textures/Interface/VerbIcons/close.svg.192dpi.png";
            }
            else
            {
                toggleVerb.Text = Loc.GetString("verb-categories-open");
                toggleVerb.IconTexture = "/Textures/Interface/VerbIcons/open.svg.192dpi.png";
            }
            args.Verbs.Add(toggleVerb);
        }

        private void OnActivateInWorld(EntityUid uid, ItemCabinetComponent cabinet, ActivateInWorldEvent args)
        {
            if (args.Handled)
                return;

            args.Handled = true;
            ToggleItemCabinet(uid, cabinet);
        }

        /// <summary>
        ///     Toggles the ItemCabinet's state.
        /// </summary>
        private void ToggleItemCabinet(EntityUid uid, ItemCabinetComponent? cabinet = null)
        {
            if (!Resolve(uid, ref cabinet))
                return;

            cabinet.Opened = !cabinet.Opened;
            SoundSystem.Play(Filter.Pvs(uid), cabinet.DoorSound.GetSound(), uid, AudioHelpers.WithVariation(0.15f));
            _itemSlotsSystem.SetLock(uid, cabinet.CabinetSlot.ID, !cabinet.Opened);

            UpdateAppearance(uid, cabinet);
        }
    }
}
