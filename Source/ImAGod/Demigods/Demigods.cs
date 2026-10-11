using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    /// <summary>A follower raised to demigodhood. Demigods serve the god: their abilities are paid from its faith.</summary>
    public class Demigod : IExposable
    {
        public const int MaxLevel = 10;
        private const float BaseXpPerLevel = 80f;
        private const float XpGrowth = 1.35f;

        public Pawn pawn;
        public int level = 1;
        public float xp;
        public List<DivineNodeDef> learned = new List<DivineNodeDef>();
        public int ascendedTick;

        public static float XpForLevel(int level) => BaseXpPerLevel * Mathf.Pow(XpGrowth, level - 1);

        public float XpToNext => level >= MaxLevel ? 0f : XpForLevel(level);

        public int Points => level - learned.Sum(n => n.pointCost);

        public void GainXp(float amount)
        {
            if (level >= MaxLevel || amount <= 0f)
                return;
            xp += amount;
            while (level < MaxLevel && xp >= XpToNext)
            {
                xp -= XpToNext;
                level++;
                Messages.Message("IAG_DemigodLevelUp".Translate(pawn.Named("PAWN"), level), pawn, MessageTypeDefOf.PositiveEvent);
            }
            if (level >= MaxLevel)
                xp = 0f;
        }

        public bool Knows(DivineNodeDef node) => learned.Contains(node);

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref level, "level", 1);
            Scribe_Values.Look(ref xp, "xp");
            Scribe_Values.Look(ref ascendedTick, "ascendedTick");
            Scribe_Collections.Look(ref learned, "learned", LookMode.Def);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                learned ??= new List<DivineNodeDef>();
                learned.RemoveAll(n => n == null);
            }
        }
    }

    public partial class GameComponent_Divinity
    {
        private const float DemigodXpPerHour = 2f;
        private const float DemigodXpPerFaithSpent = 0.5f;
        private const float DemigodStrain = 0.5f;
        private const float DemigodDeathDoubt = 15f;
        public const float AscensionMinCertainty = 0.8f;

        public List<Demigod> demigods = new List<Demigod>();

        public int MaxDemigods => TierLevel;

        public Demigod DemigodFor(Pawn pawn) => pawn == null ? null : demigods.FirstOrDefault(d => d.pawn == pawn);

        public AcceptanceReport CanAscend(Pawn pawn)
        {
            if (pawn == null || !DivinityUtility.IsFollower(pawn) || pawn.Faction != Faction.OfPlayer || pawn.IsSlave)
                return "IAG_Reason_MustTargetFollower".Translate();
            if (DemigodFor(pawn) != null)
                return "IAG_Reason_AlreadyDemigod".Translate(pawn.Named("PAWN"));
            if (pawn.ideo.Certainty < AscensionMinCertainty)
                return "IAG_Reason_CertaintyTooLow".Translate(pawn.Named("PAWN"), AscensionMinCertainty.ToStringPercent());
            if (demigods.Count >= MaxDemigods)
                return "IAG_Reason_TooManyDemigods".Translate(MaxDemigods);
            return true;
        }

        public void Ascend(Pawn pawn)
        {
            demigods.Add(new Demigod { pawn = pawn, ascendedTick = Find.TickManager.TicksGame });
            if (!pawn.health.hediffSet.HasHediff(IAG_DefOf.IAG_Demigod))
                pawn.health.AddHediff(IAG_DefOf.IAG_Demigod);
            pawn.skills?.DirtyAptitudes();
            Find.LetterStack.ReceiveLetter("IAG_AscensionLabel".Translate(), "IAG_AscensionText".Translate(pawn.Named("PAWN")), LetterDefOf.PositiveEvent, pawn);
        }

        private void Unmake(Demigod demigod)
        {
            demigods.Remove(demigod);
            Pawn pawn = demigod.pawn;
            if (pawn == null)
                return;
            Hediff hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(IAG_DefOf.IAG_Demigod);
            if (hediff != null)
                pawn.health.RemoveHediff(hediff);
            foreach (DivineNodeDef node in demigod.learned)
            {
                if (node.ability != null)
                    pawn.abilities?.RemoveAbility(node.ability);
            }
            pawn.skills?.DirtyAptitudes();
        }

        public AcceptanceReport CanLearn(Demigod demigod, DivineNodeDef node)
        {
            if (!node.forDemigod)
                return false;
            if (demigod.Knows(node))
                return "IAG_Node_AlreadyUnlocked".Translate();
            if (!domains.Contains(node.domain))
                return "IAG_Node_DomainNotEmbraced".Translate(node.domain.label);
            if (highestTier < node.MinTier)
                return "IAG_Reason_TierTooLow".Translate(node.MinTier);
            if (demigod.level < node.minDemigodLevel)
                return "IAG_Node_DemigodLevel".Translate(node.minDemigodLevel);
            if (node.prerequisites.Count > 0 && !node.prerequisites.Any(demigod.Knows))
                return "IAG_Node_NeedsPrerequisite".Translate(node.prerequisites.Select(p => p.LabelCap.ToString()).ToCommaList(useAnd: false));
            if (demigod.Points < node.pointCost)
                return "IAG_Node_NotEnoughPoints".Translate(node.pointCost);
            return true;
        }

        public void Learn(Demigod demigod, DivineNodeDef node)
        {
            if (!CanLearn(demigod, node).Accepted)
                return;
            demigod.learned.Add(node);
            demigod.pawn.abilities?.GainAbility(node.ability);
            Messages.Message("IAG_DemigodLearned".Translate(demigod.pawn.Named("PAWN"), node.LabelCap), demigod.pawn, MessageTypeDefOf.PositiveEvent, false);
        }

        /// <summary>Whether a demigod may use a power right now, paid from the god's faith.</summary>
        public AcceptanceReport CanDemigodUse(Pawn pawn, DivinePowerDef power)
        {
            if (DemigodFor(pawn) == null)
                return "IAG_Reason_NotDemigod".Translate();
            if (TierLevel < power.minTier)
                return "IAG_Reason_TierTooLow".Translate(power.minTier);
            if (faith < CostOf(power))
                return "IAG_Reason_NotEnoughFaith".Translate(CostOf(power));
            return true;
        }

        public void Notify_DemigodUsedPower(Pawn pawn, DivinePowerDef power, LocalTargetInfo target)
        {
            int cost = CostOf(power);
            faith = Mathf.Max(0f, faith - cost);
            strain += DemigodStrain;
            lastMiracleTick = Find.TickManager.TicksGame;
            if (power.doubtOffset != 0f)
                AddDoubt(power.doubtOffset, "IAG_DoubtReason_Miracle".Translate(power.label));
            DemigodFor(pawn)?.GainXp(cost * DemigodXpPerFaithSpent);
            if (target.IsValid && pawn.MapHeld != null && power.witnessRadius > 0f)
                Witness(pawn.MapHeld, target.Cell, power.witnessRadius);
        }

        private void DemigodsHourlyTick()
        {
            Ideo ideo = DivinityUtility.PlayerIdeo;
            foreach (Demigod demigod in demigods.ToList())
            {
                Pawn pawn = demigod.pawn;
                if (pawn == null || pawn.Destroyed && !pawn.Dead)
                {
                    demigods.Remove(demigod);
                }
                else if (pawn.Dead)
                {
                    demigods.Remove(demigod);
                    AddDoubt(DemigodDeathDoubt, "IAG_DoubtReason_DemigodDied".Translate(pawn.LabelShort));
                    Find.LetterStack.ReceiveLetter("IAG_DemigodFallenLabel".Translate(), "IAG_DemigodFallenText".Translate(pawn.Named("PAWN")), LetterDefOf.Death, pawn.Corpse);
                }
                else if (pawn.Ideo != ideo || pawn.Faction != Faction.OfPlayer)
                {
                    Unmake(demigod);
                    Find.LetterStack.ReceiveLetter("IAG_DemigodLostLabel".Translate(), "IAG_DemigodLostText".Translate(pawn.Named("PAWN")), LetterDefOf.NegativeEvent, pawn);
                }
                else
                {
                    demigod.GainXp(DemigodXpPerHour);
                }
            }
        }

        private void ExposeDemigods()
        {
            Scribe_Collections.Look(ref demigods, "demigods", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                demigods ??= new List<Demigod>();
                demigods.RemoveAll(d => d.pawn == null);
            }
        }
    }

    public class CompProperties_DivinePower : CompProperties_AbilityEffect
    {
        public DivinePowerDef power;

        public CompProperties_DivinePower()
        {
            compClass = typeof(CompAbilityEffect_DivinePower);
        }
    }

    /// <summary>A demigod ability that works a divine power, paid from the god's faith.</summary>
    public class CompAbilityEffect_DivinePower : CompAbilityEffect
    {
        public new CompProperties_DivinePower Props => (CompProperties_DivinePower)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            GameComponent_Divinity divinity = GameComponent_Divinity.Instance;
            Pawn caster = parent.pawn;
            if (divinity == null || !divinity.CanDemigodUse(caster, Props.power).Accepted)
                return;
            if (Props.power.Worker.Apply(target, caster.MapHeld))
                divinity.Notify_DemigodUsedPower(caster, Props.power, target);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            AcceptanceReport report = Props.power.Worker.CanApplyOn(target, parent.pawn.MapHeld);
            if (!report.Accepted && throwMessages && !report.Reason.NullOrEmpty())
                Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
            return report.Accepted && base.Valid(target, throwMessages);
        }

        public override bool GizmoDisabled(out string reason)
        {
            AcceptanceReport report = GameComponent_Divinity.Instance?.CanDemigodUse(parent.pawn, Props.power) ?? false;
            reason = report.Reason;
            return !report.Accepted;
        }

        public override string ExtraTooltipPart()
        {
            GameComponent_Divinity divinity = GameComponent_Divinity.Instance;
            string summary = Props.power.Worker.EffectSummary();
            string cost = divinity == null ? "" : "IAG_DemigodAbilityCost".Translate(divinity.CostOf(Props.power)).ToString();
            return summary.NullOrEmpty() ? cost : cost + "\n" + summary;
        }
    }

    /// <summary>Raises a devout follower to demigodhood.</summary>
    public class DivinePowerWorker_Ascension : DivinePowerWorker
    {
        public override AcceptanceReport CanApplyOn(LocalTargetInfo target, Map map)
        {
            AcceptanceReport report = base.CanApplyOn(target, map);
            if (!report.Accepted)
                return report;
            return Divinity.CanAscend(target.Thing as Pawn);
        }

        public override bool Apply(LocalTargetInfo target, Map map)
        {
            Divinity.Ascend((Pawn)target.Thing);
            return true;
        }

        public override string EffectSummary() =>
            "IAG_Effect_Ascension".Translate(Divinity?.demigods.Count ?? 0, Divinity?.MaxDemigods ?? 0);
    }

    public class HediffComp_Demigod : HediffComp
    {
        public override string CompLabelInBracketsExtra
        {
            get
            {
                Demigod demigod = GameComponent_Divinity.Instance?.DemigodFor(Pawn);
                return demigod == null ? null : "IAG_DemigodLevelShort".Translate(demigod.level).ToString();
            }
        }
    }
}
