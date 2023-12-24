﻿using System.Numerics;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.UserInterface.Controls
{
    [GenerateTypedNameReferences]
    public partial class SplitBar : BoxContainer
    {
        public Vector2 MinBarSize = new(24, 0);

        public SplitBar()
        {
            RobustXamlLoader.Load(this);
        }

        public void Clear()
        {
            DisposeAllChildren();
        }

        public void AddEntry(float amount, Color color, string? tooltip = null)
        {
            AddChild(new PanelContainer
            {
                ToolTip = tooltip,
                HorizontalExpand = true,
                SizeFlagsStretchRatio = amount,
                MouseFilter = MouseFilterMode.Stop,
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = color,
                    PaddingLeft = 2f,
                    PaddingRight = 2f,
                },
                MinSize = MinBarSize
            });
        }
    }
}
