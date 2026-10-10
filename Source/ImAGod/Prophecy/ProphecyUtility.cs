using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    /// <summary>
    /// Divine foresight. When the storyteller has decided to send a threat against a player home, the very same
    /// incident is moved into the incident queue for later and announced as a prophecy. It is recorded in the map's
    /// story state at once, exactly as if it had fired, so the storyteller's pacing is unchanged and it never
    /// decides on an extra threat because of the delay. When the queue later fires it, it is executed directly.
    /// </summary>
    public static class ProphecyUtility
    {
        public const int RetryDurationTicks = GenDate.TicksPerDay;
        private const int RevealFactionTier = 2;
        private const int RevealDiseaseTier = 2;
        private const int RevealDetailsTier = 3;

        /// <summary>True while the incident queue is firing queued incidents.</summary>
        public static bool InIncidentQueueTick;

        /// <summary>The prophecy whose incident is executing right now, if any.</summary>
        public static Prophecy Executing;

        private static IncidentCategoryDef diseaseAnimal;

        private static readonly Func<IncidentWorker_Raid, IncidentParms, bool> tryResolveRaidFaction =
            AccessTools.MethodDelegate<Func<IncidentWorker_Raid, IncidentParms, bool>>(AccessTools.Method(typeof(IncidentWorker_Raid), "TryResolveRaidFaction"));

        private static IncidentCategoryDef DiseaseAnimal => diseaseAnimal ??= DefDatabase<IncidentCategoryDef>.GetNamedSilentFail("DiseaseAnimal");

        public static bool IsThreat(IncidentCategoryDef category) =>
            category == IncidentCategoryDefOf.ThreatBig || category == IncidentCategoryDefOf.ThreatSmall;

        public static bool IsForeseeable(IncidentCategoryDef category, int tier) =>
            IsThreat(category) || (tier >= RevealDiseaseTier && (category == IncidentCategoryDefOf.DiseaseHuman || (category != null && category == DiseaseAnimal)));

        /// <summary>Holds back an incident the storyteller is firing and announces it. Returns false to let it fire normally.</summary>
        public static bool TryForesee(FiringIncident fi)
        {
            GameComponent_Divinity divinity = GameComponent_Divinity.Instance;
            if (divinity == null || !divinity.ForesightActive || divinity.foresightLeadTicks <= 0)
                return false;
            if (fi.def == null || fi.parms == null || fi.sourceQuestPart != null)
                return false;
            int tier = divinity.TierLevel;
            if (!IsForeseeable(fi.def.category, tier))
                return false;
            if (!(fi.parms.target is Map map) || !map.IsPlayerHome)
                return false;

            int now = Find.TickManager.TicksGame;
            var prophecy = new Prophecy
            {
                label = fi.def.LabelCap,
                incident = fi.def,
                map = map,
                decisionTick = now,
                fireTick = now + divinity.foresightLeadTicks
            };
            var details = new List<string>();
            try
            {
                if (!fi.def.Worker.CanFireNow(fi.parms))
                    return false;
                if (fi.def.Worker is IncidentWorker_Raid raid)
                    ForeseeRaid(raid, fi.parms, map, tier, divinity, prophecy, details);
                else if (tier >= RevealDetailsTier && IsThreat(fi.def.category) && fi.parms.points > 0f)
                    details.Add("IAG_ProphecyStrength".Translate(DescribeStrength(fi.parms.points), fi.parms.points.ToString("0")));
            }
            catch (Exception e)
            {
                Log.Error($"[I'm a God] Failed to foresee incident {fi.def.defName}, letting it fire normally: {e}");
                return false;
            }
            prophecy.points = fi.parms.points;
            prophecy.details = details.ToLineList();

            if (!Find.Storyteller.incidentQueue.Add(new QueuedIncident(fi, prophecy.fireTick, RetryDurationTicks)))
                return false;

            // From here on the incident is queued, so it must not also fire now.
            try
            {
                map.StoryState.Notify_IncidentFired(fi);
                divinity.prophecies.Add(prophecy);
                Announce(fi, map, prophecy);
            }
            catch (Exception e)
            {
                Log.Error($"[I'm a God] Error while announcing prophecy of {fi.def.defName}: {e}");
            }
            return true;
        }

        private static void ForeseeRaid(IncidentWorker_Raid raid, IncidentParms parms, Map map, int tier, GameComponent_Divinity divinity, Prophecy prophecy, List<string> details)
        {
            if (tier < RevealFactionTier)
                return;
            if (parms.faction == null)
                tryResolveRaidFaction(raid, parms);
            if (parms.faction == null)
                return;
            details.Add("IAG_ProphecyFaction".Translate(parms.faction.NameColored));

            RivalGod rival = divinity.RivalFor(parms.faction.ideos?.PrimaryIdeo);
            prophecy.championRolled = true;
            prophecy.champion = Rand.Chance(divinity.ChampionChance(rival));

            if (tier < RevealDetailsTier)
                return;
            raid.ResolveRaidStrategy(parms, PawnGroupKindDefOf.Combat);
            if (raid.TryResolveRaidArriveMode(parms) && parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms) && parms.spawnCenter.IsValid)
            {
                prophecy.cell = parms.spawnCenter;
                string direction = Direction(parms.spawnCenter, map);
                details.Add(parms.raidArrivalMode.walkIn
                    ? "IAG_ProphecyArrivalWalk".Translate(direction).ToString()
                    : "IAG_ProphecyArrivalOther".Translate(parms.raidArrivalMode.label, direction).ToString());
            }
            if (parms.raidStrategy != null)
                details.Add("IAG_ProphecyStrategy".Translate(parms.raidStrategy.label));
            if (parms.points > 0f)
                details.Add("IAG_ProphecyStrength".Translate(DescribeStrength(parms.points), parms.points.ToString("0")));
            if (prophecy.champion)
                details.Add("IAG_ProphecyChampion".Translate(rival.NameCap));
        }

        private static void Announce(FiringIncident fi, Map map, Prophecy prophecy)
        {
            string when = (prophecy.fireTick - Find.TickManager.TicksGame).ToStringTicksToPeriod();
            TaggedString text = "IAG_ProphecyText".Translate(fi.def.LabelCap, map.Parent.LabelCap, when);
            if (!prophecy.details.NullOrEmpty())
                text += "\n\n" + prophecy.details;
            LetterDef letterDef = fi.def.category == IncidentCategoryDefOf.ThreatBig ? LetterDefOf.ThreatBig
                : IsThreat(fi.def.category) ? LetterDefOf.ThreatSmall : LetterDefOf.NegativeEvent;
            LookTargets look = prophecy.cell.IsValid ? new LookTargets(new TargetInfo(prophecy.cell, map)) : new LookTargets(map.Parent);
            Find.LetterStack.ReceiveLetter("IAG_ProphecyLabel".Translate(fi.def.LabelCap), text, letterDef, look);
        }

        /// <summary>Executes a foreseen incident fired by the incident queue. It was already recorded in the story state.</summary>
        public static bool Execute(Prophecy prophecy, FiringIncident fi)
        {
            Executing = prophecy;
            try
            {
                bool fired = fi.def.Worker.TryExecute(fi.parms);
                if (fired)
                    GameComponent_Divinity.Instance?.Notify_ProphecyFulfilled(prophecy);
                return fired;
            }
            finally
            {
                Executing = null;
            }
        }

        public static string DescribeStrength(float points)
        {
            if (points < 400f)
                return "IAG_Strength_Small".Translate();
            if (points < 1500f)
                return "IAG_Strength_Medium".Translate();
            if (points < 3500f)
                return "IAG_Strength_Large".Translate();
            return "IAG_Strength_Overwhelming".Translate();
        }

        private static readonly string[] CompassKeys = { "E", "NE", "N", "NW", "W", "SW", "S", "SE" };

        public static string Direction(IntVec3 cell, Map map)
        {
            float dx = cell.x - map.Center.x;
            float dz = cell.z - map.Center.z;
            if (Mathf.Sqrt(dx * dx + dz * dz) < Mathf.Min(map.Size.x, map.Size.z) * 0.2f)
                return "IAG_Dir_Center".Translate();
            float angle = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
            int index = Mathf.RoundToInt((angle + 360f) % 360f / 45f) % 8;
            return ("IAG_Dir_" + CompassKeys[index]).Translate();
        }
    }
}
