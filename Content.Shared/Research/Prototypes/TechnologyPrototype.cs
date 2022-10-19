using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;
using Robust.Shared.Utility;

namespace Content.Shared.Research.Prototypes
{
    [NetSerializable, Serializable, Prototype("technology")]
    public readonly record struct TechnologyPrototype : IPrototype
    {
        /// <summary>
        ///     The ID of this technology prototype.
        /// </summary>
        [ViewVariables]
        [IdDataFieldAttribute]
        public string ID { get; } = default!;

        /// <summary>
        ///     The name this technology will have on user interfaces.
        /// </summary>
        [ViewVariables]
        public string Name => _name ?? ID;

        [DataField("name", customTypeSerializer: typeof(LocStringSerializer))]
        private readonly string? _name;

        /// <summary>
        ///     An icon that represent this technology.
        /// </summary>
        [DataField("icon")]
        public SpriteSpecifier Icon { get; } = SpriteSpecifier.Invalid;

        /// <summary>
        ///     A short description of the technology.
        /// </summary>
        [ViewVariables] [DataField("description", customTypeSerializer: typeof(LocStringSerializer))]
        public readonly string Description = "";

        /// <summary>
        ///    The required research points to unlock this technology.
        /// </summary>
        [ViewVariables]
        [DataField("requiredPoints")]
        public int RequiredPoints { get; }

        /// <summary>
        ///     A list of technology IDs required to unlock this technology.
        /// </summary>
        [DataField("requiredTechnologies", customTypeSerializer: typeof(PrototypeIdListSerializer<TechnologyPrototype>))]
        public List<string> RequiredTechnologies { get; } = new();

        /// <summary>
        ///     A list of recipe IDs this technology unlocks.
        /// </summary>
        [ViewVariables]
        [DataField("unlockedRecipes", customTypeSerializer:typeof(PrototypeIdListSerializer<LatheRecipePrototype>))]
        public List<string> UnlockedRecipes { get; } = new();
    }
}
