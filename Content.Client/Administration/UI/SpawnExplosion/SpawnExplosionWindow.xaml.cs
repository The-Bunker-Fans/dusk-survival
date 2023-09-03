using System.Numerics;
using Content.Shared.Explosion;
using JetBrains.Annotations;
using Robust.Client.AutoGenerated;
using Robust.Client.Console;
using Robust.Client.Player;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using static Robust.Client.UserInterface.Controls.BaseButton;
using static Robust.Client.UserInterface.Controls.OptionButton;

namespace Content.Client.Administration.UI.SpawnExplosion;

[GenerateTypedNameReferences]
[UsedImplicitly]
public sealed partial class SpawnExplosionWindow : DefaultWindow
{
    [Dependency] private IClientConsoleHost _conHost = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IEntityManager _entMan = default!;


    private readonly SpawnExplosionEui _eui;
    private List<MapId> _mapData = new();
    private List<string> _explosionTypes = new();

    /// <summary>
    ///     Used to prevent unnecessary preview updates when setting fields (e.g., updating position)..
    /// </summary>
    private bool _pausePreview;

    public SpawnExplosionWindow(SpawnExplosionEui eui)
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);
        _eui = eui;

        ExplosionOption.OnItemSelected += ExplosionSelected;
        MapOptions.OnItemSelected += MapSelected;
        Recentre.OnPressed += (_) => SetLocation();
        Spawn.OnPressed += SubmitButtonOnOnPressed;

        Preview.OnToggled += (_) => UpdatePreview();
        MapX.OnValueChanged += (_) => UpdatePreview();
        MapY.OnValueChanged += (_) => UpdatePreview();
        Intensity.OnValueChanged += (_) => UpdatePreview();
        Slope.OnValueChanged += (_) => UpdatePreview();
        MaxIntensity.OnValueChanged += (_) => UpdatePreview();
    }

    private void ExplosionSelected(ItemSelectedEventArgs args)
    {
        ExplosionOption.SelectId(args.Id);
        UpdatePreview();
    }

    private void MapSelected(ItemSelectedEventArgs args)
    {
        MapOptions.SelectId(args.Id);
        UpdatePreview();
    }

    protected override void EnteredTree()
    {
        SetLocation();
        UpdateExplosionTypeOptions();
    }

    private void UpdateExplosionTypeOptions()
    {
        _explosionTypes.Clear();
        ExplosionOption.Clear();
        foreach (var type in _prototypeManager.EnumeratePrototypes<ExplosionPrototype>())
        {
            _explosionTypes.Add(type.ID);
            ExplosionOption.AddItem(type.ID);
        }
    }

    private void UpdateMapOptions()
    {
        _mapData.Clear();
        MapOptions.Clear();
        foreach (var map in _mapManager.GetAllMapIds())
        {
            _mapData.Add(map);
            MapOptions.AddItem(map.ToString());
        }
    }

    /// <summary>
    ///     Set the current grid & indices based on the attached entities current location.
    /// </summary>
    private void SetLocation()
    {
        UpdateMapOptions();

        if (!_entMan.TryGetComponent(_playerManager.LocalPlayer?.ControlledEntity, out TransformComponent? transform))
            return;

        _pausePreview = true;
        MapOptions.Select(_mapData.IndexOf(transform.MapID));
        (MapX.Value, MapY.Value) = transform.MapPosition.Position;
        _pausePreview = false;

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_pausePreview)
            return;

        if (!Preview.Pressed)
        {
            _eui.ClearOverlay();
            return;
        }

        MapCoordinates coords = new(new Vector2(MapX.Value, MapY.Value), _mapData[MapOptions.SelectedId]);
        var explosionType = _explosionTypes[ExplosionOption.SelectedId];
        _eui.RequestPreviewData(coords, explosionType, Intensity.Value, Slope.Value, MaxIntensity.Value);
    }

    private void SubmitButtonOnOnPressed(ButtonEventArgs args)
    {
        // need to make room to view the fireworks
        Preview.Pressed = false;
        _eui.ClearOverlay();

        // for the actual explosion, we will just re-use the explosion command.
        // so assemble command arguments:
        var mapId = _mapData[MapOptions.SelectedId];
        var explosionType = _explosionTypes[ExplosionOption.SelectedId];
        var cmd = $"explosion {Intensity.Value} {Slope.Value} {MaxIntensity.Value} {MapX.Value} {MapY.Value} {mapId} {explosionType}";

        _conHost.ExecuteCommand(cmd);
    }
}
