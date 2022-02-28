using Content.Shared.Interaction;
using Content.Shared.PlayingCard;
using Content.Server.Popups;
using Content.Server.Hands.Components;
using Content.Shared.Item;
using Robust.Shared.Player;
using Content.Shared.Examine;
using Robust.Shared.Map;

namespace Content.Server.PlayingCard.EntitySystems;

public sealed class PlayingCardSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly PlayingCardHandSystem _playingCardHandSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayingCardComponent, UseInHandEvent>(OnUseInhand);
        SubscribeLocalEvent<PlayingCardComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PlayingCardComponent, ExaminedEvent>(OnExamined);
    }

    private void OnUseInhand(EntityUid uid, PlayingCardComponent cardComponent, UseInHandEvent args)
    {
        if (args.Handled) return;
        FlipCard(cardComponent);
        args.Handled = true;
    }

    private void OnInteractUsing(EntityUid uid, PlayingCardComponent cardComponent, InteractUsingEvent args)
    {
        CombineCards(uid, args.Used, args.User, cardComponent);
    }

    private void OnExamined(EntityUid uid, PlayingCardComponent cardComponent, ExaminedEvent args)
    {
        if (cardComponent.FacingUp)
            args.PushText(cardComponent.CardName);
    }

    public void CombineCards(EntityUid uid, EntityUid itemUsed, EntityUid user, PlayingCardComponent cardComponent)
    {
        if (!TryComp<PlayingCardComponent>(itemUsed, out PlayingCardComponent? incomingCardComp))
            return;

        if (incomingCardComp.CardDeckID != cardComponent.CardDeckID)
        {
            _popupSystem.PopupEntity(Loc.GetString("playing-card-hand-component-merge-card-id-fail"),
                uid, Filter.Entities(user));
            return;
        }

        if (!TryComp<HandsComponent>(user, out var hands))
            return;

        if (!TryComp<TransformComponent>(cardComponent.Owner, out var transformComp))
        return;

        List<string> cardsToAdd = new(){
            cardComponent.CardName,
            incomingCardComp.CardName
        };

        EntityUid? cardHand =  _playingCardHandSystem.CreateCardHand(cardComponent.CardDeckID, cardsToAdd, cardComponent.CardHandPrototype, cardComponent.PlayingCardPrototype, cardComponent.NoUniqueCardLayers, transformComp.Coordinates);

        if (cardHand == null || !TryComp<SharedItemComponent>(cardHand, out var cardHandEnt))
            return;

        EntityManager.QueueDeleteEntity(itemUsed);
        EntityManager.DeleteEntity(cardComponent.Owner);
        hands.PutInHand(cardHandEnt);
    }

    public void FlipCard(PlayingCardComponent component)
    {
        component.FacingUp = !component.FacingUp;
        if (TryComp<AppearanceComponent>(component.Owner, out AppearanceComponent? appearance))
        {
            appearance.SetData(PlayingCardVisuals.FacingUp, component.FacingUp);
        }
    }

    public EntityUid? CreateCard(string cardDeckID, string cardName, string cardPrototype, bool noUniqueCardLayers, EntityCoordinates coords, bool facingUp = false)
    {
        EntityUid playingCardEnt = EntityManager.SpawnEntity(cardPrototype, coords);
        if (!TryComp<PlayingCardComponent>(playingCardEnt, out PlayingCardComponent? playingCardComp))
        {
            EntityManager.DeleteEntity(playingCardEnt);
            return null;
        }

        playingCardComp.CardDeckID = cardDeckID;
        playingCardComp.CardName = cardName;
        playingCardComp.NoUniqueCardLayers = noUniqueCardLayers;

        if (facingUp)
            FlipCard(playingCardComp);

        if (TryComp<AppearanceComponent>(playingCardEnt, out AppearanceComponent? appearance))
        {
            appearance.SetData(PlayingCardVisuals.CardSprite, cardName);
            appearance.SetData(PlayingCardVisuals.NoUniqueCardLayers, playingCardComp.NoUniqueCardLayers);
        }
        return playingCardEnt;
    }
}
