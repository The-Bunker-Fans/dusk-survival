using Content.Shared.Item;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.Server.Items
{

    public class ItemSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SharedItemComponent, GetInteractionVerbsEvent>(AddPickupVerb);
        }

        private void AddPickupVerb(EntityUid uid, SharedItemComponent component, GetInteractionVerbsEvent args)
        {
            if (!args.CanAccess || args.Hands == null)
                return;

            if (component.CanPickup(args.User))
            {
                Verb verb = new("pickup");
                verb.Act = () => args.Hands.TryPutInActiveHandOrAny(args.Target);
                verb.Text = Loc.GetString("pick-up-verb-get-data-text");
                verb.IconTexture = "/Textures/Interface/VerbIcons/pickup.svg.192dpi.png";
                args.Verbs.Add(verb);
            }
        }
    }
}
