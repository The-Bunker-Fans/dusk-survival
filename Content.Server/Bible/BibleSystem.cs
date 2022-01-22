using Robust.Shared.GameObjects;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Damage;
using Content.Server.Inventory;
using Content.Server.Mind.Components;
using Content.Server.Bible.Components;
using Robust.Shared.IoC;
using Robust.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Player;


namespace Content.Server.Bible
{
    public class BibleSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<BibleComponent, AfterInteractEvent>(OnAfterInteract);
        }

        private void OnAfterInteract(EntityUid uid, BibleComponent component, AfterInteractEvent args)
        {
            var invSystem = EntitySystem.Get<InventorySystem>();
            var random = IoCManager.Resolve<IRobustRandom>();


            if (!EntityManager.HasComponent<BibleUserComponent>(args.User))
            {
                return;
            }
            if (args.Target == null)
            {
                return;
            }

            if (!invSystem.TryGetSlotEntity(args.Target.Value, "head", out var entityUid))
            {
                if (random.Prob(0.34f))
                {
                SoundSystem.Play(Filter.Pvs(args.Target.Value), "/Audio/Effects/hit_kick.ogg");
                EntitySystem.Get<DamageableSystem>().TryChangeDamage(args.Target.Value, component.DamageOnFail, true);
                return;
                }
            }
            SoundSystem.Play(Filter.Pvs(args.Target.Value), "/Audio/Effects/holy.ogg");
            EntitySystem.Get<DamageableSystem>().TryChangeDamage(args.Target.Value, component.Damage, true);
        }

    }
}
