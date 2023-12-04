using Content.Client.Pinpointer.UI;
using Content.Client.UserInterface.Controls;
using Content.Shared.Pinpointer;
using Content.Shared.Power;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Power;

[GenerateTypedNameReferences]
public sealed partial class PowerMonitoringWindow : FancyWindow
{
    private readonly IEntityManager _entManager;
    private readonly SpriteSystem _spriteSystem;
    private readonly IGameTiming _gameTiming;

    private float _updateTimer = 1.0f;
    private const float UpdateTime = 1.0f;
    private const float BlinkFrequency = 1f;

    private EntityUid? _owner;
    private EntityUid? _focusEntity;

    private Color _wallColor = new Color(102, 164, 217);
    private Color _tileColor = new Color(30, 57, 67);

    private Dictionary<EntityUid, (EntityCoordinates, NavMapTrackableComponent)> _trackedEntities = new();

    public event Action<NetEntity?, PowerMonitoringConsoleGroup?>? RequestPowerMonitoringUpdateAction;

    public PowerMonitoringWindow(PowerMonitoringConsoleBoundUserInterface userInterface, EntityUid? owner)
    {
        RobustXamlLoader.Load(this);
        _entManager = IoCManager.Resolve<IEntityManager>();
        _gameTiming = IoCManager.Resolve<IGameTiming>();

        _spriteSystem = _entManager.System<SpriteSystem>();
        _owner = owner;

        if (_entManager.TryGetComponent<PowerMonitoringConsoleComponent>(owner, out var powerMonitoringConsole))
            NavMap.PowerMonitoringConsole = powerMonitoringConsole;

        // Set nav map grid uid
        if (_entManager.TryGetComponent<TransformComponent>(owner, out var xform))
        {
            NavMap.MapUid = xform.GridUid;

            // Assign station name      
            var stationName = Loc.GetString("power-monitoring-window-unknown-location");

            if (_entManager.TryGetComponent<MetaDataComponent>(xform.GridUid, out var stationMetaData))
                stationName = stationMetaData.EntityName;

            var msg = new FormattedMessage();
            msg.AddMarkup(Loc.GetString("power-monitoring-window-station-name", ("stationName", stationName)));

            StationName.SetMessage(msg);
        }
        else
        {
            StationName.SetMessage(Loc.GetString("power-monitoring-window-unknown-location"));
            NavMap.Visible = false;
        }

        // Set colors
        NavMap.TileColor = _tileColor;
        NavMap.WallColor = _wallColor;

        // Update nav map
        NavMap.ForceNavMapUpdate();

        // Set UI tab titles
        MasterTabContainer.SetTabTitle(0, Loc.GetString("power-monitoring-window-label-sources"));
        MasterTabContainer.SetTabTitle(1, Loc.GetString("power-monitoring-window-label-smes"));
        MasterTabContainer.SetTabTitle(2, Loc.GetString("power-monitoring-window-label-substation"));
        MasterTabContainer.SetTabTitle(3, Loc.GetString("power-monitoring-window-label-apc"));

        // Track when the MasterTabContainer changes its tab
        MasterTabContainer.OnTabChanged += OnTabChanged;

        // Set UI toggles
        ShowHVCable.OnToggled += _ => OnShowCableToggled(NavMapLineGroup.HighVoltage);
        ShowMVCable.OnToggled += _ => OnShowCableToggled(NavMapLineGroup.MediumVoltage);
        ShowLVCable.OnToggled += _ => OnShowCableToggled(NavMapLineGroup.Apc);

        // Set power monitoring update request action
        RequestPowerMonitoringUpdateAction += userInterface.RequestPowerMonitoringUpdate;

        // Set trackable entity selected action
        NavMap.TrackableEntitySelectedAction += SetTrackedEntityFromNavMap;
    }

    private void OnTabChanged(int tab)
    {
        RequestPowerMonitoringUpdateAction?.Invoke(_entManager.GetNetEntity(_focusEntity), (PowerMonitoringConsoleGroup) tab);
    }

    private void OnShowCableToggled(NavMapLineGroup lineGroup)
    {
        if (!NavMap.HiddenLineGroups.Remove(lineGroup))
            NavMap.HiddenLineGroups.Add(lineGroup);
    }

    private void UpdateTrackedEntities()
    {
        _trackedEntities.Clear();

        if (NavMap.MapUid == null)
            return;

        var query = _entManager.AllEntityQueryEnumerator<PowerMonitoringDeviceComponent, NavMapTrackableComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var _, out var trackable, out var xform))
        {
            if (NavMap.MapUid == xform.GridUid && xform.Anchored)
                _trackedEntities.Add(ent, (xform.Coordinates, trackable));
        }
    }

    public void ShowEntites
        (double totalSources,
        double totalBatteryUsage,
        double totalLoads,
        PowerMonitoringConsoleEntry[] allEntries,
        PowerMonitoringConsoleEntry[] focusSources,
        PowerMonitoringConsoleEntry[] focusLoads,
        EntityCoordinates? monitorCoords)
    {
        if (_owner == null)
            return;

        if (!_entManager.TryGetComponent<MapGridComponent>(NavMap.MapUid, out var _))
            return;

        // Sort all devices alphabetically by their entity name (not by power usage; otherwise their position on the UI will shift)
        Array.Sort(allEntries, AlphabeticalSort);
        Array.Sort(focusSources, AlphabeticalSort);
        Array.Sort(focusLoads, AlphabeticalSort);

        // Update tracked entries
        UpdateTrackedEntities();

        // Reset nav map values
        NavMap.TrackedCoordinates.Clear();
        NavMap.TrackedEntities.Clear();

        // Draw all entities on the map
        foreach ((var ent, (var coords, var trackable)) in _trackedEntities)
        {
            if (trackable.ParentUid != null && trackable.ParentUid.Value.IsValid())
                continue;

            AddTrackedEntityToNavMap(ent, coords, trackable, _focusEntity != null);
        }

        foreach (var netEnt in focusSources)
        {
            var ent = _entManager.GetEntity(netEnt.NetEntity);

            if (_entManager.TryGetComponent<NavMapTrackableComponent>(ent, out var focusTrackable))
                AddTrackedEntityToNavMap(ent, _entManager.GetComponent<TransformComponent>(ent).Coordinates, focusTrackable);
        }

        foreach (var netEnt in focusLoads)
        {
            var ent = _entManager.GetEntity(netEnt.NetEntity);

            if (_entManager.TryGetComponent<NavMapTrackableComponent>(ent, out var focusTrackable))
                AddTrackedEntityToNavMap(ent, _entManager.GetComponent<TransformComponent>(ent).Coordinates, focusTrackable);
        }


        // Show monitor location
        if (monitorCoords != null &&
            _entManager.TryGetComponent<NavMapTrackableComponent>(_owner, out var monitorTrackable))
        {
            AddTrackedEntityToNavMap(_owner.Value, monitorCoords.Value, monitorTrackable);
        }

        // Update power status text
        TotalSources.Text = Loc.GetString("power-monitoring-window-value", ("value", totalSources));
        TotalBatteryUsage.Text = Loc.GetString("power-monitoring-window-value", ("value", totalBatteryUsage));
        TotalLoads.Text = Loc.GetString("power-monitoring-window-value", ("value", totalLoads));

        // 10+% of station power is being drawn from batteries
        TotalBatteryUsage.FontColorOverride = (totalSources * 0.1111f) < totalBatteryUsage ? new Color(180, 0, 0) : Color.White;

        // Station generator and battery output is less than the current demand
        TotalLoads.FontColorOverride = (totalSources + totalBatteryUsage) < totalLoads &&
            !MathHelper.CloseToPercent(totalSources + totalBatteryUsage, totalLoads, 0.1f) ? new Color(180, 0, 0) : Color.White;

        // Update current list
        switch (GetCurrentPowerMonitoringConsoleGroup())
        {
            case PowerMonitoringConsoleGroup.Generator:
                UpdateAllConsoleEntries(SourcesList, allEntries, null, focusLoads); break;
            case PowerMonitoringConsoleGroup.SMES:
                UpdateAllConsoleEntries(SMESList, allEntries, focusSources, focusLoads); break;
            case PowerMonitoringConsoleGroup.Substation:
                UpdateAllConsoleEntries(SubstationList, allEntries, focusSources, focusLoads); break;
            case PowerMonitoringConsoleGroup.APC:
                UpdateAllConsoleEntries(ApcList, allEntries, focusSources, null); break;
        }

        // Update nav map
        NavMap.ForceNavMapUpdate();

        // Update system warnings
        if (!_entManager.TryGetComponent<PowerMonitoringConsoleComponent>(_owner.Value, out var console))
            return;

        UpdateWarningLabel(console.Flags);
    }

    private void AddTrackedEntityToNavMap(EntityUid uid, EntityCoordinates coords, NavMapTrackableComponent component, bool useDarkColors = false)
    {
        if (!NavMap.Visible)
            return;

        component.Modulate = (uid != _focusEntity && useDarkColors) ? Color.DimGray : Color.White;
        component.Blinks = uid == _focusEntity || uid == _owner;

        // We expect a single tracked entity at a given coordinate
        NavMap.TrackedEntities[coords] = component;

        if (component.ChildOffsets.Count > 0)
        {
            foreach (var offset in component.ChildOffsets)
            {
                NavMap.TrackedEntities[coords + offset] = component;
            }
        }
    }

    private void SetTrackedEntityFromNavMap(EntityCoordinates? coordinates, NavMapTrackableComponent? trackable)
    {
        if (trackable == null)
            return;

        _focusEntity = trackable.Owner;

        if (!_entManager.TryGetComponent<PowerMonitoringDeviceComponent>(_focusEntity, out var device))
            return;

        // Switch tabs
        SwitchTabsBasedOnPowerMonitoringConsoleGroup(device.Group);

        // Get the scroll position of the selected entity on the selected button the UI
        _tryToScroll = true;

        // Request new data
        RequestPowerMonitoringUpdateAction?.Invoke(_entManager.GetNetEntity(_focusEntity), device.Group);
        _updateTimer = 0f;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        TryToScrollToFocus();

        _updateTimer += args.DeltaSeconds;

        // Warning sign pulse        
        var lit = _gameTiming.RealTime.TotalSeconds % BlinkFrequency > BlinkFrequency / 2f;
        SystemWarningPanel.Modulate = lit ? Color.White : new Color(178, 178, 178);

        if (_updateTimer >= UpdateTime)
        {
            _updateTimer -= UpdateTime;

            // Request update from power monitoring system
            RequestPowerMonitoringUpdateAction?.Invoke(_entManager.GetNetEntity(_focusEntity), GetCurrentPowerMonitoringConsoleGroup());
        }
    }

    private int AlphabeticalSort(PowerMonitoringConsoleEntry x, PowerMonitoringConsoleEntry y)
    {
        var entX = _entManager.GetEntity(x.NetEntity);

        if (!entX.IsValid())
            return -1;

        var entY = _entManager.GetEntity(y.NetEntity);

        if (!entY.IsValid())
            return 1;

        var nameX = _entManager.GetComponent<MetaDataComponent>(entX).EntityName;
        var nameY = _entManager.GetComponent<MetaDataComponent>(entY).EntityName;

        return nameX.CompareTo(nameY);
    }
}

public struct PowerMonitoringConsoleTrackable
{
    public EntityUid EntityUid;
    public PowerMonitoringConsoleGroup Group;

    public PowerMonitoringConsoleTrackable(EntityUid uid, PowerMonitoringConsoleGroup group)
    {
        EntityUid = uid;
        Group = group;
    }
}
