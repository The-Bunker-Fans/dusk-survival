﻿using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.GameObjects;

namespace Content.Client.Administration.UI.Tabs.PlayerTab;

[GenerateTypedNameReferences]
public partial class PlayerTabEntry : ContainerButton
{
    public EntityUid? PlayerUid;

    public PlayerTabEntry(string username, string character, string job, string antagonist, StyleBox styleBox, bool connected)
    {
        RobustXamlLoader.Load(this);

        UsernameLabel.Text = username;
        if (!connected)
            UsernameLabel.StyleClasses.Add("Disabled");
        JobLabel.Text = job;
        CharacterLabel.Text = character;
        AntagonistLabel.Text = antagonist;
        BackgroundColorPanel.PanelOverride = styleBox;
    }
}
