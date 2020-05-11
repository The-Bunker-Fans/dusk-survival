﻿using System;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.GameObjects.Components.Mobs.Abilities
{
    public abstract class SharedLaserAbilityComponent : Component
    {
        public override string Name => "LaserAbility";
        public sealed override uint? NetID => ContentNetIDs.LASER_ABILITY;

        [Serializable, NetSerializable]
        public class FireLaserMessage : ComponentMessage
        {
            public IEntity Player;
            public GridCoordinates Coordinates;

            public FireLaserMessage(IEntity player, GridCoordinates coordinates)
            {
                Player = player;
                Coordinates = coordinates;
            }
        }
    }
}
