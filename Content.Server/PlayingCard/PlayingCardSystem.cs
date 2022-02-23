using Content.Shared.Interaction;
using Content.Shared.PlayingCard;
using Content.Server.Popups;
using Content.Server.Hands.Components;
using Content.Shared.Item;
using Robust.Shared.Player;

namespace Content.Server.PlayingCard.EntitySystems;

public sealed class PlayingCardSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<PlayingCardComponent, UseInHandEvent>(OnUseInhand);
        SubscribeLocalEvent<PlayingCardComponent, InteractUsingEvent>(OnInteractUsing);

    }

    private void OnUseInhand(EntityUid uid, PlayingCardComponent cardComponent, UseInHandEvent args)
    {
        if (args.Handled) return;
        FlipCard(cardComponent, args.User);
        args.Handled = true;
    }

    private void OnInteractUsing(EntityUid uid, PlayingCardComponent cardComponent, InteractUsingEvent args)
    {
        CombineCards(uid, args.Used, args.User, cardComponent);
    }

    public void CombineCards(EntityUid uid, EntityUid itemUsed, EntityUid user, PlayingCardComponent cardComponent)
    {
        if (TryComp<PlayingCardComponent>(itemUsed, out PlayingCardComponent? incomingCardComp))
            {

                if (incomingCardComp.StackTypeId != cardComponent.StackTypeId)
                {
                    _popupSystem.PopupEntity(Loc.GetString("playing-card-hand-component-merge-card-id-fail"),
                        uid, Filter.Entities(uid));
                    return;
                }

                if (!TryComp<HandsComponent>(user, out var hands))
                    return;

                if (!TryComp<TransformComponent>(cardComponent.Owner, out var transformComp))
                return;

                EntityUid cardHand = Spawn(cardComponent.CardHandPrototype, transformComp.Coordinates);
                // ADD LIST OF CARDS
                if (!TryComp<SharedItemComponent>(cardHand, out var cardHandEnt))
                    return;

                EntityManager.QueueDeleteEntity(itemUsed);
                EntityManager.QueueDeleteEntity(cardComponent.Owner);
                hands.PutInHand(cardHandEnt);
            }
    }

    private void FlipCard(PlayingCardComponent component, EntityUid user)
    {
        if (component.FacingUp)
        {
            component.FacingUp = false;
            if (TryComp<AppearanceComponent>(component.Owner, out AppearanceComponent? appearance))
            {
                appearance.SetData(PlayingCardVisuals.FacingUp, false);
            }
            // use improper name
            // use improper description
        }
        else
        {
            component.FacingUp = true;
            if (TryComp<AppearanceComponent>(component.Owner, out AppearanceComponent? appearance))
            {
                appearance.SetData(PlayingCardVisuals.FacingUp, true);
            }
            // assign proper name
            // assign proper description
        }
    }
}
