using RimWorld;
using Verse;

namespace ImAGod
{
    public static class DivinityUtility
    {
        public const float SlaveFollowerWeight = 0.5f;
        public const float ProphetOverwhelmedSeverity = 1f;

        /// <summary>The ideoligion whose god the player is: the player faction's primary ideoligion.</summary>
        public static Ideo PlayerIdeo
        {
            get
            {
                if (!ModsConfig.IdeologyActive || Faction.OfPlayerSilentFail == null)
                    return null;
                return Faction.OfPlayer.ideos?.PrimaryIdeo;
            }
        }

        public static bool IsFollower(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.RaceProps.Humanlike)
                return false;
            Ideo ideo = PlayerIdeo;
            return ideo != null && pawn.Ideo == ideo;
        }

        public static float FollowerWeight(Pawn pawn) => pawn.IsSlave ? SlaveFollowerWeight : 1f;

        /// <summary>
        /// The pawn through whom great miracles are channeled: the moral guide of the player's ideoligion,
        /// or failing that its leader.
        /// </summary>
        public static Pawn Prophet
        {
            get
            {
                Ideo ideo = PlayerIdeo;
                if (ideo == null)
                    return null;
                Pawn leader = null;
                foreach (Precept_Role role in ideo.RolesListForReading)
                {
                    Pawn holder = role.ChosenPawnSingle();
                    if (holder == null || holder.Dead)
                        continue;
                    if (role.def.defName == "IdeoRole_Moralist")
                        return holder;
                    if (role.def.leaderRole && leader == null)
                        leader = holder;
                }
                return leader;
            }
        }

        /// <summary>Whether the prophet can channel a miracle on <paramref name="map"/> (any map if null).</summary>
        public static AcceptanceReport CanProphetChannel(Map map)
        {
            Pawn prophet = Prophet;
            if (prophet == null)
                return "IAG_Reason_NoProphet".Translate();
            if (prophet.Downed || !prophet.Awake())
                return "IAG_Reason_ProphetIncapable".Translate(prophet.Named("PROPHET"));
            if (map != null && prophet.MapHeld != map)
                return "IAG_Reason_ProphetNotHere".Translate(prophet.Named("PROPHET"));
            Hediff strain = prophet.health.hediffSet.GetFirstHediffOfDef(IAG_DefOf.IAG_DivineChanneling);
            if (strain != null && strain.Severity >= ProphetOverwhelmedSeverity)
                return "IAG_Reason_ProphetOverwhelmed".Translate(prophet.Named("PROPHET"));
            return true;
        }

        public static void GainMemory(Pawn pawn, ThoughtDef thought)
        {
            pawn?.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
        }
    }
}
