using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ImAGod
{
    /// <summary>
    /// While divine foresight is active, threats the storyteller is about to send against a player home map are
    /// not fired at once: they are queued for later and announced to the player as a prophecy. The prophecy is
    /// therefore always true, and no extra threats are created.
    /// </summary>
    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.TryFire))]
    public static class Patch_Storyteller_TryFire
    {
        private const int RetryDurationTicks = GenDate.TicksPerDay;
        private const int RevealFactionTier = 2;
        private const int RevealStrengthTier = 3;

        private static readonly Func<IncidentWorker_Raid, IncidentParms, bool> tryResolveRaidFaction =
            AccessTools.MethodDelegate<Func<IncidentWorker_Raid, IncidentParms, bool>>(AccessTools.Method(typeof(IncidentWorker_Raid), "TryResolveRaidFaction"));

        /// <summary>Incidents already foreseen this session, so they are never delayed twice.</summary>
        private static readonly ConditionalWeakTable<FiringIncident, object> foreseen = new ConditionalWeakTable<FiringIncident, object>();

        public static bool Prefix(FiringIncident fi, bool queued, ref bool __result)
        {
            if (queued || !ImAGodMod.Settings.foresightEnabled || fi == null || foreseen.TryGetValue(fi, out _))
                return true;
            GameComponent_Divinity divinity = GameComponent_Divinity.Instance;
            if (divinity == null || !divinity.ForesightActive || divinity.foresightLeadTicks <= 0)
                return true;
            if (fi.def == null || fi.parms == null || fi.sourceQuestPart != null)
                return true;
            if (fi.def.category != IncidentCategoryDefOf.ThreatBig && fi.def.category != IncidentCategoryDefOf.ThreatSmall)
                return true;
            if (!(fi.parms.target is Map map) || !map.IsPlayerHome)
                return true;

            try
            {
                if (!fi.def.Worker.CanFireNow(fi.parms))
                    return true;

                int tier = divinity.TierLevel;
                if (tier >= RevealFactionTier && fi.parms.faction == null && fi.def.Worker is IncidentWorker_Raid raid)
                    tryResolveRaidFaction(raid, fi.parms);

                int fireTick = Find.TickManager.TicksGame + divinity.foresightLeadTicks;
                if (!Find.Storyteller.incidentQueue.Add(new QueuedIncident(fi, fireTick, RetryDurationTicks)))
                    return true;
                foreseen.Add(fi, null);

                Announce(divinity, fi, map, fireTick, tier);
                __result = true;
                return false;
            }
            catch (Exception e)
            {
                Log.Error($"[I'm a God] Failed to foresee incident {fi.def.defName}, letting it fire normally: {e}");
                return true;
            }
        }

        private static void Announce(GameComponent_Divinity divinity, FiringIncident fi, Map map, int fireTick, int tier)
        {
            string when = (fireTick - Find.TickManager.TicksGame).ToStringTicksToPeriod();
            TaggedString text = "IAG_ProphecyText".Translate(fi.def.LabelCap, map.Parent.LabelCap, when);
            if (tier >= RevealFactionTier && fi.parms.faction != null)
                text += "\n\n" + "IAG_ProphecyFaction".Translate(fi.parms.faction.NameColored);
            if (tier >= RevealStrengthTier && fi.parms.points > 0f)
                text += "\n\n" + "IAG_ProphecyStrength".Translate(DescribeStrength(fi.parms.points), fi.parms.points.ToString("0"));

            LetterDef letterDef = fi.def.category == IncidentCategoryDefOf.ThreatBig ? LetterDefOf.ThreatBig : LetterDefOf.ThreatSmall;
            Find.LetterStack.ReceiveLetter("IAG_ProphecyLabel".Translate(fi.def.LabelCap), text, letterDef, new LookTargets(map.Parent));
            divinity.prophecies.Add(new Prophecy { label = fi.def.LabelCap, fireTick = fireTick });
        }

        private static string DescribeStrength(float points)
        {
            if (points < 400f)
                return "IAG_Strength_Small".Translate();
            if (points < 1500f)
                return "IAG_Strength_Medium".Translate();
            if (points < 3500f)
                return "IAG_Strength_Large".Translate();
            return "IAG_Strength_Overwhelming".Translate();
        }
    }
}
