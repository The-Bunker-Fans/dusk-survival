using Content.Server.GameTicking.Rules.Configurations;
using JetBrains.Annotations;

namespace Content.Server.GameTicking.Rules;

[PublicAPI]
public abstract class GameRuleSystem : EntitySystem
{
    [Dependency] protected GameTicker GameTicker = default!;

    /// <summary>
    ///     Whether this GameRule is currently enabled or not.
    ///     Be sure to check this before doing anything rule-specific.
    /// </summary>
    public bool Enabled { get; protected set; } = false;

    /// <summary>
    ///     When the GameRule prototype with this ID is added, this system will be enabled.
    ///     When it gets removed, this system will be disabled.
    /// </summary>
    public new abstract string Prototype { get; }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRuleAddedEvent>(OnGameRuleAdded);

        SubscribeLocalEvent<GameRuleStartedEvent>(OnGameRuleStarted);
        SubscribeLocalEvent<GameRuleEndedEvent>(OnGameRuleEnded);
    }

    private void OnGameRuleAdded(GameRuleAddedEvent ev)
    {
        if (ev.Rule.Configuration.Id != Prototype)
            return;

        Enabled = true;
    }

    private void OnGameRuleStarted(GameRuleStartedEvent ev)
    {
        if (ev.Rule.Configuration.Id != Prototype)
            return;

        Started(ev.Rule.Configuration);
    }

    private void OnGameRuleEnded(GameRuleEndedEvent ev)
    {
        if (ev.Rule.Configuration.Id != Prototype)
            return;

        Enabled = false;
        Ended(ev.Rule.Configuration);
    }

    /// <summary>
    ///     Called when the game rule has been started..
    /// </summary>
    public abstract void Started(GameRuleConfiguration configuration);

    /// <summary>
    ///     Called when the game rule has ended..
    /// </summary>
    public abstract void Ended(GameRuleConfiguration configuration);
}
