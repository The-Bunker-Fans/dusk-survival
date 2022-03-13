using System.Threading;
using Content.Server.Disease.Components;
using Content.Shared.Disease;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Examine;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Server.Hands.Components;
using Content.Server.Nutrition.EntitySystems;
using Content.Server.Paper;
using Content.Server.Tools.Components;
using Robust.Shared.Random;
using Robust.Shared.Player;

namespace Content.Server.Disease
{
    /// Everything that's about identifying, making vaccines for, etc diseases is in here

    public sealed class DiseaseDiagnosisSystem : EntitySystem
    {
        [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly InventorySystem _inventorySystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DiseaseSwabComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<DiseaseSwabComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<DiseaseDiagnoserComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
            SubscribeLocalEvent<DiseaseVaccineCreatorComponent, AfterInteractUsingEvent>(OnAfterInteractUsingVaccine);
            SubscribeLocalEvent<TargetSwabSuccessfulEvent>(OnTargetSwabSuccessful);
            SubscribeLocalEvent<SwabCancelledEvent>(OnSwabCancelled);
        }

        private void OnAfterInteract(EntityUid uid, DiseaseSwabComponent swab, AfterInteractEvent args)
        {
            IngestionBlockerComponent blocker;
            if (swab.CancelToken != null)
            {
                swab.CancelToken.Cancel();
                swab.CancelToken = null;
                return;
            }
            if (args.Target == null)
                return;

            if (!args.CanReach)
                return;

            if (swab.CancelToken != null)
                return;

            if (!TryComp<DiseaseCarrierComponent>(args.Target, out var carrier))
                return;

            if (swab.Used)
            {
                _popupSystem.PopupEntity(Loc.GetString("swab-already-used"), args.User, Filter.Entities(args.User));
                return;
            }

            if (_inventorySystem.TryGetSlotEntity(args.Target.Value, "mask", out var maskUid) &&
                EntityManager.TryGetComponent(maskUid, out blocker) &&
                blocker.Enabled)
            {
                _popupSystem.PopupEntity(Loc.GetString("swab-mask-blocked", ("target", args.Target), ("mask", maskUid)), args.User, Filter.Entities(args.User));
                return;
            }

            swab.CancelToken = new CancellationTokenSource();
            _doAfterSystem.DoAfter(new DoAfterEventArgs(args.User, swab.SwabDelay, swab.CancelToken.Token, target: args.Target)
            {
                BroadcastFinishedEvent = new TargetSwabSuccessfulEvent(args.User, args.Target, swab, carrier),
                BroadcastCancelledEvent = new SwabCancelledEvent(swab),
                BreakOnTargetMove = true,
                BreakOnUserMove = true,
                BreakOnStun = true,
                NeedHand = true
            });
        }

        private void OnAfterInteractUsing(EntityUid uid, DiseaseDiagnoserComponent component, AfterInteractUsingEvent args)
        {
            if (args.Handled || !args.CanReach)
                return;

            if (!HasComp<HandsComponent>(args.User) || HasComp<ToolComponent>(args.Used))
                return;

            if (!TryComp<DiseaseSwabComponent>(args.Used, out var swab))
            {
                _popupSystem.PopupEntity(Loc.GetString("diagnoser-cant-use-swab", ("machine", uid), ("swab", args.Used)), uid, Filter.Entities(args.User));
                return;
            }
            _popupSystem.PopupEntity(Loc.GetString("diagnoser-insert-swab", ("machine", uid), ("swab", args.Used)), uid, Filter.Entities(args.User));

            component.Disease = swab.Disease;
            EntityManager.DeleteEntity(args.Used);

            // spawn a piece of paper.
            var printed = EntityManager.SpawnEntity(component.PrinterOutput, Transform(uid).Coordinates);

            if (!TryComp<PaperComponent>(printed, out var paper))
                return;

            var reportTitle = string.Empty;
            var contents = string.Empty;
            if (component.Disease != null)
            {
                reportTitle = Loc.GetString("diagnoser-disease-report", ("disease", component.Disease.Name));
                contents = AssembleDiseaseReport(component.Disease);
            } else
            {
                reportTitle = Loc.GetString("diagnoser-disease-report-none");
                contents = Loc.GetString("diagnoser-disease-report-none-contents");
            }
            MetaData(printed).EntityName = reportTitle;

            paper.SetContent(contents);
        }


        private void OnAfterInteractUsingVaccine(EntityUid uid, DiseaseVaccineCreatorComponent component, AfterInteractUsingEvent args)
        {
            if (args.Handled || !args.CanReach)
                return;

            if (!HasComp<HandsComponent>(args.User) || HasComp<ToolComponent>(args.Used))
                return;

            if (!TryComp<DiseaseSwabComponent>(args.Used, out var swab) || swab.Disease == null || !swab.Disease.Infectious)
            {
                _popupSystem.PopupEntity(Loc.GetString("diagnoser-cant-use-swab", ("machine", uid), ("swab", args.Used)), uid, Filter.Entities(args.User));
                return;
            }
            _popupSystem.PopupEntity(Loc.GetString("diagnoser-insert-swab", ("machine", uid), ("swab", args.Used)), uid, Filter.Entities(args.User));

            component.Disease = swab.Disease;
            EntityManager.DeleteEntity(args.Used);

            // spawn a vaccine
            var vaxx = EntityManager.SpawnEntity(component.MachineOutput, Transform(uid).Coordinates);

            if (!TryComp<DiseaseVaccineComponent>(vaxx, out var vaxxComp))
                return;

            vaxxComp.Disease = component.Disease;
        }


        private void OnExamined(EntityUid uid, DiseaseSwabComponent swab, ExaminedEvent args)
        {
            if (args.IsInDetailsRange)
            {
                if (swab.Used)
                {
                    args.PushMarkup(Loc.GetString("swab-used"));
                }
                else
                {
                    args.PushMarkup(Loc.GetString("swab-unused"));
                }
            }
        }

        private void OnTargetSwabSuccessful(TargetSwabSuccessfulEvent args)
        {
            if (args.Target == null)
                return;

            args.Swab.Used = true;
            _popupSystem.PopupEntity(Loc.GetString("swab-swabbed", ("target", args.Target)), args.Target.Value, Filter.Entities(args.User));

            if (args.Swab.Disease != null || args.Carrier.Diseases.Count == 0)
                return;

            args.Swab.Disease = _random.Pick(args.Carrier.Diseases);
        }

        private string AssembleDiseaseReport(DiseasePrototype disease)
        {
            string report = string.Empty;
            report += Loc.GetString("diagnoser-disease-report-name", ("disease", disease.Name));
            report += System.Environment.NewLine;

            if (disease.CureResist <= 0)
            {
                report += Loc.GetString("diagnoser-disease-report-cureresist-none");
            } else if (disease.CureResist <= 0.05)
            {
                report += Loc.GetString("diagnoser-disease-report-cureresist-low");
            } else if (disease.CureResist <= 0.14)
            {
                report += Loc.GetString("diagnoser-disease-report-cureresist-medium");
            } else
            {
                report += Loc.GetString("diagnoser-disease-report-cureresist-high");
            }
            report += System.Environment.NewLine;

            /// Add Cures
            if (disease.Cures.Count == 0)
            {
                report += Loc.GetString("diagnoser-no-cures");
            }
            else
            {
                report += Loc.GetString("diagnoser-cure-has");
                report += System.Environment.NewLine;

                foreach (var cure in disease.Cures)
                {
                    report += cure.CureText();
                    report += System.Environment.NewLine;
                }
            }

            return report;
        }
        private static void OnSwabCancelled(SwabCancelledEvent args)
        {
            args.Swab.CancelToken = null;
        }

        private sealed class SwabCancelledEvent : EntityEventArgs
        {
            public readonly DiseaseSwabComponent Swab;
            public SwabCancelledEvent(DiseaseSwabComponent swab)
            {
                Swab = swab;
            }
        }

        private sealed class TargetSwabSuccessfulEvent : EntityEventArgs
        {
            public EntityUid User { get; }
            public EntityUid? Target { get; }
            public DiseaseSwabComponent Swab { get; }

            public DiseaseCarrierComponent Carrier { get; }

            public TargetSwabSuccessfulEvent(EntityUid user, EntityUid? target, DiseaseSwabComponent swab, DiseaseCarrierComponent carrier)
            {
                User = user;
                Target = target;
                Swab = swab;
                Carrier = carrier;
            }
        }

    }
}
