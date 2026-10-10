using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    /// <summary>
    /// Rival gods: the gods of other factions' ideoligions. They grow in power over time; those whose factions are
    /// hostile to the colony occasionally intervene against it, and empower champions in their raids.
    /// </summary>
    public partial class GameComponent_Divinity
    {
        private const float StartingPowerBase = 50f;
        private const float StartingPowerPerSettlement = 5f;
        private const float PowerGrowthPerDay = 1.5f;
        private const float PowerGrowthPerSettlementPerDay = 0.2f;
        private const int MaxCountedSettlements = 25;
        private const int MinTicksBetweenInterventions = 4 * GenDate.TicksPerDay;
        private const int MinTicksBetweenRivalInterventions = 6 * GenDate.TicksPerDay;
        private const float InterventionMtbDaysWeak = 40f;
        private const float InterventionMtbDaysStrong = 12f;
        private const float WardRepelPowerLoss = 0.05f;
        public const float ChampionMinPower = 300f;
        private const float ChampionMaxChance = 0.6f;
        private const float ChampionDeathPowerLoss = 0.15f;
        private const float ChampionDeathFaith = 30f;
        private const float ChampionDeathDoubt = -10f;

        public List<RivalGod> rivals = new List<RivalGod>();
        private int lastAnyInterventionTick = -MinTicksBetweenInterventions;

        public RivalGod RivalFor(Ideo ideo) => ideo == null ? null : rivals.FirstOrDefault(r => r.ideo == ideo);

        public static float RivalActivity => ImAGodMod.Settings.rivalActivity;

        public void SyncRivals()
        {
            Ideo ours = DivinityUtility.PlayerIdeo;
            rivals.RemoveAll(r => !r.IsValid || r.ideo == ours || !r.Factions.Any());
            if (ours == null)
                return;
            foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
            {
                Ideo ideo = faction.ideos?.PrimaryIdeo;
                if (faction.IsPlayer || faction.Hidden || faction.defeated || ideo == null || ideo == ours || RivalFor(ideo) != null)
                    continue;
                var rival = new RivalGod { ideo = ideo };
                rival.power = Mathf.Min(RivalGod.MaxPower, StartingPowerBase + StartingPowerPerSettlement * Mathf.Min(rival.SettlementCount, MaxCountedSettlements));
                rivals.Add(rival);
            }
        }

        private void RivalsHourlyTick()
        {
            if (Find.TickManager.TicksGame % GenDate.TicksPerDay == 0)
            {
                SyncRivals();
                foreach (RivalGod rival in rivals)
                {
                    float growth = PowerGrowthPerDay + PowerGrowthPerSettlementPerDay * Mathf.Min(rival.SettlementCount, MaxCountedSettlements);
                    rival.power = Mathf.Min(RivalGod.MaxPower, rival.power + growth * Mathf.Max(RivalActivity, 0.5f));
                }
            }

            if (RivalActivity <= 0f || tier == null || DivinityUtility.PlayerIdeo == null)
                return;
            int now = Find.TickManager.TicksGame;
            if (now - lastAnyInterventionTick < MinTicksBetweenInterventions)
                return;
            foreach (RivalGod rival in rivals.InRandomOrder())
            {
                if (!rival.IsValid || !rival.HostileToPlayer || now - rival.lastInterventionTick < MinTicksBetweenRivalInterventions)
                    continue;
                float mtb = Mathf.Lerp(InterventionMtbDaysWeak, InterventionMtbDaysStrong, rival.power / RivalGod.MaxPower) / RivalActivity;
                if (Rand.MTBEventOccurs(mtb, GenDate.TicksPerDay, GenDate.TicksPerHour) && TryBeginIntervention(rival))
                    break;
            }
        }

        private bool TryBeginIntervention(RivalGod rival)
        {
            Map map = Find.Maps.Where(m => m.IsPlayerHome).RandomElementWithFallback();
            if (map == null)
                return false;
            if (!DefDatabase<RivalInterventionDef>.AllDefsListForReading
                    .Where(d => rival.power >= d.minPower && d.Worker.CanDoNow(rival, map))
                    .TryRandomElementByWeight(d => d.weight, out RivalInterventionDef intervention))
                return false;

            int now = Find.TickManager.TicksGame;
            rival.lastInterventionTick = now;
            lastAnyInterventionTick = now;

            if (ForesightActive)
            {
                ProphesyIntervention(rival, intervention, map);
                return true;
            }
            ExecuteIntervention(rival, intervention, map);
            return true;
        }

        /// <summary>Works a rival god's intervention, unless the god has been humbled and the colony is warded against it.</summary>
        public void ExecuteIntervention(RivalGod rival, RivalInterventionDef intervention, Map map)
        {
            if (!rival.IsValid || map == null)
                return;
            if (rival.Humbled)
            {
                rival.Humble(rival.power * WardRepelPowerLoss);
                Messages.Message("IAG_Rival_Repelled".Translate(rival.NameCap, intervention.label), MessageTypeDefOf.PositiveEvent);
                return;
            }
            if (intervention.Worker.CanDoNow(rival, map))
                intervention.Worker.Execute(rival, map);
        }

        public float ChampionChance(RivalGod rival)
        {
            if (rival == null || rival.power < ChampionMinPower || RivalActivity <= 0f || rival.Humbled)
                return 0f;
            return Mathf.Clamp(rival.power / RivalGod.MaxPower * ChampionMaxChance * RivalActivity, 0f, ChampionMaxChance);
        }

        public void Notify_ChampionDied(Pawn champion, Ideo ideo)
        {
            RivalGod rival = RivalFor(ideo);
            if (rival == null)
                return;
            rival.Humble(rival.power * ChampionDeathPowerLoss);
            AddFaith(ChampionDeathFaith);
            AddDoubt(ChampionDeathDoubt, "IAG_DoubtReason_ChampionSlain".Translate(rival.NameCap));
            Find.LetterStack.ReceiveLetter("IAG_ChampionSlainLabel".Translate(), "IAG_ChampionSlainText".Translate(champion.Named("PAWN"), rival.NameCap), LetterDefOf.PositiveEvent, new LookTargets(champion.Corpse ?? (Thing)champion));
        }

        private void ExposeRivals()
        {
            Scribe_Collections.Look(ref rivals, "rivals", LookMode.Deep);
            Scribe_Values.Look(ref lastAnyInterventionTick, "lastAnyInterventionTick", -MinTicksBetweenInterventions);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                rivals ??= new List<RivalGod>();
                rivals.RemoveAll(r => r.ideo == null);
            }
        }
    }
}
