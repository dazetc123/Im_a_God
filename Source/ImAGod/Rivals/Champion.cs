using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ImAGod
{
    public class HediffCompProperties_Champion : HediffCompProperties
    {
        public HediffCompProperties_Champion()
        {
            compClass = typeof(HediffComp_Champion);
        }
    }

    /// <summary>Marks a raider blessed by a rival god. Slaying the champion humbles that god.</summary>
    public class HediffComp_Champion : HediffComp
    {
        public Ideo ideo;

        public override string CompLabelInBracketsExtra => GameComponent_Divinity.Instance?.RivalFor(ideo)?.Name;

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            GameComponent_Divinity.Instance?.Notify_ChampionDied(Pawn, ideo);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref ideo, "ideo");
        }
    }

    /// <summary>A strong rival god may bless the strongest raider of a raid sent by its faithful.</summary>
    [HarmonyPatch(typeof(IncidentWorker_Raid), "PostProcessSpawnedPawns")]
    public static class Patch_Raid_PostProcessSpawnedPawns
    {
        public static void Postfix(IncidentParms parms, List<Pawn> pawns)
        {
            GameComponent_Divinity divinity = GameComponent_Divinity.Instance;
            if (divinity == null || parms?.faction == null || pawns.NullOrEmpty() || !parms.faction.HostileTo(Faction.OfPlayer))
                return;
            RivalGod rival = divinity.RivalFor(parms.faction.ideos?.PrimaryIdeo);
            if (rival == null || rival.Humbled)
                return;

            // A foreseen raid already knows whether it is led by a champion.
            Prophecy prophecy = ProphecyUtility.Executing;
            bool champion = prophecy != null && prophecy.championRolled ? prophecy.champion : Rand.Chance(divinity.ChampionChance(rival));
            if (!champion)
                return;

            Pawn chosen = pawns.Where(p => p.RaceProps.Humanlike && !p.Dead && !p.Downed).OrderByDescending(p => p.kindDef.combatPower).FirstOrDefault();
            if (chosen == null)
                return;
            Hediff hediff = chosen.health.AddHediff(IAG_DefOf.IAG_Champion);
            HediffComp_Champion comp = hediff.TryGetComp<HediffComp_Champion>();
            if (comp != null)
                comp.ideo = rival.ideo;
            Messages.Message("IAG_ChampionArrives".Translate(chosen.Named("PAWN"), rival.NameCap), chosen, MessageTypeDefOf.ThreatBig);
        }
    }
}
