using Content.Shared.Fax;
using Content.Shared.Paper;
using Robust.Client.AutoGenerated;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client.Fax.AdminUI;

[GenerateTypedNameReferences]
public sealed partial class AdminFaxWindow : DefaultWindow
{
    private const string StampsRsiPath = "/Textures/Objects/Misc/bureaucracy.rsi";

    public Action<(NetEntity entity, string title, string stampedBy, string message, SharedPaperComponent.TagsState? tagsState, string stampSprite, Color stampColor)>? OnMessageSend;
    public Action<NetEntity>? OnFollowFax;

    [Dependency] private readonly IResourceCache _resCache = default!;

    public AdminFaxWindow()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        PopulateStamps();

        FaxSelector.OnItemSelected += args => FaxSelector.SelectId(args.Id);
        StampSelector.OnItemSelected += args => StampSelector.SelectId(args.Id);
        FollowButton.OnPressed += FollowFax;
        SendButton.OnPressed += SendMessage;

        // Don't use this, but ColorSelectorSliders requires it:
        // what the fok
        StampColorSelector.OnColorChanged += (color) => {};

        var loc = IoCManager.Resolve<ILocalizationManager>();
        MessageEdit.Placeholder = new Rope.Leaf(loc.GetString("admin-fax-message-placeholder")); // TextEdit work only with Nodes
    }

    public void PopulateFaxes(List<AdminFaxEntry> faxes)
    {
        for (var i = 0; i < faxes.Count; i++)
        {
            var fax = faxes[i];
            FaxSelector.AddItem($"{fax.Name} ({fax.Address})", i);
            FaxSelector.SetItemMetadata(i, fax.Uid);
        }
    }

    private void PopulateStamps()
    {
        var rsi = _resCache.GetResource<RSIResource>(StampsRsiPath).RSI;
        using (var enumerator = rsi.GetEnumerator())
        {
            var i = 0;
            while (enumerator.MoveNext())
            {
                var state = enumerator.Current;
                var stateName = state.StateId.Name!;
                if (!stateName.StartsWith("paper_stamp-"))
                    continue;

                StampSelector.AddItem(stateName, i);
                StampSelector.SetItemMetadata(i, stateName);
                i++;
            }
        }
    }

    private void FollowFax(BaseButton.ButtonEventArgs obj)
    {
        var faxEntity = (NetEntity?) FaxSelector.SelectedMetadata;
        if (faxEntity == null)
            return;

        OnFollowFax?.Invoke(faxEntity.Value);
    }

    private void SendMessage(BaseButton.ButtonEventArgs obj)
    {
        var faxEntity = (NetEntity?) FaxSelector.SelectedMetadata;
        if (faxEntity == null)
            return;

        var stamp = (string?) StampSelector.SelectedMetadata;
        if (stamp == null)
            return;

        var title = TitleEdit.Text;
        if (string.IsNullOrEmpty(title))
            return;

        var message = Rope.Collapse(MessageEdit.TextRope);
        if (string.IsNullOrEmpty(message))
            return;

        var from = FromEdit.Text;
        var stampColor = StampColorSelector.Color;
        OnMessageSend?.Invoke((faxEntity.Value, title, from, message, null, stamp, stampColor));
    }
}
