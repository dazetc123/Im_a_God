using RimWorld;
using Verse;

namespace ImAGod
{
    [DefOf]
    public static class IAG_DefOf
    {
        public static HediffDef IAG_DivineChanneling;
        public static ThoughtDef IAG_WitnessedMiracle;
        public static ThoughtDef IAG_Prayed;
        public static HediffDef IAG_Champion;
        public static HediffDef IAG_Demigod;
        public static MainButtonDef IAG_Divinity;
        public static FleckDef IAG_Fleck_LightBeam;
        public static FleckDef IAG_Fleck_Ring;
        public static ThoughtDef IAG_PrayerAnswered;
        public static ThoughtDef IAG_PrayerIgnored;
        public static ThoughtDef IAG_WitnessedSacrifice;
        public static ThoughtDef IAG_CommandmentKept;
        public static JobDef IAG_Sacrifice;
        public static JobDef IAG_MessengerCast;
        public static FleckDef IAG_Fleck_Beam;
        public static FleckDef IAG_Fleck_Glow;
        public static HediffDef IAG_DivineBody;
        public static HediffDef IAG_RivalAvatar;
        public static PawnKindDef IAG_Seraphim;
        public static PawnKindDef IAG_Tiamat;
        public static ThoughtDef IAG_MessengerDescended;
        public static ThoughtDef IAG_AvatarSlain;
        public static ThoughtDef IAG_MissionSucceeded;

        static IAG_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(IAG_DefOf));
        }
    }
}
