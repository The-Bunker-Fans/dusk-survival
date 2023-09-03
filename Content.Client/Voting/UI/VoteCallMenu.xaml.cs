using System;
using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.Administration;
using Content.Shared.Voting;
using JetBrains.Annotations;
using Robust.Client.AutoGenerated;
using Robust.Client.Console;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client.Voting.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class VoteCallMenu : BaseWindow
    {
        [Dependency] private IClientConsoleHost _consoleHost = default!;
        [Dependency] private IVoteManager _voteManager = default!;
        [Dependency] private IGameTiming _gameTiming = default!;
        [Dependency] private IClientNetManager _netManager = default!;

        public static readonly (string name, StandardVoteType type, (string name, string id)[]? secondaries)[]
            AvailableVoteTypes =
            {
                ("ui-vote-type-restart", StandardVoteType.Restart, null),
                ("ui-vote-type-gamemode", StandardVoteType.Preset, null),
                ("ui-vote-type-map", StandardVoteType.Map, null)
            };

        public VoteCallMenu()
        {
            IoCManager.InjectDependencies(this);
            RobustXamlLoader.Load(this);

            Stylesheet = IoCManager.Resolve<IStylesheetManager>().SheetSpace;
            CloseButton.OnPressed += _ => Close();

            for (var i = 0; i < AvailableVoteTypes.Length; i++)
            {
                var (text, _, _) = AvailableVoteTypes[i];
                VoteTypeButton.AddItem(Loc.GetString(text), i);
            }

            VoteTypeButton.OnItemSelected += VoteTypeSelected;
            VoteSecondButton.OnItemSelected += VoteSecondSelected;
            CreateButton.OnPressed += CreatePressed;
        }

        protected override void Opened()
        {
            base.Opened();

            _netManager.ClientSendMessage(new MsgVoteMenu());

            _voteManager.CanCallVoteChanged += CanCallVoteChanged;
        }

        public override void Close()
        {
            base.Close();

            _voteManager.CanCallVoteChanged -= CanCallVoteChanged;
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            UpdateVoteTimeout();
        }

        private void CanCallVoteChanged(bool obj)
        {
            if (!obj)
                Close();
        }

        private void CreatePressed(BaseButton.ButtonEventArgs obj)
        {
            var typeId = VoteTypeButton.SelectedId;
            var (_, typeKey, secondaries) = AvailableVoteTypes[typeId];

            if (secondaries != null)
            {
                var secondaryId = VoteSecondButton.SelectedId;
                var (_, secondKey) = secondaries[secondaryId];

                _consoleHost.LocalShell.RemoteExecuteCommand($"createvote {typeKey} {secondKey}");
            }
            else
            {
                _consoleHost.LocalShell.RemoteExecuteCommand($"createvote {typeKey}");
            }

            Close();
        }

        private void UpdateVoteTimeout()
        {
            var (_, typeKey, _) = AvailableVoteTypes[VoteTypeButton.SelectedId];
            var isAvailable = _voteManager.CanCallStandardVote(typeKey, out var timeout);
            CreateButton.Disabled = !isAvailable;
            VoteTypeTimeoutLabel.Visible = !isAvailable;

            if (!isAvailable)
            {
                if (timeout == TimeSpan.Zero)
                {
                    VoteTypeTimeoutLabel.Text = Loc.GetString("ui-vote-type-not-available");
                }
                else
                {
                    var remaining = timeout - _gameTiming.RealTime;
                    VoteTypeTimeoutLabel.Text = Loc.GetString("ui-vote-type-timeout", ("remaining", remaining.ToString("mm\\:ss")));
                }
            }
        }

        private static void VoteSecondSelected(OptionButton.ItemSelectedEventArgs obj)
        {
            obj.Button.SelectId(obj.Id);
        }

        private void VoteTypeSelected(OptionButton.ItemSelectedEventArgs obj)
        {
            VoteTypeButton.SelectId(obj.Id);

            var (_, _, options) = AvailableVoteTypes[obj.Id];
            if (options == null)
            {
                VoteSecondButton.Visible = false;
            }
            else
            {
                VoteSecondButton.Visible = true;
                VoteSecondButton.Clear();

                for (var i = 0; i < options.Length; i++)
                {
                    var (text, _) = options[i];
                    VoteSecondButton.AddItem(Loc.GetString(text), i);
                }
            }
        }

        protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
        {
            return DragMode.Move;
        }
    }

    [UsedImplicitly, AnyCommand]
    public sealed class VoteMenuCommand : IConsoleCommand
    {
        public string Command => "votemenu";
        public string Description => Loc.GetString("ui-vote-menu-command-description");
        public string Help => Loc.GetString("ui-vote-menu-command-help-text");

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            new VoteCallMenu().OpenCentered();
        }
    }
}
