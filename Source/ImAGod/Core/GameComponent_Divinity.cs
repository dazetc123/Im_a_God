using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    public class Prophecy : IExposable
    {
        public string label;
        public int fireTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref fireTick, "fireTick");
        }
    }

    /// <summary>
    /// Holds the god's state: faith, divine strain, cooldowns, active foresight and prophecies.
    /// </summary>
    public class GameComponent_Divinity : GameComponent
    {
        private const int RecountInterval = 250;
        private const float StrainDecayPerDay = 0.5f;
        private const float StrainCostFactor = 0.15f;
        private const float WitnessCertaintyGain = 0.04f;
        private const float WitnessConversionPower = 0.03f;

        public float faith;
        public float lifetimeFaith;
        public float strain;
        public int foresightUntilTick = -1;
        public int foresightLeadTicks;
        public List<Prophecy> prophecies = new List<Prophecy>();
        private Dictionary<DivinePowerDef, int> lastUsedTick = new Dictionary<DivinePowerDef, int>();

        private int followerCount;
        private float followerWeight;
        private float devotion;
        private DivinityTierDef tier;

        public static GameComponent_Divinity Instance => Current.Game?.GetComponent<GameComponent_Divinity>();

        public GameComponent_Divinity(Game game)
        {
        }

        public int FollowerCount => followerCount;
        public float FollowerWeight => followerWeight;
        /// <summary>Sum over followers of weight × certainty.</summary>
        public float Devotion => devotion;
        public DivinityTierDef Tier => tier;
        public int TierLevel => tier?.level ?? 0;
        public float FaithCap => tier == null ? 0f : tier.faithCapBase + tier.faithCapPerFollower * followerWeight;
        public float PassiveFaithPerDay => tier == null ? 0f : devotion * tier.passiveFaithPerFollowerPerDay * ImAGodMod.Settings.faithGainMultiplier;
        public bool ForesightActive => Find.TickManager.TicksGame < foresightUntilTick;

        public DivinityTierDef NextTier =>
            DefDatabase<DivinityTierDef>.AllDefsListForReading.Where(t => t.level > TierLevel).OrderBy(t => t.level).FirstOrDefault();

        public override void FinalizeInit()
        {
            Recount();
        }

        public override void GameComponentTick()
        {
            int ticks = Find.TickManager.TicksGame;
            if (ticks % RecountInterval == 0)
                Recount();
            if (ticks % GenDate.TicksPerHour == 0)
                HourlyTick();
        }

        private void HourlyTick()
        {
            AddFaith(devotion * (tier?.passiveFaithPerFollowerPerDay ?? 0f) / GenDate.HoursPerDay);
            strain = Mathf.Max(0f, strain - StrainDecayPerDay / GenDate.HoursPerDay);
            prophecies.RemoveAll(p => p.fireTick < Find.TickManager.TicksGame);
        }

        public void Recount()
        {
            followerCount = 0;
            followerWeight = 0f;
            devotion = 0f;
            if (DivinityUtility.PlayerIdeo != null)
            {
                foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction)
                {
                    if (!DivinityUtility.IsFollower(pawn) || pawn.IsQuestLodger())
                        continue;
                    float weight = DivinityUtility.FollowerWeight(pawn);
                    followerCount++;
                    followerWeight += weight;
                    devotion += weight * (pawn.ideo?.Certainty ?? 0f);
                }
            }
            tier = DefDatabase<DivinityTierDef>.AllDefsListForReading
                .Where(t => followerCount >= t.minFollowers && lifetimeFaith >= t.minLifetimeFaith)
                .OrderByDescending(t => t.level)
                .FirstOrDefault();
            // Without a tier (no followers) faith is frozen rather than lost.
            if (tier != null && faith > FaithCap)
                faith = FaithCap;
        }

        /// <summary>Adds faith offered by believers. Gains are scaled by the mod setting and count towards divine tiers even when faith is full.</summary>
        /// <returns>The faith offered after the setting multiplier.</returns>
        public float AddFaith(float amount)
        {
            if (amount <= 0f || tier == null)
                return 0f;
            amount *= ImAGodMod.Settings.faithGainMultiplier;
            lifetimeFaith += amount;
            faith = Mathf.Min(faith + amount, FaithCap);
            return amount;
        }

        public int CostOf(DivinePowerDef power) =>
            Mathf.RoundToInt(power.faithCost * ImAGodMod.Settings.powerCostMultiplier * (1f + StrainCostFactor * strain));

        public int CooldownTicksLeft(DivinePowerDef power)
        {
            if (!lastUsedTick.TryGetValue(power, out int last))
                return 0;
            return Mathf.Max(0, last + Mathf.RoundToInt(power.cooldownDays * GenDate.TicksPerDay) - Find.TickManager.TicksGame);
        }

        public AcceptanceReport CanInvoke(DivinePowerDef power)
        {
            if (DivinityUtility.PlayerIdeo == null)
                return "IAG_Reason_NoIdeo".Translate();
            if (TierLevel < power.minTier)
                return "IAG_Reason_TierTooLow".Translate(power.minTier);
            int cooldown = CooldownTicksLeft(power);
            if (cooldown > 0)
                return "IAG_Reason_Cooldown".Translate(cooldown.ToStringTicksToPeriod());
            if (faith < CostOf(power))
                return "IAG_Reason_NotEnoughFaith".Translate(CostOf(power));
            if (power.requiresProphet)
            {
                AcceptanceReport prophet = DivinityUtility.CanProphetChannel(null);
                if (!prophet.Accepted)
                    return prophet;
            }
            return true;
        }

        /// <summary>Starts invoking a miracle: applies it at once, or lets the player pick a target first.</summary>
        public void BeginInvoke(DivinePowerDef power)
        {
            AcceptanceReport report = CanInvoke(power);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
                return;
            }

            DivinePowerWorker worker = power.Worker;
            if (power.targetMode == DivineTargetMode.None)
            {
                TryApply(power, LocalTargetInfo.Invalid, Find.CurrentMap);
                return;
            }

            Find.MainTabsRoot.EscapeCurrentTab(false);
            Find.Targeter.BeginTargeting(
                worker.TargetingParameters(),
                target => TryApply(power, target, Find.CurrentMap),
                highlightAction: target =>
                {
                    if (power.radius > 0f && target.IsValid)
                        GenDraw.DrawRadiusRing(target.Cell, power.radius);
                },
                targetValidator: target => target.IsValid,
                mouseAttachment: power.Icon);
        }

        private void TryApply(DivinePowerDef power, LocalTargetInfo target, Map map)
        {
            AcceptanceReport report = CanInvoke(power);
            if (report.Accepted && power.requiresProphet)
                report = DivinityUtility.CanProphetChannel(power.targetMode == DivineTargetMode.None ? null : map);
            if (report.Accepted)
                report = power.Worker.CanApplyOn(target, map);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
                return;
            }

            int cost = CostOf(power);
            if (!power.Worker.Apply(target, map))
                return;

            faith -= cost;
            strain += 1f;
            lastUsedTick[power] = Find.TickManager.TicksGame;

            if (power.requiresProphet && power.prophetStrain > 0f)
            {
                Pawn prophet = DivinityUtility.Prophet;
                if (prophet != null)
                    HealthUtility.AdjustSeverity(prophet, IAG_DefOf.IAG_DivineChanneling, power.prophetStrain);
            }

            if (target.IsValid && map != null && power.witnessRadius > 0f)
                Witness(map, target.Cell, power.witnessRadius);
        }

        /// <summary>Believers who see a miracle grow more certain; non-believers begin to doubt their own faith.</summary>
        private void Witness(Map map, IntVec3 center, float radius)
        {
            Ideo ideo = DivinityUtility.PlayerIdeo;
            List<Pawn> pawns = map.mapPawns.AllPawnsSpawned.ToList();
            foreach (Pawn pawn in pawns)
            {
                if (!pawn.RaceProps.Humanlike || pawn.Downed || !pawn.Awake() || pawn.ideo == null)
                    continue;
                if (pawn.Faction != Faction.OfPlayer && !pawn.IsPrisonerOfColony)
                    continue;
                if (!pawn.Position.InHorDistOf(center, radius))
                    continue;

                if (pawn.Ideo == ideo)
                {
                    pawn.ideo.OffsetCertainty(WitnessCertaintyGain);
                    DivinityUtility.GainMemory(pawn, IAG_DefOf.IAG_WitnessedMiracle);
                }
                else if (pawn.ideo.IdeoConversionAttempt(WitnessConversionPower, ideo))
                {
                    Messages.Message("IAG_ConvertedByMiracle".Translate(pawn.Named("PAWN"), ideo.name), pawn, MessageTypeDefOf.PositiveEvent);
                }
            }
        }

        public void StartForesight(int durationTicks, int leadTicks)
        {
            foresightUntilTick = Find.TickManager.TicksGame + durationTicks;
            foresightLeadTicks = leadTicks;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref faith, "faith");
            Scribe_Values.Look(ref lifetimeFaith, "lifetimeFaith");
            Scribe_Values.Look(ref strain, "strain");
            Scribe_Values.Look(ref foresightUntilTick, "foresightUntilTick", -1);
            Scribe_Values.Look(ref foresightLeadTicks, "foresightLeadTicks");
            Scribe_Collections.Look(ref prophecies, "prophecies", LookMode.Deep);
            Scribe_Collections.Look(ref lastUsedTick, "lastUsedTick", LookMode.Def, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                prophecies ??= new List<Prophecy>();
                lastUsedTick ??= new Dictionary<DivinePowerDef, int>();
                lastUsedTick.RemoveAll(kv => kv.Key == null);
            }
        }
    }
}
