﻿using Content.Shared.GameObjects.Components.Fluids;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Interfaces.GameObjects.Components;
using Robust.Shared.Interfaces.Serialization;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.Client.GameObjects.Components.Fluids
{
    [UsedImplicitly]
    public class SprayVisualizer : AppearanceVisualizer
    {
        private string _safetyOnState;
        private string _safetyOffState;

        public override void LoadData(YamlMappingNode node)
        {
            base.LoadData(node);

            if (node.TryGetNode("safety_on_state", out var safetyOn))
            {
                _safetyOnState = safetyOn.AsString();
            }

            if (node.TryGetNode("safety_off_state", out var safetyOff))
            {
                _safetyOffState = safetyOff.AsString();
            }
        }

        public override void OnChangeData(AppearanceComponent component)
        {
            base.OnChangeData(component);

            if (component.TryGetData<bool>(SprayVisuals.Safety, out var safety))
            {
                SetSafety(component, safety);
            }
        }

        public override IDeepClone DeepClone()
        {
            return new SprayVisualizer
            {
                _safetyOffState = _safetyOffState,
                _safetyOnState = _safetyOnState
            };
        }

        private void SetSafety(AppearanceComponent component, bool safety)
        {
            var sprite = component.Owner.GetComponent<ISpriteComponent>();

            sprite.LayerSetState(SprayVisualLayers.Base, safety ? _safetyOnState : _safetyOffState);
        }
    }

    public enum SprayVisualLayers : byte
    {
        Base
    }
}
