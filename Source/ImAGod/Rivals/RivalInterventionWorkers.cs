using System.Linq;
using RimWorld;
using Verse;

namespace ImAGod
{
    /// <summary>The rival god whispers to the colony's believers, sowing doubt.</summary>
    public class RivalInterventionWorker_Whispers : RivalInterventionWorker
    {
        private const float CertaintyLoss = 0.02f;

        public override bool CanDoNow(RivalGod rival, Map map) => ImAGodMod.Settings.faithCrisisEnabled;

        public override bool Execute(RivalGod rival, Map map)
        {
            GameComponent_Divinity divinity = GameComponent_Divinity.Instance;
            divinity.AddDoubt(def.amount, "IAG_DoubtReason_Whispers".Translate(rival.NameCap));
            foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction)
            {
                if (DivinityUtility.IsFollower(pawn))
                    pawn.ideo.OffsetCertainty(-CertaintyLoss);
            }
            Find.LetterStack.ReceiveLetter("IAG_Rival_WhispersLabel".Translate(rival.NameCap), "IAG_Rival_WhispersText".Translate(rival.NameCap, def.amount.ToString("0")), LetterDefOf.NegativeEvent);
            return true;
        }
    }

    /// <summary>The rival god sends a vanilla incident, such as a blight, against the colony.</summary>
    public class RivalInterventionWorker_Incident : RivalInterventionWorker
    {
        private IncidentParms MakeParms(Map map)
        {
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.incident.category, map);
            parms.forced = true;
            return parms;
        }

        public override bool CanDoNow(RivalGod rival, Map map) =>
            def.incident != null && map != null && def.incident.Worker.CanFireNow(MakeParms(map));

        public override bool Execute(RivalGod rival, Map map)
        {
            if (!def.incident.Worker.TryExecute(MakeParms(map)))
                return false;
            Messages.Message("IAG_Rival_IncidentMessage".Translate(rival.NameCap, def.incident.label), MessageTypeDefOf.NegativeEvent);
            return true;
        }
    }

    /// <summary>The rival god hurls lightning at the colony's home area.</summary>
    public class RivalInterventionWorker_Storm : RivalInterventionWorker
    {
        public override bool CanDoNow(RivalGod rival, Map map) => map != null && map.areaManager.Home.TrueCount > 0;

        public override bool Execute(RivalGod rival, Map map)
        {
            var cells = map.areaManager.Home.ActiveCells.Where(c => !c.Roofed(map)).InRandomOrder().Take((int)def.amount).ToList();
            if (cells.Count == 0)
                return false;
            foreach (IntVec3 cell in cells)
                map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(map, cell));
            Find.LetterStack.ReceiveLetter("IAG_Rival_StormLabel".Translate(rival.NameCap), "IAG_Rival_StormText".Translate(rival.NameCap), LetterDefOf.NegativeEvent, new TargetInfo(cells[0], map));
            return true;
        }
    }

    /// <summary>The rival god lures a wavering believer into heresy.</summary>
    public class RivalInterventionWorker_Heresy : RivalInterventionWorker
    {
        public override bool CanDoNow(RivalGod rival, Map map) =>
            ImAGodMod.Settings.faithCrisisEnabled &&
            PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Any(p => DivinityUtility.IsFollower(p) && p.ideo.Certainty < def.amount);

        public override bool Execute(RivalGod rival, Map map) =>
            GameComponent_Divinity.Instance.TryStartHeresy(rival, def.amount);
    }
}
