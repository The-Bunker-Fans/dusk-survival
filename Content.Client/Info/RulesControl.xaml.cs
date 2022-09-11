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
    [Dependency] private readonly IEntitySystemManager _sysMan = default!;
    [Dependency] private readonly RulesManager _rules = default!;

    public RulesControl()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);
        AddChild(_rules.RulesSection());
    }
}
