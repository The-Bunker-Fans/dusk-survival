﻿using Content.Client.Message;
using Content.Shared.Anomaly;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Timing;
using FancyWindow = Content.Client.UserInterface.Controls.FancyWindow;

namespace Content.Client.Anomaly.Ui;

[GenerateTypedNameReferences]
public sealed partial class AnomalyGeneratorWindow : FancyWindow
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _cooldownEnd = TimeSpan.Zero;
    private bool _hasEnoughFuel;

    public Action? OnGenerateButtonPressed;

    public AnomalyGeneratorWindow(EntityUid gen)
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        EntityView.Sprite = _entityManager.GetComponent<SpriteComponent>(gen);

        GenerateButton.OnPressed += _ => OnGenerateButtonPressed?.Invoke();
    }

    public void UpdateState(AnomalyGeneratorUserInterfaceState state)
    {
        _cooldownEnd = state.CooldownEndTime;
        _hasEnoughFuel = state.FuelCost <= state.FuelAmount;

        var fuelCompletion = Math.Clamp((float) state.FuelAmount / state.FuelCost, 0f, 1f);

        FuelBar.Value = fuelCompletion;
        FuelText.Text = $"{fuelCompletion:P}";

        UpdateTimer();
        UpdateReady(); // yes this can trigger twice. no i don't care
    }

    public void UpdateTimer()
    {
        if (_timing.CurTime > _cooldownEnd)
        {
            CooldownLabel.SetMarkup(Loc.GetString("anomaly-generator-no-cooldown"));
        }
        else
        {
            var timeLeft = _cooldownEnd - _timing.CurTime;
            var timeString = $"{timeLeft.Minutes:0}:{timeLeft.Seconds:00}";
            CooldownLabel.SetMarkup(Loc.GetString("anomaly-generator-cooldown", ("time", timeString)));
            UpdateReady();
        }
    }

    public void UpdateReady()
    {
        var ready = _hasEnoughFuel && _timing.CurTime > _cooldownEnd;

        var msg = ready
            ? Loc.GetString("anomaly-generator-yes-fire")
            : Loc.GetString("anomaly-generator-no-fire");
        ReadyLabel.SetMarkup(msg);

        GenerateButton.Disabled = !ready;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        UpdateTimer();
    }
}

