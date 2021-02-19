using System.Collections.Generic;
using Content.Shared.Materials;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Reflection;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.GameObjects.Components.Materials
{
    /// <summary>
    ///     Component to store data such as "this object is made out of steel".
    ///     This is not a storage system for say smelteries.
    /// </summary>
    [RegisterComponent]
    public class MaterialComponent : Component, ISerializationHooks
    {
        public const string SerializationCache = "mat";

        public override string Name => "Material";

        [DataField("materials")] private List<MaterialDataEntry> _materials = new();

        public Dictionary<object, MaterialPrototype> MaterialTypes { get; }

        void ISerializationHooks.AfterDeserialization()
        {
            if (_materials != null)
            {
                var protoMan = IoCManager.Resolve<IPrototypeManager>();

                foreach (var entry in _materials)
                {
                    var proto = protoMan.Index<MaterialPrototype>(entry.Value);
                    MaterialTypes[entry.Key] = proto;
                }
            }
        }

        public class MaterialDataEntry : ISerializationHooks
        {
            public object Key;

            [DataField("key")]
            public string StringKey;

            [DataField("mat")]
            public string Value;

            void ISerializationHooks.AfterDeserialization()
            {
                var refl = IoCManager.Resolve<IReflectionManager>();

                if (refl.TryParseEnumReference(StringKey, out var @enum))
                {
                    Key = @enum;
                    return;
                }

                Key = StringKey;
            }
        }
    }

    public enum MaterialKeys
    {
        Stack,
    }
}
