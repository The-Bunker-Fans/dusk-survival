using Content.Client.UserInterface.Controls;
using Content.Shared.Silicons.Laws.Components;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Silicons.Laws.Ui;

[GenerateTypedNameReferences]
public sealed partial class SiliconLawMenu : FancyWindow
{
    public SiliconLawMenu()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);
    }

    public void Update(SiliconLawBuiState state)
    {
        state.Laws.Sort();
        LawDisplayContainer.Children.Clear();
        foreach (var law in state.Laws)
        {
            var control = new LawDisplay(law, state.CanVerbalizeLaws, state.RadioChannels);

            LawDisplayContainer.AddChild(control);
        }
    }
}

