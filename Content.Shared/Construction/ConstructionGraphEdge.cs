﻿using System;
using System.Collections.Generic;
using System.IO;
using Content.Shared.GameObjects.Components.Power;
using Content.Shared.Interfaces;
using Robust.Shared.Interfaces.Serialization;
using Robust.Shared.IoC;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;
using YamlDotNet.RepresentationModel;

namespace Content.Shared.Construction
{
    [Serializable, NetSerializable]
    public class ConstructionGraphEdge : IExposeData
    {
        private List<ConstructionGraphStep> _steps = new List<ConstructionGraphStep>();

        [ViewVariables]
        public string Target { get; private set; }

        [ViewVariables]
        public List<IEdgeCondition> Conditions { get; private set; }

        [ViewVariables]
        public List<IEdgeCompleted> Completed { get; private set; }

        [ViewVariables]
        public IReadOnlyList<ConstructionGraphStep> Steps => _steps;

        public void ExposeData(ObjectSerializer serializer)
        {
            var moduleManager = IoCManager.Resolve<IModuleManager>();

            serializer.DataField(this, x => x.Target, "to", string.Empty);
            if (!moduleManager.IsServerModule) return;
            serializer.DataField(this, x => x.Conditions, "conditions", new List<IEdgeCondition>());
            serializer.DataField(this, x => x.Completed, "completed", new List<IEdgeCompleted>());
        }

        public void LoadFrom(YamlMappingNode mapping)
        {
            var serializer = YamlObjectSerializer.NewReader(mapping);
            ExposeData(serializer);

            if (!mapping.TryGetNode("steps", out YamlSequenceNode stepsMapping)) return;

            foreach (var yamlNode in stepsMapping)
            {
                var stepMapping = (YamlMappingNode) yamlNode;
                _steps.Add(LoadStep(stepMapping));
            }
        }

        public static ConstructionGraphStep LoadStep(YamlMappingNode mapping)
        {
            var stepSerializer = YamlObjectSerializer.NewReader(mapping);

            if (mapping.TryGetNode("material", out _))
            {
                var material = new MaterialConstructionGraphStep();
                material.ExposeData(stepSerializer);
                return material;
            }

            if (mapping.TryGetNode("tool", out _))
            {
                var tool = new ToolConstructionGraphStep();
                tool.ExposeData(stepSerializer);
                return tool;
            }

            if (mapping.TryGetNode("prototype", out _))
            {
                var prototype = new PrototypeConstructionGraphStep();
                prototype.ExposeData(stepSerializer);
                return prototype;
            }

            if (mapping.TryGetNode("component", out _))
            {
                var component = new ComponentConstructionGraphStep();
                component.ExposeData(stepSerializer);
                return component;
            }

            if(mapping.TryGetNode("steps", out _))
            {
                var nested = new NestedConstructionGraphStep();
                nested.ExposeData(stepSerializer);
                nested.LoadFrom(mapping);
                return nested;
            }

            throw new ArgumentException("Tried to convert invalid YAML node mapping to ConstructionGraphStep!");
        }
    }
}
