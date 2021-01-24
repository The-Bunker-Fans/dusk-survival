﻿using Content.Client.Chat;
using Content.Client.Interfaces.Chat;
using Content.Client.UserInterface.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client.UserInterface
{
    /// <summary>
    ///     The status effects display on the right side of the screen.
    /// </summary>
    public sealed class AlertsUI : Control
    {
        private const float ChatSeparation = 24f;
        public GridContainer Grid { get; }

        private readonly IChatManager _chatManager;

        public AlertsUI()
        {
            _chatManager = IoCManager.Resolve<IChatManager>();
            LayoutContainer.SetGrowHorizontal(this, LayoutContainer.GrowDirection.Begin);
            LayoutContainer.SetGrowVertical(this, LayoutContainer.GrowDirection.End);
            LayoutContainer.SetAnchorTop(this, 0f);
            LayoutContainer.SetAnchorRight(this, 1f);
            LayoutContainer.SetAnchorBottom(this, 1f);
            LayoutContainer.SetMarginBottom(this, -180);
            LayoutContainer.SetMarginTop(this, 250);
            LayoutContainer.SetMarginRight(this, -10);
            var panelContainer = new PanelContainer
            {
                StyleClasses = {StyleNano.StyleClassTransparentBorderedWindowPanel},
                SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
                SizeFlagsVertical = SizeFlags.None
            };
            AddChild(panelContainer);

            Grid = new GridContainer
            {
                MaxHeight = 64,
                ExpandBackwards = true
            };
            panelContainer.AddChild(Grid);


        }

        protected override void EnteredTree()
        {
            base.EnteredTree();
            _chatManager.OnChatBoxResized += OnChatResized;
            OnChatResized(new ChatResizedEventArgs(ChatBox.InitialChatBottom));
        }

        protected override void ExitedTree()
        {
            base.ExitedTree();
            _chatManager.OnChatBoxResized -= OnChatResized;
        }


        private void OnChatResized(ChatResizedEventArgs chatResizedEventArgs)
        {
            // resize us to fit just below the chatbox
            if (_chatManager.CurrentChatBox != null)
            {
                LayoutContainer.SetMarginTop(this, chatResizedEventArgs.NewBottom + ChatSeparation);
            }
            else
            {
                LayoutContainer.SetMarginTop(this, 250);
            }

        }

        protected override void Resized()
        {
            // TODO: Can rework this once https://github.com/space-wizards/RobustToolbox/issues/1392 is done,
            // this is here because there isn't currently a good way to allow the grid to adjust its height based
            // on constraints, otherwise we would use anchors to lay it out
            base.Resized();
            Grid.MaxHeight = Height;
        }

        protected override Vector2 CalculateMinimumSize()
        {
            // allows us to shrink down to a single row
            return (64, 64);
        }

        protected override void UIScaleChanged()
        {
            Grid.MaxHeight = Height;
            base.UIScaleChanged();
        }
    }
}
