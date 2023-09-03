using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Research.UI;

[GenerateTypedNameReferences]
public sealed partial class ResearchConsoleMenu : FancyWindow
{
    public Action<string>? OnTechnologyCardPressed;
    public Action? OnServerButtonPressed;

    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IPlayerManager _player = default!;
    private readonly TechnologyDatabaseComponent? _technologyDatabase;
    private readonly ResearchSystem _research;
    private readonly SpriteSystem _sprite;
    private readonly AccessReaderSystem _accessReader = default!;

    public readonly EntityUid Entity;

    public ResearchConsoleMenu(EntityUid entity)
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        _research = _entity.System<ResearchSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _accessReader = _entity.System<AccessReaderSystem>();
        Entity = entity;

        ServerButton.OnPressed += _ => OnServerButtonPressed?.Invoke();

        _entity.TryGetComponent(entity, out _technologyDatabase);
    }

    public void  UpdatePanels(ResearchConsoleBoundInterfaceState state)
    {
        var allTech = _research.GetAvailableTechnologies(Entity);
        AvailableCardsContainer.Children.Clear();
        TechnologyCardsContainer.Children.Clear();
        UnlockedCardsContainer.Children.Clear();

        foreach (var tech in allTech)
        {
            var mini = new MiniTechnologyCardControl(tech, _prototype, _sprite, GetTechnologyDescription(tech, false));
            AvailableCardsContainer.AddChild(mini);
        }

        if (_technologyDatabase == null)
            return;

        // i can't figure out the spacing so here you go
        TechnologyCardsContainer.AddChild(new Control
        {
            MinHeight = 10
        });

        var hasAccess = _player.LocalPlayer?.ControlledEntity is not { } local ||
                        !_entity.TryGetComponent<AccessReaderComponent>(Entity, out var access) ||
                        _accessReader.IsAllowed(local, access);
        foreach (var techId in _technologyDatabase.CurrentTechnologyCards)
        {
            var tech = _prototype.Index<TechnologyPrototype>(techId);
            var cardControl = new TechnologyCardControl(tech, _prototype, _sprite, GetTechnologyDescription(tech), state.Points, hasAccess);
            cardControl.OnPressed += () => OnTechnologyCardPressed?.Invoke(techId);
            TechnologyCardsContainer.AddChild(cardControl);
        }

        foreach (var unlocked in _technologyDatabase.UnlockedTechnologies)
        {
            var tech = _prototype.Index<TechnologyPrototype>(unlocked);
            var cardControl = new MiniTechnologyCardControl(tech, _prototype, _sprite, GetTechnologyDescription(tech, false));
            UnlockedCardsContainer.AddChild(cardControl);
        }
    }

    public FormattedMessage GetTechnologyDescription(TechnologyPrototype technology, bool includeCost = true)
    {
        var description = new FormattedMessage();
        if (includeCost)
        {
            description.AddMarkup(Loc.GetString("research-console-cost", ("amount", technology.Cost)));
            description.PushNewline();
        }
        description.AddMarkup(Loc.GetString("research-console-unlocks-list-start"));
        foreach (var recipe in technology.RecipeUnlocks)
        {
            var recipeProto = _prototype.Index<LatheRecipePrototype>(recipe);
            description.PushNewline();
            description.AddMarkup(Loc.GetString("research-console-unlocks-list-entry",
                ("name",recipeProto.Name)));
        }
        foreach (var generic in technology.GenericUnlocks)
        {
            description.PushNewline();
            description.AddMarkup(Loc.GetString("research-console-unlocks-list-entry-generic",
                ("name", Loc.GetString(generic.UnlockDescription))));
        }

        return description;
    }

    public void UpdateInformationPanel(ResearchConsoleBoundInterfaceState state)
    {
        var amountMsg = new FormattedMessage();
        amountMsg.AddMarkup(Loc.GetString("research-console-menu-research-points-text",
            ("points", state.Points)));
        ResearchAmountLabel.SetMessage(amountMsg);

        if (_technologyDatabase == null)
            return;

        var disciplineText = Loc.GetString("research-discipline-none");
        var disciplineColor = Color.Gray;
        if (_technologyDatabase.MainDiscipline != null)
        {
            var discipline = _prototype.Index<TechDisciplinePrototype>(_technologyDatabase.MainDiscipline);
            disciplineText = Loc.GetString(discipline.Name);
            disciplineColor = discipline.Color;
        }

        var msg = new FormattedMessage();
        msg.AddMarkup(Loc.GetString("research-console-menu-main-discipline",
            ("name", disciplineText), ("color", disciplineColor)));
        MainDisciplineLabel.SetMessage(msg);

        TierDisplayContainer.Children.Clear();
        foreach (var disciplineId in _technologyDatabase.SupportedDisciplines)
        {
            var discipline = _prototype.Index<TechDisciplinePrototype>(disciplineId);
            var tier = _research.GetHighestDisciplineTier(_technologyDatabase, discipline);

            // don't show tiers with no available tech
            if (tier == 0)
                continue;

            // i'm building the small-ass control here to spare me some mild annoyance in making a new file
            var texture = new TextureRect
            {
                TextureScale = new Vector2( 2, 2 ),
                VerticalAlignment = VAlignment.Center
            };
            var label = new RichTextLabel();
            texture.Texture = _sprite.Frame0(discipline.Icon);
            label.SetMessage(Loc.GetString("research-console-tier-info-small", ("tier", tier)));

            var control = new BoxContainer
            {
                Children =
                {
                    texture,
                    label,
                    new Control
                    {
                        MinWidth = 10
                    }
                }
            };
            TierDisplayContainer.AddChild(control);
        }
    }
}

