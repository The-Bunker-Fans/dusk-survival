using Content.Client.CharacterInterface;
using Content.Client.HUD.UI;
using Content.Client.Stylesheets;
using Content.Shared.CharacterInfo;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.Client.CharacterInfo.Components
{
    [RegisterComponent]
    public sealed class CharacterInfoComponent : SharedCharacterInfoComponent, ICharacterUI
    {
        [Dependency] private readonly IResourceCache _resourceCache = default!;

        public CharacterInfoControl Control = default!;

        public Control Scene { get; private set; } = default!;
        public UIPriority Priority => UIPriority.Info;

        protected override void OnAdd()
        {
            base.OnAdd();

            Scene = Control = new CharacterInfoControl(_resourceCache);
        }

        public void Opened()
        {
            EntitySystem.Get<CharacterInfoSystem>().RequestCharacterInfo(OwnerUid);
        }

        public sealed class CharacterInfoControl : BoxContainer
        {
            public SpriteView SpriteView { get; }
            public Label NameLabel { get; }
            public Label SubText { get; }

            public BoxContainer ObjectivesContainer { get; }

            public CharacterInfoControl(IResourceCache resourceCache)
            {
                IoCManager.InjectDependencies(this);

                Orientation = LayoutOrientation.Vertical;

                AddChild(new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Children =
                    {
                        (SpriteView = new SpriteView { OverrideDirection = Direction.South, Scale = (2,2)}),
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Vertical,
                            VerticalAlignment = VAlignment.Top,
                            Children =
                            {
                                (NameLabel = new Label()),
                                (SubText = new Label
                                {
                                    VerticalAlignment = VAlignment.Top,
                                    StyleClasses = {StyleNano.StyleClassLabelSubText},

                                })
                            }
                        }
                    }
                });

                AddChild(new Label
                {
                    Text = Loc.GetString("character-info-objectives-label"),
                    HorizontalAlignment = HAlignment.Center
                });
                ObjectivesContainer = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical
                };
                AddChild(ObjectivesContainer);

                AddChild(new Placeholder()
                {
                    PlaceholderText = Loc.GetString("character-info-roles-antagonist-text")
                });
            }
        }
    }
}
