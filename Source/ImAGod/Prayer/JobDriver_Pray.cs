using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ImAGod
{
    /// <summary>A follower prays at a divine shrine as recreation, offering faith in proportion to their certainty.</summary>
    public class JobDriver_Pray : JobDriver
    {
        /// <summary>Faith a fully certain follower offers per hour of prayer.</summary>
        public const float FaithPerHour = 4f;
        private const int MinTicksForBlessing = 600;
        private const float CertaintyGain = 0.01f;

        private int ticksPrayed;

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => !DivinityUtility.IsFollower(pawn));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            Toil pray = ToilMaker.MakeToil("Pray");
            pray.handlingFacing = true;
            pray.defaultCompleteMode = ToilCompleteMode.Delay;
            pray.defaultDuration = job.def.joyDuration;
            pray.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                ticksPrayed += delta;
                float certainty = pawn.ideo?.Certainty ?? 0f;
                GameComponent_Divinity.Instance?.AddFaith(FaithPerHour * certainty * delta / GenDate.TicksPerHour);
                JoyUtility.JoyTickCheckEnd(pawn, delta, JoyTickFullJoyAction.EndJob, 1f, job.targetA.Thing as Building);
            };
            pray.AddFinishAction(() =>
            {
                if (ticksPrayed < MinTicksForBlessing)
                    return;
                pawn.ideo?.OffsetCertainty(CertaintyGain);
                DivinityUtility.GainMemory(pawn, IAG_DefOf.IAG_Prayed);
            });
            yield return pray;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksPrayed, "ticksPrayed");
        }
    }

    public class JoyGiver_Pray : JoyGiver_InteractBuildingInteractionCell
    {
        public override bool CanBeGivenTo(Pawn pawn) => DivinityUtility.IsFollower(pawn) && base.CanBeGivenTo(pawn);

        public override Job TryGiveJob(Pawn pawn) => DivinityUtility.IsFollower(pawn) ? base.TryGiveJob(pawn) : null;

        public override Job TryGiveJobInGatheringArea(Pawn pawn, IntVec3 gatheringSpot, float maxRadius = -1f) =>
            DivinityUtility.IsFollower(pawn) ? base.TryGiveJobInGatheringArea(pawn, gatheringSpot, maxRadius) : null;
    }
}
