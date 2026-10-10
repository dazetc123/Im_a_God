using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    /// <summary>
    /// Divine foresight: for a while, threats the storyteller sends against the colony are revealed in a
    /// prophecy and delayed by a lead time that grows with the god's tier. See Patch_Storyteller_TryFire.
    /// </summary>
    public class DivinePowerWorker_Foresight : DivinePowerWorker
    {
        private int DurationTicks => Mathf.RoundToInt(def.durationDays * GenDate.TicksPerDay);
        private int LeadTicks => Mathf.RoundToInt(ScaledAmount * GenDate.TicksPerHour);

        public override AcceptanceReport CanApplyOn(LocalTargetInfo target, Map map)
        {
            if (!ImAGodMod.Settings.foresightEnabled)
                return "IAG_Reason_ForesightDisabled".Translate();
            return base.CanApplyOn(target, map);
        }

        public override bool Apply(LocalTargetInfo target, Map map)
        {
            Divinity.StartForesight(DurationTicks, LeadTicks);
            Find.LetterStack.ReceiveLetter("IAG_ForesightLabel".Translate(), "IAG_ForesightText".Translate(DurationTicks.ToStringTicksToPeriod(), LeadTicks.ToStringTicksToPeriod()), LetterDefOf.PositiveEvent);
            return true;
        }

        public override string EffectSummary() =>
            "IAG_Effect_Foresight".Translate(DurationTicks.ToStringTicksToPeriod(), LeadTicks.ToStringTicksToPeriod());
    }

    /// <summary>Heals the injuries of a pawn of the colony.</summary>
    public class DivinePowerWorker_Mend : DivinePowerWorker
    {
        public override AcceptanceReport CanApplyOn(LocalTargetInfo target, Map map)
        {
            AcceptanceReport report = base.CanApplyOn(target, map);
            if (!report.Accepted)
                return report;
            if (!(target.Thing is Pawn pawn) || (pawn.Faction != Faction.OfPlayer && !pawn.IsPrisonerOfColony))
                return "IAG_Reason_MustTargetColonyPawn".Translate();
            if (!pawn.health.hediffSet.hediffs.Any(h => h is Hediff_Injury))
                return "IAG_Reason_NoInjuries".Translate(pawn.Named("PAWN"));
            return true;
        }

        public override bool Apply(LocalTargetInfo target, Map map)
        {
            Pawn pawn = (Pawn)target.Thing;
            float remaining = ScaledAmount;
            List<Hediff_Injury> injuries = pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>()
                .OrderByDescending(h => h.Bleeding ? 1 : 0).ThenByDescending(h => h.Severity).ToList();
            foreach (Hediff_Injury injury in injuries)
            {
                if (remaining <= 0f)
                    break;
                float heal = Mathf.Min(remaining, injury.Severity);
                injury.Heal(heal);
                remaining -= heal;
            }
            FleckMaker.ThrowMetaIcon(pawn.Position, map, FleckDefOf.HealingCross);
            Messages.Message("IAG_MendDone".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.PositiveEvent);
            return true;
        }

        public override string EffectSummary() => "IAG_Effect_Mend".Translate(ScaledAmount.ToString("0"));
    }

    /// <summary>Drops food from the heavens. The amount grows with the number of followers.</summary>
    public class DivinePowerWorker_Manna : DivinePowerWorker
    {
        private const int MaxCount = 40;

        private int Count => Mathf.Min(MaxCount, Mathf.RoundToInt(def.amount + def.amountPerFollower * (Divinity?.FollowerWeight ?? 0f)));

        public override AcceptanceReport CanApplyOn(LocalTargetInfo target, Map map)
        {
            AcceptanceReport report = base.CanApplyOn(target, map);
            if (!report.Accepted)
                return report;
            if (!target.Cell.Standable(map))
                return "IAG_Reason_NotStandable".Translate();
            return true;
        }

        public override bool Apply(LocalTargetInfo target, Map map)
        {
            var things = new List<Thing>();
            int left = Count;
            while (left > 0)
            {
                Thing thing = ThingMaker.MakeThing(def.thingDef);
                thing.stackCount = Mathf.Min(left, def.thingDef.stackLimit);
                left -= thing.stackCount;
                things.Add(thing);
            }
            DropPodUtility.DropThingsNear(target.Cell, map, things, forbid: false);
            Messages.Message("IAG_MannaDone".Translate(Count, def.thingDef.label), new TargetInfo(target.Cell, map), MessageTypeDefOf.PositiveEvent);
            return true;
        }

        public override string EffectSummary() => "IAG_Effect_Manna".Translate(Count, def.thingDef.label);
    }

    /// <summary>Calls down lightning.</summary>
    public class DivinePowerWorker_Smite : DivinePowerWorker
    {
        private int Strikes => Mathf.Max(1, Mathf.RoundToInt(ScaledAmount));

        public override bool Apply(LocalTargetInfo target, Map map)
        {
            for (int i = 0; i < Strikes; i++)
            {
                IntVec3 cell = i == 0 ? target.Cell : target.Cell + GenRadial.RadialPattern[Rand.Range(1, GenRadial.NumCellsInRadius(def.radius))];
                if (cell.InBounds(map))
                    map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(map, cell));
            }
            return true;
        }

        public override string EffectSummary() => "IAG_Effect_Smite".Translate(Strikes);
    }

    /// <summary>Grants a follower a random inspiration.</summary>
    public class DivinePowerWorker_Inspire : DivinePowerWorker
    {
        public override AcceptanceReport CanApplyOn(LocalTargetInfo target, Map map)
        {
            AcceptanceReport report = base.CanApplyOn(target, map);
            if (!report.Accepted)
                return report;
            if (!(target.Thing is Pawn pawn) || !DivinityUtility.IsFollower(pawn) || pawn.mindState?.inspirationHandler == null)
                return "IAG_Reason_MustTargetFollower".Translate();
            if (pawn.mindState.inspirationHandler.Inspired)
                return "IAG_Reason_AlreadyInspired".Translate(pawn.Named("PAWN"));
            return true;
        }

        public override bool Apply(LocalTargetInfo target, Map map)
        {
            Pawn pawn = (Pawn)target.Thing;
            InspirationHandler handler = pawn.mindState.inspirationHandler;
            InspirationDef inspiration = handler.GetRandomAvailableInspirationDef();
            if (inspiration == null || !handler.TryStartInspiration(inspiration, "IAG_InspirationReason".Translate()))
            {
                Messages.Message("IAG_Reason_CannotInspire".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return true;
        }
    }

    /// <summary>Speeds the growth of crops in an area.</summary>
    public class DivinePowerWorker_Bounty : DivinePowerWorker
    {
        public override bool Apply(LocalTargetInfo target, Map map)
        {
            int grown = 0;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(target.Cell, map, def.radius, true).ToList())
            {
                if (thing is Plant plant && plant.IsCrop && plant.LifeStage != PlantLifeStage.Sowing)
                {
                    plant.Growth = Mathf.Min(1f, plant.Growth + ScaledAmount);
                    plant.DirtyMapMesh(map);
                    grown++;
                }
            }
            if (grown == 0)
            {
                Messages.Message("IAG_Reason_NoCrops".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            Messages.Message("IAG_BountyDone".Translate(grown), new TargetInfo(target.Cell, map), MessageTypeDefOf.PositiveEvent);
            return true;
        }

        public override string EffectSummary() => "IAG_Effect_Bounty".Translate(ScaledAmount.ToStringPercent(), def.radius.ToString("0"));
    }

    /// <summary>Sends hostile humanlikes in an area fleeing in terror.</summary>
    public class DivinePowerWorker_Terror : DivinePowerWorker
    {
        public override bool Apply(LocalTargetInfo target, Map map)
        {
            int fled = 0;
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (!pawn.RaceProps.Humanlike || pawn.Downed || !pawn.HostileTo(Faction.OfPlayer))
                    continue;
                if (!pawn.Position.InHorDistOf(target.Cell, def.radius))
                    continue;
                if (pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee, "IAG_TerrorReason".Translate(), forced: true))
                    fled++;
            }
            if (fled == 0)
            {
                Messages.Message("IAG_Reason_NoEnemies".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            Messages.Message("IAG_TerrorDone".Translate(fled), new TargetInfo(target.Cell, map), MessageTypeDefOf.PositiveEvent);
            return true;
        }

        public override string EffectSummary() => "IAG_Effect_Radius".Translate(def.radius.ToString("0"));
    }

    /// <summary>Brings a dead follower back to life.</summary>
    public class DivinePowerWorker_Resurrect : DivinePowerWorker
    {
        public override AcceptanceReport CanApplyOn(LocalTargetInfo target, Map map)
        {
            AcceptanceReport report = base.CanApplyOn(target, map);
            if (!report.Accepted)
                return report;
            if (!(target.Thing is Corpse corpse) || corpse.InnerPawn == null || !corpse.InnerPawn.RaceProps.Humanlike)
                return "IAG_Reason_MustTargetCorpse".Translate();
            if (corpse.InnerPawn.Ideo != DivinityUtility.PlayerIdeo)
                return "IAG_Reason_NotABeliever".Translate(corpse.InnerPawn.Named("PAWN"));
            if (corpse.GetRotStage() == RotStage.Dessicated)
                return "IAG_Reason_Dessicated".Translate();
            return true;
        }

        public override bool Apply(LocalTargetInfo target, Map map)
        {
            Pawn pawn = ((Corpse)target.Thing).InnerPawn;
            if (!ResurrectionUtility.TryResurrect(pawn))
                return false;
            Messages.Message("IAG_ResurrectDone".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.PositiveEvent);
            return true;
        }
    }

    /// <summary>The god takes a follower as a vessel, granting them divine might for a time.</summary>
    public class DivinePowerWorker_Incarnation : DivinePowerWorker
    {
        public override AcceptanceReport CanApplyOn(LocalTargetInfo target, Map map)
        {
            AcceptanceReport report = base.CanApplyOn(target, map);
            if (!report.Accepted)
                return report;
            if (!(target.Thing is Pawn pawn) || !DivinityUtility.IsFollower(pawn) || pawn.Faction != Faction.OfPlayer)
                return "IAG_Reason_MustTargetFollower".Translate();
            if (pawn.health.hediffSet.HasHediff(def.hediffDef))
                return "IAG_Reason_AlreadyVessel".Translate(pawn.Named("PAWN"));
            return true;
        }

        public override bool Apply(LocalTargetInfo target, Map map)
        {
            Pawn pawn = (Pawn)target.Thing;
            pawn.health.AddHediff(def.hediffDef);
            Find.LetterStack.ReceiveLetter("IAG_IncarnationLabel".Translate(), "IAG_IncarnationText".Translate(pawn.Named("PAWN")), LetterDefOf.PositiveEvent, pawn);
            return true;
        }
    }
}

namespace ImAGod
{
    /// <summary>The prophet preaches a revelation: doubt fades, believers grow certain and heretics may return.</summary>
    public class DivinePowerWorker_Revelation : DivinePowerWorker
    {
        private const float HereticConversionPower = 0.2f;

        public override bool Apply(LocalTargetInfo target, Map map)
        {
            GameComponent_Divinity divinity = Divinity;
            Ideo ideo = DivinityUtility.PlayerIdeo;
            foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction)
            {
                if (DivinityUtility.IsFollower(pawn))
                    pawn.ideo.OffsetCertainty(def.amount);
            }
            int returned = 0;
            foreach (Pawn heretic in divinity.heretics.ToList())
            {
                if (heretic.ideo != null && heretic.ideo.IdeoConversionAttempt(HereticConversionPower, ideo))
                    returned++;
            }
            Find.LetterStack.ReceiveLetter("IAG_RevelationLabel".Translate(), "IAG_RevelationText".Translate(def.amount.ToStringPercent(), (-def.doubtOffset).ToString("0")), LetterDefOf.PositiveEvent);
            return true;
        }

        public override string EffectSummary() =>
            "IAG_Effect_Revelation".Translate((-def.doubtOffset).ToString("0"), def.amount.ToStringPercent());
    }

    /// <summary>The prophet challenges a rival god, humbling its power and warding the colony against it for a while.</summary>
    public class DivinePowerWorker_Challenge : DivinePowerWorker
    {
        private float Reduction => def.amount + def.amountPerFollower * (Divinity?.FollowerWeight ?? 0f);
        private int WardTicks => Mathf.RoundToInt(def.durationDays * GenDate.TicksPerDay);

        public override bool ApplyOnRival(RivalGod rival)
        {
            float before = rival.power;
            rival.Humble(Reduction);
            rival.humbledUntilTick = Find.TickManager.TicksGame + WardTicks;
            Find.LetterStack.ReceiveLetter("IAG_ChallengeLabel".Translate(rival.NameCap),
                "IAG_ChallengeText".Translate(rival.NameCap, (before - rival.power).ToString("0"), WardTicks.ToStringTicksToPeriod()), LetterDefOf.PositiveEvent);
            return true;
        }

        public override string EffectSummary() =>
            "IAG_Effect_Challenge".Translate(Reduction.ToString("0"), WardTicks.ToStringTicksToPeriod());
    }
}
