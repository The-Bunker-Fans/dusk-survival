﻿using Content.Server.GameObjects.EntitySystems;
using Content.Shared.GameObjects.Components;
using Content.Shared.GameObjects.EntitySystems.ActionBlocker;
using Content.Shared.GameObjects.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.ViewVariables;

namespace Content.Server.GameObjects.Components
{
    /// <inheritdoc cref="SharedFoldableComponent"/>
    /// <summary>
    /// This is the server-side component for foldable objects.
    /// The methods in here are called by the <see cref="FoldableSystem"/>
    /// </summary>
    [RegisterComponent]
    [ComponentReference(typeof(SharedFoldableComponent))]
    public class FoldableComponent : SharedFoldableComponent
    {

        /// <summary>
        /// Allows folding/unfolding via a verb.
        /// </summary>
        [Verb]
        private sealed class FoldVerb : Verb<FoldableComponent>
        {
            protected override void GetData(IEntity user, FoldableComponent component, VerbData data)
            {
                if (!ActionBlockerSystem.CanInteract(user) || !component.CanBeFolded)
                {
                    data.Visibility = VerbVisibility.Invisible;
                    return;
                }

                data.Visibility = VerbVisibility.Visible;
                data.Text = component.IsFolded ? Loc.GetString("Unfold") : Loc.GetString("Fold");
                data.IconTexture = "/Textures/Interface/VerbIcons/fold.svg.192dpi.png";
            }

            protected override void Activate(IEntity user, FoldableComponent component)
            {
                EntitySystem.Get<FoldableSystem>().ToggleFold(component);
            }
        }
    }
}
