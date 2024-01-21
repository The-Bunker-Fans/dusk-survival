﻿using Content.Client.UserInterface.Controls;
using System.Threading;
using Content.Shared.CCVar;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Client.Communications.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class CommunicationsConsoleMenu : FancyWindow
    {
        [Dependency] private readonly ILocalizationManager _loc = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;

        private CommunicationsConsoleBoundUserInterface Owner { get; set; }
        private readonly CancellationTokenSource _timerCancelTokenSource = new();

        public CommunicationsConsoleMenu(CommunicationsConsoleBoundUserInterface owner)
        {
            IoCManager.InjectDependencies(this);
            RobustXamlLoader.Load(this);

            Owner = owner;

            MessageInput.Placeholder = new Rope.Leaf(_loc.GetString("comms-console-menu-announcement-placeholder"));
            MessageInput.OnTextChanged += HandleTextChanged;

            AnnounceButton.OnPressed += (_) => Owner.AnnounceButtonPressed(Rope.Collapse(MessageInput.TextRope));
            AnnounceButton.Disabled = !owner.CanAnnounce;

            AlertLevelButton.OnItemSelected += args =>
            {
                var metadata = AlertLevelButton.GetItemMetadata(args.Id);
                if (metadata != null && metadata is string cast)
                {
                    Owner.AlertLevelSelected(cast);
                }
            };
            AlertLevelButton.Disabled = !owner.AlertLevelSelectable;

            EmergencyShuttleButton.OnPressed += (_) => Owner.EmergencyShuttleButtonPressed();
            EmergencyShuttleButton.Disabled = !owner.CanCall;

            UpdateCountdown();
            Timer.SpawnRepeating(1000, UpdateCountdown, _timerCancelTokenSource.Token);
        }

        // The current alert could make levels unselectable, so we need to ensure that the UI reacts properly.
        // If the current alert is unselectable, the only item in the alerts list will be
        // the current alert. Otherwise, it will be the list of alerts, with the current alert
        // selected.
        public void UpdateAlertLevels(List<string>? alerts, string currentAlert)
        {
            AlertLevelButton.Clear();

            if (alerts == null)
            {
                var name = currentAlert;
                if (Loc.TryGetString($"alert-level-{currentAlert}", out var locName))
                {
                    name = locName;
                }
                AlertLevelButton.AddItem(name);
                AlertLevelButton.SetItemMetadata(AlertLevelButton.ItemCount - 1, currentAlert);
            }
            else
            {
                foreach (var alert in alerts)
                {
                    var name = alert;
                    if (Loc.TryGetString($"alert-level-{alert}", out var locName))
                    {
                        name = locName;
                    }
                    AlertLevelButton.AddItem(name);
                    AlertLevelButton.SetItemMetadata(AlertLevelButton.ItemCount - 1, alert);
                    if (alert == currentAlert)
                    {
                        AlertLevelButton.Select(AlertLevelButton.ItemCount - 1);
                    }
                }
            }
        }

        public void UpdateCountdown()
        {
            if (!Owner.CountdownStarted)
            {
                CountdownLabel.SetMessage("");
                EmergencyShuttleButton.Text = Loc.GetString("comms-console-menu-call-shuttle");
                return;
            }

            EmergencyShuttleButton.Text = Loc.GetString("comms-console-menu-recall-shuttle");
            CountdownLabel.SetMessage($"Time remaining\n{Owner.Countdown.ToString()}s");
        }

        public override void Close()
        {
            base.Close();

            _timerCancelTokenSource.Cancel();
        }

        private void HandleTextChanged(TextEdit.TextEditEventArgs args)
        {
            var length = MessageInput.TextLength;
            var maxLength = _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);
            var remaining = maxLength - length;

            MessageLengthLabel.Visible = remaining <= 20;
            MessageLengthLabel.Text = $"{remaining}";
            MessageLengthLabel.FontColorOverride = remaining < 0 ? Color.Red : null;
            MessageLengthLabel.ToolTip = remaining < 0
                ? _loc.GetString("comms-console-menu-announcement-length-too-long-tooltip")
                : _loc.GetString("comms-console-menu-announcement-length-remaining-tooltip", ("remaining", remaining));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
                _timerCancelTokenSource.Cancel();
        }
    }
}
