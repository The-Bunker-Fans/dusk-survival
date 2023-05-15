using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Client.Administration.UI.CustomControls;
using Content.Client.Stylesheets;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Roles;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Administration.UI.BanPanel;

[GenerateTypedNameReferences]
public sealed partial class BanPanel : DefaultWindow
{
    public event Action<string?, string?, bool, byte[]?, bool, uint, string, NoteSeverity, string[]?>? BanSubmitted;
    public event Action<string>? PlayerChanged;
    private string? PlayerUsername { get; set; }
    private string? IpAddress { get; set; }
    private byte[]? Hwid { get; set; }
    private double TimeEntered { get; set; }
    private uint Multiplier { get; set; }
    private bool HasBanFlag { get; set; }
    private double? LastButtonReset { get; set; }
    // This is less efficient than just holding a reference to the root control and enumerating children, but you
    // have to know how the controls are nested, which makes the code more complicated.
    private readonly List<CheckBox> _roleCheckboxes = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("GeneratedRegex", "SYSLIB1045:Convert to 'GeneratedRegexAttribute'.", Justification = "Sandbox violation")]
    private static readonly Regex IPRegex = new(
        pattern: @"((^\s*((([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5]))\s*$)|(^\s*((([0-9A-Fa-f]{1,4}:){7}([0-9A-Fa-f]{1,4}|:))|(([0-9A-Fa-f]{1,4}:){6}(:[0-9A-Fa-f]{1,4}|((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3})|:))|(([0-9A-Fa-f]{1,4}:){5}(((:[0-9A-Fa-f]{1,4}){1,2})|:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3})|:))|(([0-9A-Fa-f]{1,4}:){4}(((:[0-9A-Fa-f]{1,4}){1,3})|((:[0-9A-Fa-f]{1,4})?:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:))|(([0-9A-Fa-f]{1,4}:){3}(((:[0-9A-Fa-f]{1,4}){1,4})|((:[0-9A-Fa-f]{1,4}){0,2}:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:))|(([0-9A-Fa-f]{1,4}:){2}(((:[0-9A-Fa-f]{1,4}){1,5})|((:[0-9A-Fa-f]{1,4}){0,3}:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:))|(([0-9A-Fa-f]{1,4}:){1}(((:[0-9A-Fa-f]{1,4}){1,6})|((:[0-9A-Fa-f]{1,4}){0,4}:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:))|(:(((:[0-9A-Fa-f]{1,4}){1,7})|((:[0-9A-Fa-f]{1,4}){0,5}:((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}))|:)))(%.+)?\s*$))",
        options: RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("GeneratedRegex", "SYSLIB1045:Convert to 'GeneratedRegexAttribute'.", Justification = "Sandbox violation")]
    private static readonly Regex HwidRegex = new(@"^[0-9a-f]{64}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private enum TabNumbers
    {
        BasicInfo,
        //Text,
        Players,
        Roles
    }

    private enum Multipliers
    {
        Minutes,
        Hours,
        Days,
        Weeks,
        Months,
        Years,
        Permanent
    }

    private enum Types
    {
        None,
        Server,
        Role
    }

    public BanPanel()
    {
        RobustXamlLoader.Load(this);
        PlayerList.OnSelectionChanged += OnPlayerSelectionChanged;
        PlayerNameLine.OnFocusExit += _ => OnPlayerNameChanged();
        PlayerCheckbox.OnPressed += _ =>
        {
            PlayerNameLine.Editable = PlayerCheckbox.Pressed;
            PlayerNameLine.ModulateSelfOverride = null;
        };
        TimeLine.OnTextChanged += OnMinutesChanged;
        MultiplierOption.OnItemSelected += args =>
        {
            MultiplierOption.SelectId(args.Id);
            OnMultiplierChanged();
        };
        IpLine.OnFocusExit += _ => OnIpChanged();
        IpCheckbox.OnPressed += _ =>
        {
            IpLine.Editable = IpCheckbox.Pressed;
            OnIpChanged();
        };
        HwidLine.OnFocusExit += _ => OnHwidChanged();
        HwidCheckbox.OnPressed += _ =>
        {
            HwidLine.Editable = HwidCheckbox.Pressed;
            OnHwidChanged();
        };
        TypeOption.OnItemSelected += args =>
        {
            TypeOption.SelectId(args.Id);
            OnTypeChanged();
        };
        LastConnCheckbox.OnPressed += args =>
        {
            IpLine.ModulateSelfOverride = null;
            HwidLine.ModulateSelfOverride = null;
            OnIpChanged();
            OnHwidChanged();
        };
        SubmitButton.OnPressed += SubmitButtonOnOnPressed;

        SeverityOption.AddItem(Loc.GetString("admin-note-editor-severity-none"), (int) NoteSeverity.None);
        SeverityOption.AddItem(Loc.GetString("admin-note-editor-severity-low"), (int) NoteSeverity.Minor);
        SeverityOption.AddItem(Loc.GetString("admin-note-editor-severity-medium"), (int) NoteSeverity.Medium);
        SeverityOption.AddItem(Loc.GetString("admin-note-editor-severity-high"), (int) NoteSeverity.High);
        SeverityOption.SelectId((int) NoteSeverity.Medium);
        SeverityOption.OnItemSelected += args => SeverityOption.SelectId(args.Id);

        MultiplierOption.AddItem(Loc.GetString("ban-panel-minutes"), (int) Multipliers.Minutes);
        MultiplierOption.AddItem(Loc.GetString("ban-panel-hours"), (int) Multipliers.Hours);
        MultiplierOption.AddItem(Loc.GetString("ban-panel-days"), (int) Multipliers.Days);
        MultiplierOption.AddItem(Loc.GetString("ban-panel-weeks"), (int) Multipliers.Weeks);
        MultiplierOption.AddItem(Loc.GetString("ban-panel-months"), (int) Multipliers.Months);
        MultiplierOption.AddItem(Loc.GetString("ban-panel-years"), (int) Multipliers.Years);
        MultiplierOption.AddItem(Loc.GetString("ban-panel-permanent"), (int) Multipliers.Permanent);
        MultiplierOption.SelectId((int) Multipliers.Minutes);
        OnMultiplierChanged();

        Tabs.SetTabTitle((int) TabNumbers.BasicInfo, Loc.GetString("ban-panel-tabs-basic"));
        //Tabs.SetTabTitle((int) TabNumbers.Text, Loc.GetString("ban-panel-tabs-reason"));
        Tabs.SetTabTitle((int) TabNumbers.Players, Loc.GetString("ban-panel-tabs-players"));
        Tabs.SetTabTitle((int) TabNumbers.Roles, Loc.GetString("ban-panel-tabs-role"));
        Tabs.SetTabVisible((int) TabNumbers.Roles, false);

        TypeOption.AddItem(Loc.GetString("ban-panel-select"), (int) Types.None);
        TypeOption.AddItem(Loc.GetString("ban-panel-server"), (int) Types.Server);
        TypeOption.AddItem(Loc.GetString("ban-panel-role"), (int) Types.Role);

        ReasonTextEdit.Placeholder = new Rope.Leaf(Loc.GetString("ban-panel-reason"));

        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        foreach (var proto in prototypeManager.EnumeratePrototypes<DepartmentPrototype>())
        {
            CreateRoleGroup(proto.ID, proto.Roles, proto.Color);
        }

        CreateRoleGroup("Antagonist", prototypeManager.EnumeratePrototypes<AntagPrototype>().Select(p => p.ID), Color.Red);
    }

    private void CreateRoleGroup(string roleName, IEnumerable<string> roleList, Color color)
    {
        var outerContainer = new BoxContainer
        {
            Name = $"{roleName}GroupOuterBox",
            HorizontalExpand = true,
            VerticalExpand = true,
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(4)
        };
        var departmentCheckbox = new CheckBox
        {
            Name = $"{roleName}GroupCheckbox",
            Text = roleName,
            Modulate = color,
            HorizontalAlignment = HAlignment.Left
        };
        outerContainer.AddChild(departmentCheckbox);
        var innerContainer = new BoxContainer
        {
            Name = $"{roleName}GroupInnerBox",
            HorizontalExpand = true,
            Orientation = BoxContainer.LayoutOrientation.Horizontal
        };
        departmentCheckbox.OnToggled += args =>
        {
            foreach (var child in innerContainer.Children)
            {
                if (child is CheckBox c)
                {
                    c.Pressed = args.Pressed;
                }
            }
        };
        outerContainer.AddChild(innerContainer);
        foreach (var role in roleList)
        {
            AddRoleCheckbox(role, innerContainer, departmentCheckbox);
        }
        RolesContainer.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = color
            }
        });
        RolesContainer.AddChild(outerContainer);
        RolesContainer.AddChild(new HSeparator());
    }

    private void AddRoleCheckbox(string role, Control container, CheckBox header)
    {
        var roleCheckbox = new CheckBox
        {
            Name = $"{role}RoleCheckbox",
            Text = role
        };
        roleCheckbox.OnToggled += args =>
        {
            if (args is { Pressed: true, Button.Parent: { } } && args.Button.Parent.Children.Where(e => e is CheckBox).All(e => ((CheckBox) e).Pressed))
                header.Pressed = args.Pressed;
            else
                header.Pressed = false;
        };
        container.AddChild(roleCheckbox);
        _roleCheckboxes.Add(roleCheckbox);
    }

    public void UpdateBanFlag(bool newFlag)
    {
        HasBanFlag = newFlag;
        SubmitButton.Visible = HasBanFlag;
        ModulateSelfOverride = HasBanFlag ? Color.Red : null;
    }

    public void UpdatePlayerData(string playerName)
    {
        if (string.IsNullOrEmpty(playerName))
        {
            PlayerNameLine.ModulateSelfOverride = Color.Red;
            ErrorLevel |= ErrorLevelEnum.PlayerName;
            UpdateSubmitEnabled();
            return;
        }
        PlayerNameLine.ModulateSelfOverride = null;
        ErrorLevel &= ~ErrorLevelEnum.PlayerName;
        UpdateSubmitEnabled();
        PlayerUsername = playerName;
        PlayerNameLine.Text = playerName;
    }

    [Flags]
    private enum ErrorLevelEnum : byte
    {
        None = 0,
        Minutes = 1 << 0,
        PlayerName = 1 << 1,
        IpAddress = 1 << 2,
        Hwid = 1 << 3,
    }

    private ErrorLevelEnum ErrorLevel { get; set; }

    private void OnMinutesChanged(LineEdit.LineEditEventArgs args)
    {
        TimeLine.Text = args.Text;
        if (!double.TryParse(args.Text, out var result))
        {
            ExpiresLabel.Text = "err";
            ErrorLevel |= ErrorLevelEnum.Minutes;
            TimeLine.ModulateSelfOverride = Color.Red;
            UpdateSubmitEnabled();
            return;
        }

        ErrorLevel &= ~ErrorLevelEnum.Minutes;
        TimeLine.ModulateSelfOverride = null;
        TimeEntered = result;
        UpdateSubmitEnabled();
        UpdateExpiresLabel();
    }

    private void OnMultiplierChanged()
    {
        TimeLine.Editable = MultiplierOption.SelectedId != (int) Multipliers.Permanent;
        Multiplier = MultiplierOption.SelectedId switch
        {
            (int) Multipliers.Minutes => 1,
            (int) Multipliers.Hours => 60,
            (int) Multipliers.Days => 60 * 24,
            (int) Multipliers.Weeks => 60 * 24 * 7,
            (int) Multipliers.Months => 60 * 24 * 30,
            (int) Multipliers.Years => 60 * 24 * 365,
            (int) Multipliers.Permanent => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(MultiplierOption.SelectedId), "Multiplier out of range")
        };
        UpdateExpiresLabel();
    }

    private void UpdateExpiresLabel()
    {
        var minutes = (uint) (TimeEntered * Multiplier);
        ExpiresLabel.Text = minutes == 0
            ? $"{Loc.GetString("admin-note-editor-expiry-label")} {Loc.GetString("server-ban-string-never")}"
            : $"{Loc.GetString("admin-note-editor-expiry-label")} {DateTime.Now + TimeSpan.FromMinutes(minutes):yyyy/MM/dd HH:mm:ss}";
    }

    private void OnIpChanged()
    {
        IpAddress = IpLine.Text;
        if (LastConnCheckbox.Pressed && string.IsNullOrEmpty(IpAddress) || !IpCheckbox.Pressed)
        {
            IpAddress = null;
            ErrorLevel &= ~ErrorLevelEnum.IpAddress;
            IpLine.ModulateSelfOverride = null;
            UpdateSubmitEnabled();
            return;
        }
        var ip = IpLine.Text;
        var hid = "0";
        if (ip.Contains('/'))
        {
            var split = ip.Split('/');
            ip = split[0];
            hid = split[1];
        }

        // IPAddress.TryParse is considered a sandbox violation :[
        if (!IPRegex.IsMatch(ip) || !uint.TryParse(hid, out var hidInt) || hidInt > 128 || hidInt > 32 && !ip.Contains(':'))
        {
            ErrorLevel |= ErrorLevelEnum.IpAddress;
            IpLine.ModulateSelfOverride = Color.Red;
            UpdateSubmitEnabled();
            return;
        }

        ErrorLevel &= ~ErrorLevelEnum.IpAddress;
        IpLine.ModulateSelfOverride = null;
        UpdateSubmitEnabled();
    }

    private void OnHwidChanged()
    {
        var hwidString = HwidLine.Text;
        if (HwidCheckbox.Pressed && !(string.IsNullOrEmpty(hwidString) && LastConnCheckbox.Pressed) && !HwidRegex.IsMatch(hwidString) )
        {
            ErrorLevel |= ErrorLevelEnum.Hwid;
            HwidLine.ModulateSelfOverride = Color.Red;
            UpdateSubmitEnabled();
            return;
        }

        ErrorLevel &= ~ErrorLevelEnum.Hwid;
        HwidLine.ModulateSelfOverride = null;
        UpdateSubmitEnabled();

        if (LastConnCheckbox.Pressed || !HwidCheckbox.Pressed)
        {
            Hwid = null;
            return;
        }
        Hwid = Enumerable.Range(0, hwidString.Length / 2)
            .Select(x => byte.Parse(hwidString.Substring(x * 2, 2), NumberStyles.HexNumber))
            .ToArray();
    }

    private void OnTypeChanged()
    {
        TypeOption.ModulateSelfOverride = null;
        Tabs.SetTabVisible((int) TabNumbers.Roles, TypeOption.SelectedId == (int) Types.Role);
    }

    private void UpdateSubmitEnabled()
    {
        SubmitButton.Disabled = ErrorLevel != ErrorLevelEnum.None;
    }

    private void OnPlayerNameChanged()
    {
        if (PlayerUsername == PlayerNameLine.Text)
            return;
        PlayerUsername = PlayerNameLine.Text;
        if (!PlayerCheckbox.Pressed)
            return;
        if (string.IsNullOrWhiteSpace(PlayerUsername))
            ErrorLevel |= ErrorLevelEnum.PlayerName;
        else
            ErrorLevel &= ~ErrorLevelEnum.PlayerName;

        UpdateSubmitEnabled();
        PlayerChanged?.Invoke(PlayerUsername);
    }

    public void OnPlayerSelectionChanged(PlayerInfo? player)
    {
        PlayerNameLine.Text = player?.Username ?? string.Empty;
        OnPlayerNameChanged();
    }

    private void ResetTextEditor(GUIBoundKeyEventArgs _)
    {
        ReasonTextEdit.ModulateSelfOverride = null;
        ReasonTextEdit.OnKeyBindDown -= ResetTextEditor;
    }

    private void SubmitButtonOnOnPressed(BaseButton.ButtonEventArgs obj)
    {
        string[]? roles = null;
        if (TypeOption.SelectedId == (int) Types.Role)
        {
            var rolesList = new List<string>();
            if (_roleCheckboxes.Count == 0)
                throw new DebugAssertException("RoleCheckboxes was empty");

            rolesList.AddRange(_roleCheckboxes.Where(c => c is { Pressed: true, Text: { } }).Select(c => c.Text!));

            if (rolesList.Count == 0)
            {
                Tabs.CurrentTab = (int) TabNumbers.Roles;
                return;
            }

            roles = rolesList.ToArray();
        }

        if (TypeOption.SelectedId == (int) Types.None)
        {
            TypeOption.ModulateSelfOverride = Color.Red;
            Tabs.CurrentTab = (int) TabNumbers.BasicInfo;
            return;
        }

        var reason = Rope.Collapse(ReasonTextEdit.TextRope);
        if (string.IsNullOrWhiteSpace(reason))
        {
            //Tabs.CurrentTab = (int) TabNumbers.Text;
            Tabs.CurrentTab = (int) TabNumbers.BasicInfo;
            ReasonTextEdit.GrabKeyboardFocus();
            ReasonTextEdit.ModulateSelfOverride = Color.Red;
            ReasonTextEdit.OnKeyBindDown += ResetTextEditor;
            return;
        }

        if (LastButtonReset is null)
        {
            LastButtonReset = 0;
            SubmitButton.ModulateSelfOverride = Color.Red;
            SubmitButton.Text = Loc.GetString("ban-panel-confirm");
            return;
        }

        var player = PlayerCheckbox.Pressed ? PlayerUsername : null;
        var useLastIp = IpCheckbox.Pressed && LastConnCheckbox.Pressed && IpAddress is null;
        var useLastHwid = HwidCheckbox.Pressed && LastConnCheckbox.Pressed && Hwid is null;
        var severity = (NoteSeverity) SeverityOption.SelectedId;
        BanSubmitted?.Invoke(player, IpAddress, useLastIp, Hwid, useLastHwid, (uint) (TimeEntered * Multiplier), reason, severity, roles);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (LastButtonReset is null)
            return;

        LastButtonReset += args.DeltaSeconds;
        if (LastButtonReset < 3)
            return;
        LastButtonReset = null;
        SubmitButton.ModulateSelfOverride = null;
        SubmitButton.Text = Loc.GetString("ban-panel-submit");
    }
}
