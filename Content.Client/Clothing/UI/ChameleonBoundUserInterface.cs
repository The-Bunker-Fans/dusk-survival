﻿using Content.Client.Clothing.Systems;
using Content.Shared.Clothing.Components;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client.Clothing.UI;

[UsedImplicitly]
public sealed class ChameleonBoundUserInterface : BoundUserInterface
{
    private ChameleonMenu? _menu;

    public ChameleonBoundUserInterface(ClientUserInterfaceComponent owner, object uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = new ChameleonMenu();
        _menu.OnClose += Close;
        _menu.OnIdSelected += OnIdSelected;
        _menu.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ChameleonBoundUserInterfaceState st)
            return;

        var chameleonSystem = EntitySystem.Get<ChameleonClothingSystem>();
        var targets = chameleonSystem.GetValidItems(st.Slot);

        _menu?.UpdateState(targets, st.SelectedId);
    }

    private void OnIdSelected(string selectedId)
    {
        SendMessage(new ChameleonPrototypeSelectedMessage(selectedId));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _menu?.Close();
            _menu = null;
        }
    }
}
