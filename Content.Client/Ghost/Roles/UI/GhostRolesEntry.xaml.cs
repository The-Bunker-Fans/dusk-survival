using System;
using Content.Client.Stylesheets;
using Content.Shared.Ghost.Roles;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Timing;

namespace Content.Client.Ghost.Roles.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class GhostRolesEntry : BoxContainer
    {
        public event Action<GhostRoleInfo>? OnRoleSelected;
        public event Action<GhostRoleInfo>? OnRoleCancelled;
        public event Action<GhostRoleInfo>? OnRoleFollowed;

        public GhostRolesEntry(GhostRoleInfo role)
        {
            RobustXamlLoader.Load(this);

            Title.Text = role.AvailableRoleCount > 1 ? $"{role.Name} ({role.AvailableRoleCount})" : role.Name;
            Description.SetMessage(role.Description);

            RequestButton.Visible = !role.IsRequested;
            CancelButton.Visible = role.IsRequested;

            RequestButton.OnPressed += _ => OnRoleSelected?.Invoke(role);
            CancelButton.OnPressed += _ => OnRoleCancelled?.Invoke(role);
            FollowButton.OnPressed += _ => OnRoleFollowed?.Invoke(role);
        }


    }
}
