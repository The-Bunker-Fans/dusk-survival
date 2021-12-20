﻿#nullable enable
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Utility;

namespace Content.Server.Administration
{
    [UsedImplicitly]
    public class BwoinkSystem : SharedBwoinkSystem
    {
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IAdminManager _adminManager = default!;
        [Dependency] private readonly IConfigurationManager _config = default!;
        [Dependency] private readonly IPlayerLocator _playerLocator = default!;

        private readonly HttpClient _httpClient = new();
        private string _webhookUrl = string.Empty;
        private string _serverName = string.Empty;

        public override void Initialize()
        {
            base.Initialize();
            _config.OnValueChanged(CCVars.DiscordAHelpWebhook, OnWebhookChanged, true);
            _config.OnValueChanged(CVars.GameHostName, OnServerNameChanged, true);
        }

        private void OnServerNameChanged(string obj)
        {
            _serverName = obj;
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _config.UnsubValueChanged(CCVars.DiscordAHelpWebhook, OnWebhookChanged);
        }

        private void OnWebhookChanged(string obj)
        {
            _webhookUrl = obj;
        }

        protected override void OnBwoinkTextMessage(BwoinkTextMessage message, EntitySessionEventArgs eventArgs)
        {
            base.OnBwoinkTextMessage(message, eventArgs);
            var senderSession = (IPlayerSession) eventArgs.SenderSession;

            // TODO: Sanitize text?
            // Confirm that this person is actually allowed to send a message here.
            var senderAdmin = _adminManager.GetAdminData(senderSession);
            var authorized = senderSession.UserId == message.ChannelId || senderAdmin != null;
            if (!authorized)
            {
                // Unauthorized bwoink (log?)
                return;
            }

            var escapedText = FormattedMessage.EscapeText(message.Text);


            var bwoinkText = senderAdmin switch
            {
                var x when x is not null && x.Flags == AdminFlags.Adminhelp =>
                    $"[color=purple]{senderSession.Name}[/color]: {escapedText}",
                var x when x is not null && x.HasFlag(AdminFlags.Adminhelp) =>
                    $"[color=red]{senderSession.Name}[/color]: {escapedText}",
                _ => $"{senderSession.Name}: {escapedText}",
            };

            var msg = new BwoinkTextMessage(message.ChannelId, senderSession.UserId, bwoinkText);

            LogBwoink(msg);

            // Admins
            var targets = _adminManager.ActiveAdmins.Select(p => p.ConnectedClient).ToList();

            // And involved player
            if (_playerManager.TryGetSessionById(message.ChannelId, out var session))
                if (!targets.Contains(session.ConnectedClient))
                    targets.Add(session.ConnectedClient);

            foreach (var channel in targets)
                RaiseNetworkEvent(msg, channel);

            var sendsWebhook = _webhookUrl != string.Empty;
            if (sendsWebhook)
            {
                var sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("AHELP");

                void LookupFinished(Task<LocatedPlayerData?> finishedTask)
                {
                    if (finishedTask.Result == null)
                    {
                        sawmill.Log(LogLevel.Error, $"Unable to find player for netuserid {msg.ChannelId} when sending discord webhook.");
                        return;
                    }

                    var payload = new WebhookPayload()
                    {
                        Username = _serverName,
                        Content = $"`[{finishedTask.Result.Username}]` {senderSession.Name}: \"{escapedText}\""
                    };
                    var request = _httpClient.PostAsync(_webhookUrl,
                        new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));

                    request.ContinueWith(task =>
                    {
                        if (!task.Result.IsSuccessStatusCode)
                        {
                            sawmill.Log(LogLevel.Error, $"Discord returned bad status code: {task.Result.StatusCode}");
                        }
                    });
                }

                _playerLocator.LookupIdAsync(msg.ChannelId).ContinueWith(LookupFinished);
            }

            if (targets.Count == 1)
            {
                var systemText = sendsWebhook ?
                    Loc.GetString("bwoink-system-starmute-message-no-other-users-webhook") :
                    Loc.GetString("bwoink-system-starmute-message-no-other-users");
                var starMuteMsg = new BwoinkTextMessage(message.ChannelId, SystemUserId, systemText);
                RaiseNetworkEvent(starMuteMsg, senderSession.ConnectedClient);
            }
        }

        [JsonObject(MemberSerialization.Fields)]
        private struct WebhookPayload
        {
            [JsonProperty("username")] public string Username;

            [JsonProperty("content")] public string Content;
        }
    }
}

