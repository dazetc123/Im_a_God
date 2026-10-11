using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    /// <summary>
    /// The god's nature: a path (Creation or Chaos), the domains it has embraced, and the miracles it has unlocked
    /// in their trees with divine points.
    /// </summary>
    public partial class GameComponent_Divinity
    {
        /// <summary>Divine points granted by the highest tier ever reached (cumulative).</summary>
        private static readonly int[] TierPoints = { 0, 2, 4, 7, 10 };
        private const float FaithPerBonusPoint = 1000f;
        private const int MaxBonusPoints = 10;

        public DivinePathDef path;
        public List<DivineDomainDef> domains = new List<DivineDomainDef>();
        public List<DivineNodeDef> unlockedNodes = new List<DivineNodeDef>();
        public int highestTier;
        private bool pathDialogDeferred;

        private Dictionary<RecordDef, float> recordOfferingCache;
        private Dictionary<ThoughtDef, float> thoughtOfferingCache;
        private float mentalBreakOffering;

        public bool HasPath => path != null;

        public int DomainSlots => highestTier >= 4 ? 3 : highestTier >= 2 ? 2 : 1;

        public bool CanEmbraceDomain => HasPath && domains.Count < DomainSlots && path.Domains.Any(d => !domains.Contains(d));

        public int TotalPoints => TierPoints[Mathf.Clamp(highestTier, 0, 4)] + Mathf.Min(MaxBonusPoints, Mathf.FloorToInt(lifetimeFaith / FaithPerBonusPoint)) + devBonusPoints;

        public int SpentPoints => unlockedNodes.Sum(n => n.pointCost);

        public int AvailablePoints => TotalPoints - SpentPoints;

        public float FaithToNextBonusPoint =>
            Mathf.FloorToInt(lifetimeFaith / FaithPerBonusPoint) >= MaxBonusPoints ? -1f : FaithPerBonusPoint - lifetimeFaith % FaithPerBonusPoint;

        public bool IsUnlocked(DivineNodeDef node) => unlockedNodes.Contains(node);

        /// <summary>Common miracles are always known; a domain miracle must be unlocked in one of the god's trees.</summary>
        public bool IsPowerKnown(DivinePowerDef power)
        {
            if (power.demigodOnly)
                return false;
            if (!DivinityDefCache.IsDomainPower(power))
                return true;
            return unlockedNodes.Any(n => n.power == power);
        }

        public AcceptanceReport CanUnlock(DivineNodeDef node)
        {
            if (node.forDemigod)
                return false;
            if (IsUnlocked(node))
                return "IAG_Node_AlreadyUnlocked".Translate();
            if (!domains.Contains(node.domain))
                return "IAG_Node_DomainNotEmbraced".Translate(node.domain.label);
            if (highestTier < node.MinTier)
                return "IAG_Reason_TierTooLow".Translate(node.MinTier);
            if (node.prerequisites.Count > 0 && !node.prerequisites.Any(IsUnlocked))
                return "IAG_Node_NeedsPrerequisite".Translate(node.prerequisites.Select(p => p.LabelCap.ToString()).ToCommaList(useAnd: false));
            if (AvailablePoints < node.pointCost)
                return "IAG_Node_NotEnoughPoints".Translate(node.pointCost);
            return true;
        }

        public void Unlock(DivineNodeDef node)
        {
            if (!CanUnlock(node).Accepted)
                return;
            unlockedNodes.Add(node);
            Messages.Message("IAG_Node_Unlocked".Translate(node.LabelCap, node.domain.label), MessageTypeDefOf.PositiveEvent, false);
        }

        public int RespecCost => 150 + 50 * TierLevel;

        public AcceptanceReport CanRespec()
        {
            if (unlockedNodes.Count == 0)
                return "IAG_Reason_NothingToReset".Translate();
            if (faith < RespecCost)
                return "IAG_Reason_NotEnoughFaith".Translate(RespecCost);
            return true;
        }

        /// <summary>Re-sanctification: forget every unlocked miracle and get the divine points back, for a price in faith.</summary>
        public void Respec()
        {
            if (!CanRespec().Accepted)
                return;
            faith -= RespecCost;
            unlockedNodes.Clear();
            Messages.Message("IAG_RespecDone".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }

        public void ChoosePath(DivinePathDef chosen, DivineDomainDef firstDomain)
        {
            if (HasPath)
                return;
            path = chosen;
            domains.Add(firstDomain);
            DirtyOfferings();
            UpdateBlessings();
            Find.LetterStack.ReceiveLetter("IAG_PathChosenLabel".Translate(path.LabelCap),
                "IAG_PathChosenText".Translate(path.label, firstDomain.label, firstDomain.description), LetterDefOf.PositiveEvent);
        }

        public void EmbraceDomain(DivineDomainDef domain)
        {
            if (!CanEmbraceDomain || domain.path != path || domains.Contains(domain))
                return;
            domains.Add(domain);
            DirtyOfferings();
            UpdateBlessings();
            Messages.Message("IAG_DomainEmbraced".Translate(domain.LabelCap), MessageTypeDefOf.PositiveEvent, false);
        }

        /// <summary>Opens the path dialog once a game is running, unless the player chose to decide later.</summary>
        private void PromptForPathIfNeeded()
        {
            if (HasPath || pathDialogDeferred || DivinityUtility.PlayerIdeo == null)
                return;
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (!HasPath && !Find.WindowStack.IsOpen<Dialog_ChooseDivinity>())
                    Find.WindowStack.Add(new Dialog_ChooseDivinity(null));
            });
        }

        public void DeferPathChoice() => pathDialogDeferred = true;

        private void DomainsHourlyTick()
        {
            int previous = highestTier;
            highestTier = Mathf.Max(highestTier, TierLevel);
            if (highestTier > previous && HasPath)
            {
                int oldSlots = previous >= 4 ? 3 : previous >= 2 ? 2 : 1;
                if (DomainSlots > oldSlots)
                    Find.LetterStack.ReceiveLetter("IAG_NewDomainSlotLabel".Translate(), "IAG_NewDomainSlotText".Translate(), LetterDefOf.PositiveEvent);
            }
            UpdateBlessings();
        }

        /// <summary>Keeps every follower blessed by each embraced domain at the god's tier, and no one else.</summary>
        private void UpdateBlessings()
        {
            var wanted = new HashSet<HediffDef>(domains.Where(d => d.blessing != null).Select(d => d.blessing));
            var all = DivinityDefCache.AllBlessings;
            float severity = Mathf.Max(1, TierLevel);
            foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction)
            {
                if (!pawn.RaceProps.Humanlike)
                    continue;
                bool follower = DivinityUtility.IsFollower(pawn);
                foreach (HediffDef blessing in all)
                {
                    Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(blessing);
                    bool want = follower && wanted.Contains(blessing);
                    if (want && hediff == null)
                    {
                        hediff = pawn.health.AddHediff(blessing);
                        hediff.Severity = severity;
                    }
                    else if (want && !Mathf.Approximately(hediff.Severity, severity))
                    {
                        hediff.Severity = severity;
                    }
                    else if (!want && hediff != null)
                    {
                        pawn.health.RemoveHediff(hediff);
                    }
                }
            }
        }

        private void DirtyOfferings() => recordOfferingCache = null;

        private void EnsureOfferings()
        {
            if (recordOfferingCache != null)
                return;
            recordOfferingCache = new Dictionary<RecordDef, float>();
            thoughtOfferingCache = new Dictionary<ThoughtDef, float>();
            mentalBreakOffering = 0f;
            foreach (DivineDomainDef domain in domains)
            {
                foreach (RecordOffering o in domain.recordOfferings)
                    recordOfferingCache[o.record] = recordOfferingCache.TryGetValue(o.record) + o.faith;
                foreach (ThoughtOffering o in domain.thoughtOfferings)
                    thoughtOfferingCache[o.thought] = thoughtOfferingCache.TryGetValue(o.thought) + o.faith;
                mentalBreakOffering += domain.mentalBreakFaith;
            }
        }

        /// <summary>A follower's deed recorded in their records, such as a harvest or a kill.</summary>
        public void Notify_Record(Pawn pawn, RecordDef record, float amount)
        {
            if (amount <= 0f)
                return;
            CommandmentRecord(pawn, record, amount);
            if (domains.Count == 0)
                return;
            EnsureOfferings();
            if (!recordOfferingCache.TryGetValue(record, out float faith))
                return;
            if (pawn.Faction != Faction.OfPlayer || !DivinityUtility.IsFollower(pawn))
                return;
            Offer(pawn, faith * amount);
        }

        public void Notify_Thought(Pawn pawn, ThoughtDef thought)
        {
            CommandmentThought(pawn, thought);
            if (domains.Count == 0)
                return;
            EnsureOfferings();
            if (!thoughtOfferingCache.TryGetValue(thought, out float faith))
                return;
            if (pawn.Faction != Faction.OfPlayer || !DivinityUtility.IsFollower(pawn))
                return;
            Offer(pawn, faith);
        }

        public void Notify_MentalBreak(Pawn pawn)
        {
            if (domains.Count == 0)
                return;
            EnsureOfferings();
            if (mentalBreakOffering > 0f && pawn.MapHeld != null && pawn.MapHeld.IsPlayerHome)
                Offer(null, mentalBreakOffering);
        }

        private void Offer(Pawn pawn, float faith)
        {
            float offered = AddFaith(faith);
            if (pawn != null && offered > 0f)
                DemigodFor(pawn)?.GainXp(offered);
        }

        private void ExposeDomains()
        {
            Scribe_Defs.Look(ref path, "path");
            Scribe_Collections.Look(ref domains, "domains", LookMode.Def);
            Scribe_Collections.Look(ref unlockedNodes, "unlockedNodes", LookMode.Def);
            Scribe_Values.Look(ref highestTier, "highestTier");
            Scribe_Values.Look(ref pathDialogDeferred, "pathDialogDeferred");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                domains ??= new List<DivineDomainDef>();
                unlockedNodes ??= new List<DivineNodeDef>();
                domains.RemoveAll(d => d == null);
                unlockedNodes.RemoveAll(n => n == null);
                DirtyOfferings();
            }
        }
    }

    /// <summary>Lookups over the divine defs, built once after defs are loaded.</summary>
    [StaticConstructorOnStartup]
    public static class DivinityDefCache
    {
        private static readonly HashSet<DivinePowerDef> domainPowers;
        public static readonly List<HediffDef> AllBlessings;

        static DivinityDefCache()
        {
            domainPowers = new HashSet<DivinePowerDef>(DefDatabase<DivineNodeDef>.AllDefsListForReading.Where(n => n.power != null).Select(n => n.power));
            AllBlessings = DefDatabase<DivineDomainDef>.AllDefsListForReading.Where(d => d.blessing != null).Select(d => d.blessing).Distinct().ToList();
        }

        public static bool IsDomainPower(DivinePowerDef power) => domainPowers.Contains(power);
    }
}
