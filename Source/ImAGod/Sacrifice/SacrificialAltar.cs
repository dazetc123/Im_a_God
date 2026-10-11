using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ImAGod
{
    /// <summary>Only a god of the path of chaos accepts blood sacrifice.</summary>
    public class PlaceWorker_ChaosOnly : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            GameComponent_Divinity divinity = GameComponent_Divinity.Instance;
            return divinity?.path?.defName == "IAG_Path_Chaos" ? (AcceptanceReport)true : "IAG_Reason_ChaosOnly".Translate();
        }
    }

    /// <summary>
    /// An altar of blood. While it stands, a follower can sacrifice a prisoner or one of the colony's animals to a
    /// god of chaos for a great offering of faith.
    /// </summary>
    public class Building_SacrificialAltar : Building
    {
        private static Texture2D sacrificeIcon;

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
                yield return g;
            if (Faction != Faction.OfPlayer)
                yield break;
            var command = new Command_Action
            {
                defaultLabel = "IAG_OfferSacrifice".Translate(),
                defaultDesc = "IAG_OfferSacrificeDesc".Translate(),
                icon = sacrificeIcon ??= DivinityUtility.LoadIcon("ImAGod/Domains/Bloodlust/goblet"),
                action = () => Find.WindowStack.Add(new FloatMenu(SacrificeOptions()))
            };
            if (Sacrificer() == null)
                command.Disable("IAG_Reason_NoSacrificer".Translate());
            yield return command;
        }

        /// <summary>The prophet if they are here and able, otherwise any able follower.</summary>
        public Pawn Sacrificer()
        {
            Pawn prophet = DivinityUtility.Prophet;
            bool Able(Pawn p) => p != null && p.Spawned && p.Map == Map && !p.Downed && !p.InMentalState && !p.WorkTagIsDisabled(WorkTags.Violent) && DivinityUtility.IsFollower(p);
            if (Able(prophet))
                return prophet;
            return Map.mapPawns.FreeColonistsSpawned.Where(Able).OrderBy(p => p.Position.DistanceToSquared(Position)).FirstOrDefault();
        }

        private List<FloatMenuOption> SacrificeOptions()
        {
            var options = new List<FloatMenuOption>();
            IEnumerable<Pawn> victims = Map.mapPawns.AllPawnsSpawned.Where(SacrificeUtility.CanBeSacrificed);
            foreach (Pawn victim in victims.OrderByDescending(SacrificeUtility.FaithFor))
            {
                Pawn v = victim;
                options.Add(new FloatMenuOption("IAG_SacrificeOption".Translate(v.LabelShortCap, SacrificeUtility.FaithFor(v).ToString("0")), () =>
                {
                    Pawn sacrificer = Sacrificer();
                    if (sacrificer == null)
                        return;
                    Job job = JobMaker.MakeJob(IAG_DefOf.IAG_Sacrifice, v, this);
                    sacrificer.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }));
            }
            if (options.Count == 0)
                options.Add(new FloatMenuOption("IAG_NoVictims".Translate(), null));
            return options;
        }
    }

    public static class SacrificeUtility
    {
        public static bool CanBeSacrificed(Pawn pawn) =>
            !pawn.Dead && (pawn.IsPrisonerOfColony && pawn.RaceProps.Humanlike || pawn.RaceProps.Animal && pawn.Faction == Faction.OfPlayer);

        public static float FaithFor(Pawn victim)
        {
            if (victim.RaceProps.Humanlike)
                return Mathf.Min(220f, 90f + victim.MarketValue / 25f);
            return Mathf.Min(120f, 15f + victim.BodySize * 30f);
        }
    }

    public class JobDriver_Sacrifice : JobDriver
    {
        private const int Duration = 240;

        private Pawn Victim => (Pawn)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(Victim, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnDespawnedOrNull(TargetIndex.B);
            this.FailOn(() => !SacrificeUtility.CanBeSacrificed(Victim));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil rite = Toils_General.Wait(Duration, TargetIndex.A);
            rite.WithProgressBarToilDelay(TargetIndex.A);
            rite.PlaySustainerOrSound(SoundDefOf.Interact_Tend);
            yield return rite;
            Toil kill = ToilMaker.MakeToil("Sacrifice");
            kill.initAction = () =>
            {
                Pawn victim = Victim;
                float faith = SacrificeUtility.FaithFor(victim);
                IntVec3 cell = victim.Position;
                ExecutionUtility.DoExecutionByCut(pawn, victim);
                GameComponent_Divinity.Instance?.Notify_Sacrifice(victim, pawn, job.targetB.Thing, faith, cell);
            };
            kill.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return kill;
        }
    }

    public partial class GameComponent_Divinity
    {
        private const float SacrificeDoubt = -4f;

        public void Notify_Sacrifice(Pawn victim, Pawn sacrificer, Thing altar, float faith, IntVec3 cell)
        {
            float gained = AddFaith(faith);
            AddDoubt(SacrificeDoubt, "IAG_DoubtReason_Sacrifice".Translate(victim.LabelShort));
            Map map = sacrificer.Map;
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (DivinityUtility.IsFollower(pawn) && pawn != sacrificer)
                    DivinityUtility.GainMemory(pawn, IAG_DefOf.IAG_WitnessedSacrifice);
            }
            DemigodFor(sacrificer)?.GainXp(gained);
            DivineVfx.Miracle(map, altar?.Position ?? cell, 3f, true);
            Messages.Message("IAG_Sacrificed".Translate(victim.LabelShort, gained.ToString("0")), new TargetInfo(cell, map), MessageTypeDefOf.PositiveEvent);
        }
    }
}
