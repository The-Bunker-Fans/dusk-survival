﻿using System.Globalization;
using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Chat.V2.Censorship;
using Content.Server.Speech.EntitySystems;
using Content.Shared.CCVar;
using Content.Shared.Chat.V2;
using Content.Shared.Chat.V2.Components;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Replays;
using Robust.Shared.Utility;
using SixLabors.ImageSharp.Processing;

namespace Content.Server.Chat.V2;

public sealed partial class ChatSystem
{
    public void InitializeLocalChat()
    {
        // A client attempts to chat using a given entity
        SubscribeNetworkEvent<LocalChatAttemptedEvent>((msg, args) => { HandleAttemptLocalChatMessage(args.SenderSession, msg.Speaker, msg.Message); });
    }

    private void HandleAttemptLocalChatMessage(ICommonSession player, NetEntity entity, string message)
    {
        var entityUid = GetEntity(entity);

        if (player.AttachedEntity != entityUid)
        {
            // Nice try bozo.
            return;
        }

        // Are they rate-limited
        if (IsRateLimited(entityUid, out var reason))
        {
            RaiseNetworkEvent(new LocalChatAttemptFailedEvent(entity, reason), player);

            return;
        }

        // Sanity check: if you can't chat you shouldn't be chatting.
        if (!TryComp<LocalChattableComponent>(entityUid, out var chattable))
        {
            RaiseNetworkEvent(new LocalChatAttemptFailedEvent(entity, "You can't chat"), player);

            return;
        }

        var maxMessageLen = _configuration.GetCVar(CCVars.ChatMaxMessageLength);

        // Is the message too long?
        if (message.Length > _configuration.GetCVar(CCVars.ChatMaxMessageLength))
        {
            RaiseNetworkEvent(
                new LocalChatAttemptFailedEvent(
                    entity,
                    Loc.GetString("chat-manager-max-message-length-exceeded-message", ("limit", maxMessageLen))
                    ),
                player);

            return;
        }

        // All good; let's actually send a chat message.
        SendLocalChatMessage(entityUid, message, chattable.Range);
    }

    /// <summary>
    /// Try to end a chat in Local.
    /// </summary>
    /// <param name="entityUid">The entity who is chatting</param>
    /// <param name="message">The message to send. This will be mutated with accents, to remove tags, etc.</param>
    /// <param name="asName">Override the name this entity will appear as.</param>
    /// <param name="hideInChatLog">Should the chat message be hidden in the log?</param>
    public bool TrySendLocalChatMessage(EntityUid entityUid, string message, string asName = "", bool hideInChatLog = false)
    {
        if (!TryComp<LocalChattableComponent>(entityUid, out var chat))
            return false;

        SendLocalChatMessage(entityUid, message, chat.Range, asName, hideInChatLog);

        return true;
    }

    /// <summary>
    /// Send a chat in Local.
    /// </summary>
    /// <param name="entityUid">The entity who is chatting</param>
    /// <param name="message">The message to send. This will be mutated with accents, to remove tags, etc.</param>
    /// <param name="range">The range the audio can be heard in</param>
    /// <param name="asName">Override the name this entity will appear as.</param>
    /// <param name="hideInLog">Should the chat message be hidden in the log?</param>
    public void SendLocalChatMessage(EntityUid entityUid, string message, float range, string asName = "", bool hideInLog = false)
    {
        message = SanitizeInCharacterMessage(entityUid,message,out var emoteStr);

        if (emoteStr?.Length > 0)
        {
            TrySendEmoteMessage(entityUid, emoteStr, asName, true);
        }

        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        // Mitigation for exceptions such as https://github.com/space-wizards/space-station-14/issues/24671
        try
        {
            message = FormattedMessage.RemoveMarkup(message);
        }
        catch (Exception e)
        {
            _logger.GetSawmill("chat").Error($"UID {entityUid} attempted to send {message} {(asName.Length > 0 ? "as name, " : "")} but threw a parsing error: {e}");

            return;
        }

        message = TransformSpeech(entityUid, FormattedMessage.RemoveMarkup(message));

        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        if (string.IsNullOrEmpty(asName))
        {
            asName = GetSpeakerName(entityUid);
        }

        var verb = GetSpeechVerb(entityUid, message);

        var name = FormattedMessage.EscapeText(asName);

        var nameColor = "";

        // color the name unless it's something like "the old man"
        if (!TryComp<GrammarComponent>(entityUid, out var grammar) || grammar.ProperNoun == true)
            nameColor = GetNameColor(name);

        var msgOut = new EntityLocalChattedEvent(
            GetNetEntity(entityUid),
            name,
            Loc.GetString(_random.Pick(verb.SpeechVerbStrings)),
            verb.FontId,
            verb.FontSize,
            verb.Bold,
            nameColor,
            message,
            range,
            hideInLog
        );

        // Make sure anything server-side hears about the message
        // TODO: what does broadcasting even do
        RaiseLocalEvent(entityUid, msgOut, true);

        // Now fire it off to legal recipients
        foreach (var session in GetLocalChatRecipients(entityUid, range))
        {
            RaiseNetworkEvent(msgOut, session);
        }

        // And finally, stash it in the replay and log.
        _replay.RecordServerMessage(msgOut);
        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Say from {ToPrettyString(entityUid):user} as {asName}: {message}");
    }

    public void SendSubtleLocalChatMessage(ICommonSession source, ICommonSession target, string message)
    {
        // We need the default verb.
        var verb = GetSpeechVerb(EntityUid.Invalid, message);

        var msgOut = new EntitySubtleLocalChattedEvent(
            GetNetEntity(EntityUid.Invalid),
            verb.FontId,
            verb.FontSize,
            verb.Bold,
            message,
            false
        );

        RaiseNetworkEvent(msgOut, target);

        _adminLogger.Add(LogType.AdminMessage, LogImpact.Low, $"{ToPrettyString(target.AttachedEntity):player} received subtle message from {source.Name}: {message}");
    }

    private List<ICommonSession> GetLocalChatRecipients(EntityUid source, float range)
    {
        var recipients = new List<ICommonSession>();

        var ghostHearing = GetEntityQuery<GhostHearingComponent>();
        var xforms = GetEntityQuery<TransformComponent>();

        var transformSource = xforms.GetComponent(source);
        var sourceMapId = transformSource.MapID;
        var sourceCoords = transformSource.Coordinates;

        foreach (var player in _playerManager.Sessions)
        {
            if (player.AttachedEntity is not { Valid: true } playerEntity)
                continue;

            var transformEntity = xforms.GetComponent(playerEntity);

            if (transformEntity.MapID != sourceMapId)
                continue;

            // even if they are a ghost hearer, in some situations we still need the range
            if (ghostHearing.HasComponent(playerEntity) || sourceCoords.TryDistance(EntityManager, transformEntity.Coordinates, out var distance) && distance < range)
                recipients.Add(player);
        }

        return recipients;
    }
}
