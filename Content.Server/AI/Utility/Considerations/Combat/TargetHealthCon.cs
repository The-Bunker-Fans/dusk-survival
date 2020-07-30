﻿using Content.Server.AI.WorldState;
using Content.Server.AI.WorldState.States;
using Content.Server.GameObjects.Components.Damage;

namespace Content.Server.AI.Utility.Considerations.Combat
{
    public sealed class TargetHealthCon : Consideration
    {
        protected override float GetScore(Blackboard context)
        {
            var target = context.GetState<TargetEntityState>().GetValue();

            if (target == null || !target.TryGetComponent(out BaseDamageableComponent damageableComponent))
            {
                return 0.0f;
            }

            return damageableComponent.TotalDamage / 300.0f;
        }
    }
}
