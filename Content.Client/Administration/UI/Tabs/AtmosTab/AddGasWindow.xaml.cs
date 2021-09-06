using System.Collections.Generic;
using System.Linq;
using Content.Client.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Prototypes;
using JetBrains.Annotations;
using Robust.Client.AutoGenerated;
using Robust.Client.Console;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Client.Administration.UI.Tabs.AtmosTab
{
    [GenerateTypedNameReferences]
    [UsedImplicitly]
    public partial class AddGasWindow : SS14Window
    {
        private IEnumerable<IMapGrid>? _gridData;
        private IEnumerable<GasPrototype>? _gasData;

        /// <summary>
        ///     Function to fill in the UI's x, y, and grid fields with some default values.
        /// </summary>
        public void FillCoords(GridId gridId, int x, int y)
        {
            GridOptions.TrySelectId((int) gridId);
            TileXSpin.Value = x;
            TileYSpin.Value = y;
        }

        protected override void EnteredTree()
        {
            // Fill out grids
            _gridData = IoCManager.Resolve<IMapManager>().GetAllGrids().Where(g => (int) g.Index != 0);
            var playerGrid = IoCManager.Resolve<IPlayerManager>().LocalPlayer?.ControlledEntity?.Transform.GridID;
            foreach (var grid in _gridData)
            {
                GridOptions.AddItem($"{grid.Index} {(playerGrid == grid.Index ? " (Current)" : "")}", id: (int) grid.Index );
            }

            GridOptions.OnItemSelected += eventArgs => GridOptions.SelectId(eventArgs.Id);

            // Fill out gases
            _gasData = EntitySystem.Get<AtmosphereSystem>().Gases;
            foreach (var gas in _gasData)
            {
                GasOptions.AddItem($"{gas.Name} ({gas.ID})");
            }

            GasOptions.OnItemSelected += eventArgs => GasOptions.SelectId(eventArgs.Id);

            SubmitButton.OnPressed += SubmitButtonOnOnPressed;
        }

        private void SubmitButtonOnOnPressed(BaseButton.ButtonEventArgs obj)
        {
            if (_gridData == null || _gasData == null)
                return;

            GridId gridIndex = new(GridOptions.SelectedId);

            var gasList = _gasData.ToList();
            var gasId = gasList[GasOptions.SelectedId].ID;

            IoCManager.Resolve<IClientConsoleHost>().ExecuteCommand(
                $"addgas {TileXSpin.Value} {TileYSpin.Value} {gridIndex} {gasId} {AmountSpin.Value}");
        }
    }
}
