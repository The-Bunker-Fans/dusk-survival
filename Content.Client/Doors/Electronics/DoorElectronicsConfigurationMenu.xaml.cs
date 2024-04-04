using System.Linq;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Content.Client.Access.UI;
using Content.Client.Doors.Electronics;
using Content.Shared.Access;
using Content.Shared.Doors.Electronics;
using FancyWindow = Content.Client.UserInterface.Controls.FancyWindow;

namespace Content.Client.Doors.Electronics;

[GenerateTypedNameReferences]
public sealed partial class DoorElectronicsConfigurationMenu : FancyWindow
{
    private readonly DoorElectronicsBoundUserInterface _owner;
    private AccessLevelControl _buttonsList = new();

    public DoorElectronicsConfigurationMenu(DoorElectronicsBoundUserInterface ui, List<ProtoId<AccessLevelPrototype>> accessLevels, IPrototypeManager prototypeManager)
    {
        RobustXamlLoader.Load(this);

        _owner = ui;

        _buttonsList.Populate(accessLevels, prototypeManager);
        AccessLevelControlContainer.AddChild(_buttonsList);

        foreach (var (id, button) in _buttonsList.ButtonsList)
        {
            button.OnPressed += _ => _owner.UpdateConfiguration(
                _buttonsList.ButtonsList.Where(x => x.Value.Pressed).Select(x => x.Key).ToList());
        }
    }

    public void UpdateState(DoorElectronicsConfigurationState state)
    {
        _buttonsList.UpdateState(state.AccessList);
    }
}
