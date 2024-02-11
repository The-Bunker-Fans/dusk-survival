using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Client.Pinpointer.UI;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.StatusIcon;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Medical.CrewMonitoring;

[GenerateTypedNameReferences]
public sealed partial class CrewMonitoringWindow : FancyWindow
{
    private List<Control> _rowsContent = new();
    private readonly IEntityManager _entManager;
    private readonly IPrototypeManager _prototypeManager;
    private readonly SpriteSystem _spriteSystem;
    private readonly SharedTransformSystem _transformSystem;

    private NetEntity? _trackedEntity;
    private bool _tryToScrollToListFocus;
    private Texture? _blipTexture;

    public CrewMonitoringWindow(string stationName, EntityUid? mapUid)
    {
        RobustXamlLoader.Load(this);

        _entManager = IoCManager.Resolve<IEntityManager>();
        _prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        _spriteSystem = _entManager.System<SpriteSystem>();
        _transformSystem = _entManager.System<SharedTransformSystem>();


        _blipTexture = _spriteSystem.Frame0(new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/NavMap/beveled_circle.png")));

        if (_entManager.TryGetComponent<TransformComponent>(mapUid, out var xform))
            NavMap.MapUid = xform.GridUid;

        else
            NavMap.Visible = false;

        StationName.AddStyleClass("LabelBig");
        StationName.Text = stationName;

        NavMap.TrackedEntitySelectedAction += SetTrackedEntityFromNavMap;
        NavMap.ForceNavMapUpdate();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_tryToScrollToListFocus)
            TryToScrollToFocus();
    }

    public void ShowSensors(List<SuitSensorStatus> sensors, EntityUid monitor, EntityCoordinates? monitorCoords)
    {
        ClearOutDatedData();

        // No server label
        if (sensors.Count == 0)
        {
            NoServerLabel.Visible = true;
            return;
        }

        NoServerLabel.Visible = false;

        // Order sensor data
        var orderedSensors = sensors.OrderBy(n => n.Name).OrderBy(j => j.Job);
        var assignedSensors = new HashSet<SuitSensorStatus>();
        var departments = sensors.SelectMany(d => d.JobDepartments).Distinct().OrderBy(n => n);

        // Create department labels and populate lists
        foreach (var department in departments)
        {
            var departmentSensors = orderedSensors.Where(d => d.JobDepartments.Contains(department));

            if (departmentSensors == null || !departmentSensors.Any())
                continue;

            foreach (var sensor in departmentSensors)
                assignedSensors.Add(sensor);

            if (SensorsTable.ChildCount > 0)
            {
                var spacer = new Control()
                {
                    SetHeight = 20,
                };

                SensorsTable.AddChild(spacer);
                _rowsContent.Add(spacer);
            }

            var deparmentLabel = new RichTextLabel()
            {
                Margin = new Thickness(10, 0),
                HorizontalExpand = true,
            };

            deparmentLabel.SetMessage(department);
            deparmentLabel.StyleClasses.Add(StyleNano.StyleClassTooltipActionDescription);

            SensorsTable.AddChild(deparmentLabel);
            _rowsContent.Add(deparmentLabel);

            PopulateDepartmentList(departmentSensors);
        }

        // Account for any non-station users
        var remainingSensors = orderedSensors.Except(assignedSensors);

        if (remainingSensors.Any())
        {
            var spacer = new Control()
            {
                SetHeight = 20,
            };

            SensorsTable.AddChild(spacer);
            _rowsContent.Add(spacer);

            var deparmentLabel = new RichTextLabel()
            {
                Margin = new Thickness(10, 0),
                HorizontalExpand = true,
            };

            deparmentLabel.SetMessage(Loc.GetString("crew-monitoring-user-interface-no-department"));
            deparmentLabel.StyleClasses.Add(StyleNano.StyleClassTooltipActionDescription);

            SensorsTable.AddChild(deparmentLabel);
            _rowsContent.Add(deparmentLabel);

            PopulateDepartmentList(remainingSensors);
        }

        // Show monitor on nav map
        if (monitorCoords != null && _blipTexture != null)
        {
            NavMap.TrackedEntities[_entManager.GetNetEntity(monitor)] = new NavMapBlip(monitorCoords.Value, monitorCoords.Value.ToMap(_entManager, _transformSystem), _blipTexture, Color.Cyan, true, false);
        }
    }

    private void PopulateDepartmentList(IEnumerable<SuitSensorStatus> departmentSensors)
    {
        // Populate departments
        foreach (var sensor in departmentSensors)
        {
            var coordinates = _entManager.GetCoordinates(sensor.Coordinates);

            // Add a button that will hold a username and other details
            NavMap.LocalizedNames.TryAdd(sensor.SuitSensorUid, sensor.Name + ", " + sensor.Job);

            var sensorButton = new CrewMonitoringButton()
            {
                SuitSensorUid = sensor.SuitSensorUid,
                Coordinates = coordinates,
                Disabled = (coordinates == null),
                HorizontalExpand = true,
            };

            if (sensor.SuitSensorUid == _trackedEntity)
                sensorButton.AddStyleClass(StyleNano.StyleClassButtonColorGreen);

            SensorsTable.AddChild(sensorButton);
            _rowsContent.Add(sensorButton);

            // Primary container to hold the button UI elements
            var mainContainer = new BoxContainer()
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
            };

            sensorButton.AddChild(mainContainer);

            // User status container
            var statusContainer = new BoxContainer()
            {
                SizeFlagsStretchRatio = 1.25f,
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
            };

            mainContainer.AddChild(statusContainer);

            // Suit coords indicator
            var suitCoordsIndicator = new TextureRect()
            {
                Texture = _blipTexture,
                TextureScale = new Vector2(0.25f, 0.25f),
                Modulate = coordinates != null ? Color.LimeGreen : Color.DarkRed,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
            };

            statusContainer.AddChild(suitCoordsIndicator);

            // Specify texture for the user status icon
            var specifier = new SpriteSpecifier.Rsi(new ResPath("Interface/Alerts/human_crew_monitoring.rsi"), "alive");

            if (!sensor.IsAlive)
            {
                specifier = new SpriteSpecifier.Rsi(new ResPath("Interface/Alerts/human_crew_monitoring.rsi"), "dead");
            }

            else if (sensor.TotalDamage != null)
            {
                var index = MathF.Round(4f * (sensor.TotalDamage.Value / 100f));

                if (index >= 5)
                    specifier = new SpriteSpecifier.Rsi(new ResPath("Interface/Alerts/human_crew_monitoring.rsi"), "critical");

                else
                    specifier = new SpriteSpecifier.Rsi(new ResPath("Interface/Alerts/human_crew_monitoring.rsi"), "health" + index);
            }

            // Status icon
            var statusIcon = new AnimatedTextureRect
            {
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                Margin = new Thickness(0, 1, 3, 0),
            };

            statusIcon.SetFromSpriteSpecifier(specifier);
            statusIcon.DisplayRect.TextureScale = new Vector2(2f, 2f);

            statusContainer.AddChild(statusIcon);

            // User name
            var nameLabel = new Label()
            {
                Text = sensor.Name,
                HorizontalExpand = true,
                ClipText = true,
            };

            statusContainer.AddChild(nameLabel);

            // User job container
            var jobContainer = new BoxContainer()
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
            };

            mainContainer.AddChild(jobContainer);

            // Job icon
            if (_prototypeManager.TryIndex<StatusIconPrototype>(sensor.JobIcon, out var proto))
            {
                var jobIcon = new TextureRect()
                {
                    TextureScale = new Vector2(2f, 2f),
                    Stretch = TextureRect.StretchMode.KeepCentered,
                    Texture = _spriteSystem.Frame0(proto.Icon),
                    Margin = new Thickness(5, 0, 5, 0),
                };

                jobContainer.AddChild(jobIcon);
            }

            // Job name
            var jobLabel = new Label()
            {
                Text = sensor.Job,
                HorizontalExpand = true,
                ClipText = true,
            };

            jobContainer.AddChild(jobLabel);

            // Add user coordinates to the navmap
            if (coordinates != null && NavMap.Visible && _blipTexture != null)
            {
                NavMap.TrackedEntities.TryAdd(sensor.SuitSensorUid,
                    new NavMapBlip
                    (coordinates.Value,
                    coordinates.Value.ToMap(_entManager, _transformSystem),
                    _blipTexture,
                    (_trackedEntity == null || sensor.SuitSensorUid == _trackedEntity) ? Color.LimeGreen : Color.LimeGreen * Color.DimGray,
                    sensor.SuitSensorUid == _trackedEntity));

                NavMap.Focus = _trackedEntity;

                // On button up
                sensorButton.OnButtonUp += args =>
                {
                    var prevTrackedEntity = _trackedEntity;

                    if (_trackedEntity == sensor.SuitSensorUid)
                    {
                        _trackedEntity = null;
                    }

                    else
                    {
                        _trackedEntity = sensor.SuitSensorUid;
                        NavMap.CenterToCoordinates(coordinates.Value);
                    }

                    NavMap.Focus = _trackedEntity;

                    UpdateSensorsTable(_trackedEntity, prevTrackedEntity);
                };
            }
        }
    }

    private void SetTrackedEntityFromNavMap(NetEntity? netEntity)
    {
        var prevTrackedEntity = _trackedEntity;
        _trackedEntity = netEntity;

        if (_trackedEntity == prevTrackedEntity)
            prevTrackedEntity = null;

        NavMap.Focus = _trackedEntity;
        _tryToScrollToListFocus = true;

        UpdateSensorsTable(_trackedEntity, prevTrackedEntity);
    }

    private void UpdateSensorsTable(NetEntity? currTrackedEntity, NetEntity? prevTrackedEntity)
    {
        foreach (var sensor in SensorsTable.Children)
        {
            if (sensor is not CrewMonitoringButton)
                continue;

            var castSensor = (CrewMonitoringButton) sensor;

            if (castSensor.SuitSensorUid == prevTrackedEntity)
                castSensor.RemoveStyleClass(StyleNano.StyleClassButtonColorGreen);

            else if (castSensor.SuitSensorUid == currTrackedEntity)
                castSensor.AddStyleClass(StyleNano.StyleClassButtonColorGreen);

            if (castSensor?.Coordinates == null)
                continue;

            if (NavMap.TrackedEntities.TryGetValue(castSensor.SuitSensorUid, out var data))
            {
                data = new NavMapBlip
                    (data.Coordinates,
                    data.Coordinates.ToMap(_entManager, _transformSystem),
                    data.Texture,
                    (currTrackedEntity == null || castSensor.SuitSensorUid == currTrackedEntity) ? Color.LimeGreen : Color.LimeGreen * Color.DimGray,
                    castSensor.SuitSensorUid == currTrackedEntity);

                NavMap.TrackedEntities[castSensor.SuitSensorUid] = data;
            }
        }
    }

    private void TryToScrollToFocus()
    {
        if (!_tryToScrollToListFocus)
            return;

        if (!TryGetVerticalScrollbar(SensorScroller, out var vScrollbar))
            return;

        if (TryGetNextScrollPosition(out float? nextScrollPosition))
        {
            vScrollbar.ValueTarget = nextScrollPosition.Value;

            if (MathHelper.CloseToPercent(vScrollbar.Value, vScrollbar.ValueTarget))
            {
                _tryToScrollToListFocus = false;
                return;
            }
        }
    }

    private bool TryGetVerticalScrollbar(ScrollContainer scroll, [NotNullWhen(true)] out VScrollBar? vScrollBar)
    {
        vScrollBar = null;

        foreach (var child in scroll.Children)
        {
            if (child is not VScrollBar)
                continue;

            vScrollBar = (VScrollBar) child;
            return true;
        }

        return false;
    }

    private bool TryGetNextScrollPosition([NotNullWhen(true)] out float? nextScrollPosition)
    {
        nextScrollPosition = 0;

        foreach (var sensor in SensorsTable.Children)
        {
            if (sensor is CrewMonitoringButton &&
                ((CrewMonitoringButton) sensor).SuitSensorUid == _trackedEntity)
                return true;

            nextScrollPosition += sensor.Height;
        }

        // Failed to find control
        nextScrollPosition = null;

        return false;
    }

    private void ClearOutDatedData()
    {
        SensorsTable.RemoveAllChildren();
        _rowsContent.Clear();
        NavMap.TrackedCoordinates.Clear();
        NavMap.TrackedEntities.Clear();
        NavMap.LocalizedNames.Clear();
    }
}

public sealed class CrewMonitoringButton : Button
{
    public int IndexInTable;
    public NetEntity SuitSensorUid;
    public EntityCoordinates? Coordinates;
}
