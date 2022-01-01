using System;
using System.Collections.Generic;
using Content.Client.Administration.UI.CustomControls;
using Content.Shared.Administration;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client.Administration.UI.Tabs.PlayerTab
{
    [GenerateTypedNameReferences]
    public partial class PlayerTab : Control
    {
        private readonly AdminSystem _adminSystem;

        public event Action<BaseButton.ButtonEventArgs>? OnEntryPressed;

        public PlayerTab()
        {
            _adminSystem = EntitySystem.Get<AdminSystem>();
            RobustXamlLoader.Load(this);
            RefreshPlayerList(_adminSystem.PlayerList);
            _adminSystem.PlayerListChanged += RefreshPlayerList;
            OverlayButtonOn.OnPressed += _adminSystem.AdminOverlayOn;
            OverlayButtonOff.OnPressed += _adminSystem.AdminOverlayOff;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            _adminSystem.PlayerListChanged -= RefreshPlayerList;
            OverlayButtonOn.OnPressed -= _adminSystem.AdminOverlayOn;
            OverlayButtonOff.OnPressed -= _adminSystem.AdminOverlayOff;
        }

        private void RefreshPlayerList(IReadOnlyList<PlayerInfo> players)
        {
            PlayerList.RemoveAllChildren();
            var playerManager = IoCManager.Resolve<IPlayerManager>();
            PlayerCount.Text = $"Players: {playerManager.PlayerCount}";

            var altColor = Color.FromHex("#292B38");
            var defaultColor = Color.FromHex("#2F2F3B");

            PlayerList.AddChild(new PlayerTabEntry("Username",
                "Character",
                "Antagonist",
                new StyleBoxFlat(altColor),
                true));
            PlayerList.AddChild(new HSeparator());

            var useAltColor = false;
            foreach (var player in players)
            {
                var entry = new PlayerTabEntry(player.Username,
                    player.CharacterName,
                    player.Antag ? "YES" : "NO",
                    new StyleBoxFlat(useAltColor ? altColor : defaultColor),
                    player.Connected);
                entry.PlayerUid = player.EntityUid;
                entry.OnPressed += args => OnEntryPressed?.Invoke(args);
                PlayerList.AddChild(entry);

                useAltColor ^= true;
            }
        }
    }
}
