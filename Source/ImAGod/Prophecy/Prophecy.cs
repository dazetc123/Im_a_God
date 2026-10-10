using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    /// <summary>
    /// A foreseen event: either a storyteller incident that was held back in the incident queue, or a rival god's
    /// intervention waiting to happen.
    /// </summary>
    public class Prophecy : IExposable
    {
        public string label;
        public string details;
        public Map map;
        public int decisionTick;
        public int fireTick;
        public IntVec3 cell = IntVec3.Invalid;
        public bool fulfilled;

        // Storyteller incident
        public IncidentDef incident;
        public float points;
        public bool championRolled;
        public bool champion;

        // Rival intervention
        public RivalInterventionDef intervention;
        public Ideo rivalIdeo;

        public bool Pending => !fulfilled && Find.TickManager.TicksGame < fireTick + ProphecyUtility.RetryDurationTicks;

        public void ExposeData()
        {
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref details, "details");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref decisionTick, "decisionTick");
            Scribe_Values.Look(ref fireTick, "fireTick");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref fulfilled, "fulfilled");
            Scribe_Defs.Look(ref incident, "incident");
            Scribe_Values.Look(ref points, "points");
            Scribe_Values.Look(ref championRolled, "championRolled");
            Scribe_Values.Look(ref champion, "champion");
            Scribe_Defs.Look(ref intervention, "intervention");
            Scribe_References.Look(ref rivalIdeo, "rivalIdeo");
        }
    }

    public partial class GameComponent_Divinity
    {
        private const float ProphecyFulfilledDoubt = -3f;

        public int foresightUntilTick = -1;
        public int foresightLeadTicks;
        public List<Prophecy> prophecies = new List<Prophecy>();

        public bool ForesightActive => ImAGodMod.Settings.foresightEnabled && Find.TickManager.TicksGame < foresightUntilTick;

        public IEnumerable<Prophecy> PendingProphecies => prophecies.Where(p => p.Pending).OrderBy(p => p.fireTick);

        public void StartForesight(int durationTicks, int leadTicks)
        {
            foresightUntilTick = Find.TickManager.TicksGame + durationTicks;
            foresightLeadTicks = leadTicks;
        }

        /// <summary>The pending prophecy for an incident the incident queue is now firing, if it is one of ours.</summary>
        public Prophecy FindDueProphecy(FiringIncident fi)
        {
            int now = Find.TickManager.TicksGame;
            return prophecies.FirstOrDefault(p => p.incident != null && p.incident == fi.def && !p.fulfilled && p.fireTick <= now &&
                                                  p.map == fi.parms?.target && Mathf.Approximately(p.points, fi.parms.points));
        }

        public void Notify_ProphecyFulfilled(Prophecy prophecy)
        {
            prophecy.fulfilled = true;
            AddDoubt(ProphecyFulfilledDoubt, "IAG_DoubtReason_Prophecy".Translate(prophecy.label));
        }

        private void ProphesyIntervention(RivalGod rival, RivalInterventionDef intervention, Map map)
        {
            int now = Find.TickManager.TicksGame;
            var prophecy = new Prophecy
            {
                label = intervention.LabelCap,
                map = map,
                decisionTick = now,
                fireTick = now + foresightLeadTicks,
                intervention = intervention,
                rivalIdeo = rival.ideo
            };
            prophecies.Add(prophecy);
            Find.LetterStack.ReceiveLetter("IAG_ProphecyLabel".Translate(prophecy.label),
                "IAG_RivalProphecyText".Translate(rival.NameCap, intervention.label, map.Parent.LabelCap, foresightLeadTicks.ToStringTicksToPeriod()),
                LetterDefOf.ThreatSmall, new LookTargets(map.Parent));
        }

        private void ProphecyHourlyTick()
        {
            int now = Find.TickManager.TicksGame;
            foreach (Prophecy prophecy in prophecies.Where(p => p.intervention != null && !p.fulfilled && p.fireTick <= now).ToList())
            {
                prophecy.fulfilled = true;
                RivalGod rival = RivalFor(prophecy.rivalIdeo);
                if (rival != null)
                    ExecuteIntervention(rival, prophecy.intervention, prophecy.map);
            }
            prophecies.RemoveAll(p => p.map == null || now > p.fireTick + ProphecyUtility.RetryDurationTicks + GenDate.TicksPerDay);
        }

        private void ExposeProphecy()
        {
            Scribe_Values.Look(ref foresightUntilTick, "foresightUntilTick", -1);
            Scribe_Values.Look(ref foresightLeadTicks, "foresightLeadTicks");
            Scribe_Collections.Look(ref prophecies, "prophecies", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                prophecies ??= new List<Prophecy>();
        }
    }
}
