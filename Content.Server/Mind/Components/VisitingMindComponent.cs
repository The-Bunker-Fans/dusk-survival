using Robust.Shared.GameObjects;
using Robust.Shared.ViewVariables;

namespace Content.Server.Mind.Components
{
    [RegisterComponent]
    public sealed class VisitingMindComponent : Component
    {
        public override string Name => "VisitingMind";

        [ViewVariables]
        public Mind Mind = default!;
    }

    public class MindUnvisitedMessage : EntityEventArgs
    {
    }
}
