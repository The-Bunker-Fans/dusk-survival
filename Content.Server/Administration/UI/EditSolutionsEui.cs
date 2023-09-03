using Content.Server.Chemistry.Components.SolutionManager;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Server.Administration.UI
{
    /// <summary>
    ///     Admin Eui for displaying and editing the reagents in a solution.
    /// </summary>
    [UsedImplicitly]
    public sealed partial class EditSolutionsEui : BaseEui
    {
        [Dependency] private IEntityManager _entityManager = default!;
        public readonly EntityUid Target;

        public EditSolutionsEui(EntityUid entity)
        {
            IoCManager.InjectDependencies(this);
            Target = entity;
        }

        public override void Opened()
        {
            base.Opened();
            StateDirty();
        }

        public override void Closed()
        {
            base.Closed();
            EntitySystem.Get<Systems.AdminVerbSystem>().OnEditSolutionsEuiClosed(Player);
        }

        public override EuiStateBase GetNewState()
        {
            var solutions = _entityManager.GetComponentOrNull<SolutionContainerManagerComponent>(Target)?.Solutions;
            return new EditSolutionsEuiState(Target, solutions);
        }
    }
}
