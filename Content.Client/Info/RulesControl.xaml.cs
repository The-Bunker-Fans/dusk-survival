﻿using System.IO;
using Content.Shared.CCVar;
using Robust.Client.AutoGenerated;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Configuration;

namespace Content.Client.Info;

[GenerateTypedNameReferences]
public sealed partial class RulesControl : BoxContainer
{
    [Dependency] private readonly IResourceCache _resourceManager = default!;
    [Dependency] private readonly IConfigurationManager _cfgManager = default!;

    public RulesControl()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        var path = "Server Info/" + _cfgManager.GetCVar(CCVars.RulesFile);

        AddChild(new InfoSection(Loc.GetString("ui-rules-header"),
            _resourceManager.ContentFileReadAllText(path), true));
    }
}
