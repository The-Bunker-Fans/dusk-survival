﻿using Content.Client.Stylesheets;
using Content.Shared.NukeOps;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.NukeOps;

[GenerateTypedNameReferences]
public sealed partial class WarDeclaratorWindow : DefaultWindow
{
    private readonly IGameTiming _gameTiming;

    public event Action<string>? OnActivated;

    private TimeSpan _endTime;
    private TimeSpan _timeStamp;
    private WarConditionStatus _status;

    public WarDeclaratorWindow()
    {
        RobustXamlLoader.Load(this);

        _gameTiming = IoCManager.Resolve<IGameTiming>();

        WarButton.OnPressed += ActivateWarDeclarator;

        var loc = IoCManager.Resolve<ILocalizationManager>();
        MessageEdit.Placeholder = new Rope.Leaf(loc.GetString("war-declarator-message-placeholder"));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        UpdateTimer();
    }

    public void UpdateState(WarDeclaratorBoundUserInterfaceState state)
    {
        _timeStamp = state.Delay;
        _endTime = state.EndTime;
        _status = state.Status;

        switch(state.Status)
        {
            case WarConditionStatus.WAR_READY:
                StatusLabel.Text = Loc.GetString("war-declarator-boost-declared");
                InfoLabel.Text = Loc.GetString("war-declarator-conditions-ready");
                StatusLabel.SetOnlyStyleClass(StyleNano.StyleClassPowerStateLow);
                break;
            case WarConditionStatus.WAR_DELAY:
                StatusLabel.Text = Loc.GetString("war-declarator-boost-declared-delay");
                UpdateTimer();
                StatusLabel.SetOnlyStyleClass(StyleNano.StyleClassPowerStateLow);
                break;
            case WarConditionStatus.YES_WAR:
                StatusLabel.Text = Loc.GetString("war-declarator-boost-possible");
                UpdateTimer();
                StatusLabel.SetOnlyStyleClass(StyleNano.StyleClassPowerStateGood);
                break;
            case WarConditionStatus.NO_WAR_SMALL_CREW:
                StatusLabel.Text = Loc.GetString("war-declarator-boost-impossible");
                InfoLabel.Text = Loc.GetString("war-declarator-conditions-small-crew", ("min", state.MinCrew));
                StatusLabel.SetOnlyStyleClass(StyleNano.StyleClassPowerStateNone);
                break;
            case WarConditionStatus.NO_WAR_SHUTTLE_DEPARTED:
                StatusLabel.Text = Loc.GetString("war-declarator-boost-impossible");
                InfoLabel.Text = Loc.GetString("war-declarator-conditions-left-outpost");
                StatusLabel.SetOnlyStyleClass(StyleNano.StyleClassPowerStateNone);
                break;
            case WarConditionStatus.NO_WAR_TIMEOUT:
                StatusLabel.Text = Loc.GetString("war-declarator-boost-impossible");
                InfoLabel.Text = Loc.GetString("war-declarator-conditions-time-out");
                StatusLabel.SetOnlyStyleClass(StyleNano.StyleClassPowerStateNone);
                break;
            default:
                StatusLabel.Text = Loc.GetString("war-declarator-boost-impossible");
                InfoLabel.Text = Loc.GetString("war-declarator-conditions-unknown");
                StatusLabel.SetOnlyStyleClass(StyleNano.StyleClassPowerStateNone);
                break;
        }
    }

    public void UpdateTimer()
    {
        switch(_status)
        {
            case WarConditionStatus.YES_WAR:
                var gameruleTime = _gameTiming.CurTime.Subtract(_timeStamp);
                var timeLeft = _endTime.Subtract(gameruleTime);

                if (timeLeft > TimeSpan.Zero)
                {
                    InfoLabel.Text = Loc.GetString("war-declarator-boost-timer", ("minutes", timeLeft.Minutes), ("seconds", timeLeft.Seconds));
                }
                else
                {
                    _status = WarConditionStatus.NO_WAR_TIMEOUT;
                    StatusLabel.Text = Loc.GetString("war-declarator-boost-impossible");
                    InfoLabel.Text = Loc.GetString("war-declarator-conditions-time-out");
                    StatusLabel.SetOnlyStyleClass(StyleNano.StyleClassPowerStateNone);
                    WarButton.Disabled = true;
                }
                break;
            case WarConditionStatus.WAR_DELAY:
                var timeAfterDeclaration = _gameTiming.CurTime.Subtract(_timeStamp);
                var timeRemain = _endTime.Subtract(timeAfterDeclaration);

                if (timeRemain > TimeSpan.Zero)
                {
                    InfoLabel.Text = Loc.GetString("war-declarator-boost-timer", ("minutes", timeRemain.Minutes), ("seconds", timeRemain.Seconds));
                }
                else
                {
                    _status = WarConditionStatus.WAR_READY;
                    StatusLabel.Text = Loc.GetString("war-declarator-boost-declared");
                    InfoLabel.Text = Loc.GetString("war-declarator-conditions-ready");
                    StatusLabel.SetOnlyStyleClass(StyleNano.StyleClassPowerStateLow);
                    WarButton.Disabled = true;
                }
                break;
            default:
                return;
        }
    }

    private void ActivateWarDeclarator(BaseButton.ButtonEventArgs obj)
    {
        var message = Rope.Collapse(MessageEdit.TextRope);
        if (string.IsNullOrEmpty(message))
            return;

        OnActivated?.Invoke(message);
    }
}
