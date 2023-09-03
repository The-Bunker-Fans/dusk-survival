using Content.Server.Nutrition.Components;
using Content.Server.Popups;
using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Nutrition.EntitySystems
{
    /// <summary>
    /// Handles usage of the utensils on the food items
    /// </summary>
    internal sealed partial class UtensilSystem : EntitySystem
    {
        [Dependency] private IRobustRandom _robustRandom = default!;
        [Dependency] private FoodSystem _foodSystem = default!;
        [Dependency] private PopupSystem _popupSystem = default!;
        [Dependency] private SharedInteractionSystem _interactionSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<UtensilComponent, AfterInteractEvent>(OnAfterInteract);
        }

        /// <summary>
        /// Clicked with utensil
        /// </summary>
        private void OnAfterInteract(EntityUid uid, UtensilComponent component, AfterInteractEvent ev)
        {
            if (ev.Target == null || !ev.CanReach)
                return;

            var result = TryUseUtensil(ev.User, ev.Target.Value, component);
            ev.Handled = result.Handled;
        }

        public (bool Success, bool Handled) TryUseUtensil(EntityUid user, EntityUid target, UtensilComponent component)
        {
            if (!EntityManager.TryGetComponent(target, out FoodComponent? food))
                return (false, true);

            //Prevents food usage with a wrong utensil
            if ((food.Utensil & component.Types) == 0)
            {
                _popupSystem.PopupEntity(Loc.GetString("food-system-wrong-utensil", ("food", target), ("utensil", component.Owner)), user, user);
                return (false, true);
            }

            if (!_interactionSystem.InRangeUnobstructed(user, target, popup: true))
                return (false, true);

            return _foodSystem.TryFeed(user, user, target, food);
        }

        /// <summary>
        /// Attempt to break the utensil after interaction.
        /// </summary>
        /// <param name="uid">Utensil.</param>
        /// <param name="userUid">User of the utensil.</param>
        public void TryBreak(EntityUid uid, EntityUid userUid, UtensilComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            if (_robustRandom.Prob(component.BreakChance))
            {
                SoundSystem.Play(component.BreakSound.GetSound(), Filter.Pvs(userUid), userUid, AudioParams.Default.WithVolume(-2f));
                EntityManager.DeleteEntity(component.Owner);
            }
        }
    }
}
