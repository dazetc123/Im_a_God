using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    /// <summary>
    /// Holds the god's state: faith, divine strain and cooldowns. Faith crisis, rival gods and prophecies live in
    /// the other parts of this partial class.
    /// </summary>
    public partial class GameComponent_Divinity : GameComponent
    {
        private const int RecountInterval = 250;
        private const float StrainDecayPerDay = 0.5f;
        private const float StrainCostFactor = 0.15f;
        private const float WitnessCertaintyGain = 0.04f;
        private const float WitnessConversionPower = 0.03f;

        public float faith;
        public float lifetimeFaith;
        public float strain;
        public int lastMiracleTick = -1;
        private Dictionary<DivinePowerDef, int> lastUsedTick = new Dictionary<DivinePowerDef, int>();

        private int followerCount;
        private float followerWeight;
        private float devotion;
        private DivinityTierDef tier;

        private static GameComponent_Divinity cached;

        /// <summary>The current game's component. Cached, since record and thought patches ask for it very often.</summary>
        public static GameComponent_Divinity Instance => Current.Game == null ? null : cached ??= Current.Game.GetComponent<GameComponent_Divinity>();

        public GameComponent_Divinity(Game game)
        {
            cached = this;
        }

        public int FollowerCount => followerCount;
        public float FollowerWeight => followerWeight;
        /// <summary>Sum over followers of weight × certainty.</summary>
        public float Devotion => devotion;
        public DivinityTierDef Tier => tier;
        public int TierLevel => tier?.level ?? 0;
        public float FaithCap => tier == null ? 0f : tier.faithCapBase + tier.faithCapPerFollower * followerWeight;
        public float PassiveFaithPerDay =>
            tier == null ? 0f : devotion * tier.passiveFaithPerFollowerPerDay * PassiveFaithFactor * ImAGodMod.Settings.faithGainMultiplier;

        public DivinityTierDef NextTier =>
            DefDatabase<DivinityTierDef>.AllDefsListForReading.Where(t => t.level > TierLevel).OrderBy(t => t.level).FirstOrDefault();

        public override void FinalizeInit()
        {
            Recount();
            SyncRivals();
            highestTier = Mathf.Max(highestTier, TierLevel);
            PromptForPathIfNeeded();
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
            AddFaith(devotion * (tier?.passiveFaithPerFollowerPerDay ?? 0f) * PassiveFaithFactor / GenDate.HoursPerDay);
            strain = Mathf.Max(0f, strain - StrainDecayPerDay / GenDate.HoursPerDay);
            CrisisHourlyTick();
            RivalsHourlyTick();
            ProphecyHourlyTick();
            DomainsHourlyTick();
            DemigodsHourlyTick();
            PetitionsHourlyTick();
            CommandmentsHourlyTick();
            HolyDaysHourlyTick();
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
            if (devTierOverride >= 0)
                tier = DefDatabase<DivinityTierDef>.AllDefsListForReading.FirstOrDefault(t => t.level == devTierOverride);
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

        public int CostOf(DivinePowerDef power) => DevFreeMiracles ? 0 :
            Mathf.RoundToInt(power.faithCost * ImAGodMod.Settings.powerCostMultiplier * (1f + StrainCostFactor * strain));

        public int CooldownTicksLeft(DivinePowerDef power)
        {
            if (DevFreeMiracles || !lastUsedTick.TryGetValue(power, out int last))
                return 0;
            return Mathf.Max(0, last + Mathf.RoundToInt(power.cooldownDays * GenDate.TicksPerDay) - Find.TickManager.TicksGame);
        }

        public AcceptanceReport CanInvoke(DivinePowerDef power)
        {
            if (DivinityUtility.PlayerIdeo == null)
                return "IAG_Reason_NoIdeo".Translate();
            if (!IsPowerKnown(power))
                return "IAG_Reason_PowerNotUnlocked".Translate();
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
            switch (power.targetMode)
            {
                case DivineTargetMode.None:
                    TryApply(power, LocalTargetInfo.Invalid, Find.CurrentMap);
                    return;
                case DivineTargetMode.RivalGod:
                    var options = rivals.Where(r => r.IsValid)
                        .Select(r => new FloatMenuOption(r.NameCap + " (" + "IAG_RivalPowerShort".Translate(r.power.ToString("0")) + ")", () => TryApplyOnRival(power, r)))
                        .ToList();
                    if (options.Count == 0)
                        Messages.Message("IAG_Reason_NoRivals".Translate(), MessageTypeDefOf.RejectInput, false);
                    else
                        Find.WindowStack.Add(new FloatMenu(options));
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
            if (!Reject(report) && power.Worker.Apply(target, map))
                Commit(power, map, target.IsValid ? target.Cell : IntVec3.Invalid);
        }

        public void TryApplyOnRival(DivinePowerDef power, RivalGod rival)
        {
            AcceptanceReport report = CanInvoke(power);
            if (report.Accepted)
                report = power.Worker.CanApplyOnRival(rival);
            if (!Reject(report) && power.Worker.ApplyOnRival(rival))
                Commit(power, null, IntVec3.Invalid);
        }

        private static bool Reject(AcceptanceReport report)
        {
            if (report.Accepted)
                return false;
            Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
            return true;
        }

        /// <summary>Pays for a miracle that has just been worked and lets believers witness it.</summary>
        private void Commit(DivinePowerDef power, Map map, IntVec3 cell)
        {
            faith -= CostOf(power);
            strain += 1f;
            lastUsedTick[power] = Find.TickManager.TicksGame;
            lastMiracleTick = Find.TickManager.TicksGame;

            if (power.requiresProphet && power.prophetStrain > 0f)
            {
                Pawn prophet = DivinityUtility.Prophet;
                if (prophet != null)
                    HealthUtility.AdjustSeverity(prophet, IAG_DefOf.IAG_DivineChanneling, power.prophetStrain);
            }

            if (power.doubtOffset != 0f)
                AddDoubt(power.doubtOffset, "IAG_DoubtReason_Miracle".Translate(power.label));

            if (cell.IsValid && map != null && power.witnessRadius > 0f)
                Witness(map, cell, power.witnessRadius);

            bool big = power.requiresProphet || power.minTier >= 3;
            if (cell.IsValid && map != null)
                DivineVfx.Miracle(map, cell, power.radius, big);
            else
                DivineVfx.Global(map ?? Find.CurrentMap, big);
            Notify_MiracleWorked(map ?? Find.CurrentMap, power, cell);
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
                    Notify_Witnessed(pawn);
                }
                else if (pawn.ideo.IdeoConversionAttempt(WitnessConversionPower, ideo))
                {
                    Messages.Message("IAG_ConvertedByMiracle".Translate(pawn.Named("PAWN"), ideo.name), pawn, MessageTypeDefOf.PositiveEvent);
                }
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref faith, "faith");
            Scribe_Values.Look(ref lifetimeFaith, "lifetimeFaith");
            Scribe_Values.Look(ref strain, "strain");
            Scribe_Values.Look(ref lastMiracleTick, "lastMiracleTick", -1);
            Scribe_Collections.Look(ref lastUsedTick, "lastUsedTick", LookMode.Def, LookMode.Value);
            ExposeProphecy();
            ExposeCrisis();
            ExposeRivals();
            ExposeDomains();
            ExposeDemigods();
            ExposePetitions();
            ExposeCommandments();
            ExposeHolyDays();
            ExposeDev();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lastUsedTick ??= new Dictionary<DivinePowerDef, int>();
                lastUsedTick.RemoveAll(kv => kv.Key == null);
            }
        }
    }
}
