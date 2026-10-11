using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    /// <summary>The god sends its great messenger into the mortal world for a few days.</summary>
    public class DivinePowerWorker_SummonMessenger : DivinePowerWorker
    {
        public override AcceptanceReport CanApplyOn(LocalTargetInfo target, Map map)
        {
            AcceptanceReport report = base.CanApplyOn(target, map);
            if (!report.Accepted)
                return report;
            if (!target.Cell.Standable(map))
                return "IAG_Reason_NotStandable".Translate();
            if (Divinity.PlayerGreatMessenger != null)
                return "IAG_Reason_MessengerPresent".Translate(Divinity.PlayerGreatMessenger.LabelShortCap);
            return true;
        }

        public override bool Apply(LocalTargetInfo target, Map map)
        {
            if (def.pawnKind == null)
                return false;
            Divinity.SummonGreatMessenger(def.pawnKind, target.Cell, map, def.durationDays);
            return true;
        }

        public override string EffectSummary() =>
            "IAG_Effect_Messenger".Translate(def.pawnKind?.label ?? "?", Mathf.RoundToInt(def.durationDays * GenDate.TicksPerDay).ToStringTicksToPeriod());
    }

    /// <summary>A mighty rival god sends its avatar, with an escort of its faithful, to destroy the colony.</summary>
    public class RivalInterventionWorker_Avatar : RivalInterventionWorker
    {
        public override bool CanDoNow(RivalGod rival, Map map) =>
            map != null && rival.HostileToPlayer && !GameComponent_Divinity.Instance.ActiveMessengers.Any(p => p.Map == map && p.Messenger().avatar);

        public override bool Execute(RivalGod rival, Map map)
        {
            Faction faction = rival.Factions.Where(f => f.HostileTo(Faction.OfPlayer)).RandomElementWithFallback();
            if (faction == null)
                return false;
            GameComponent_Divinity divinity = GameComponent_Divinity.Instance;
            Pawn avatar = divinity.SendAvatar(rival, faction, map);
            if (avatar == null)
                return false;

            // The avatar's faithful march with it.
            if (def.amount > 0f)
            {
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
                parms.faction = faction;
                parms.points *= def.amount;
                parms.forced = true;
                IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
            }
            divinity.AddDoubt(6f, "IAG_DoubtReason_AvatarCame".Translate(rival.NameCap));
            Find.LetterStack.ReceiveLetter("IAG_AvatarLabel".Translate(rival.NameCap), "IAG_AvatarText".Translate(rival.NameCap, avatar.kindDef.label, faction.Name),
                LetterDefOf.ThreatBig, avatar);
            return true;
        }
    }
}
