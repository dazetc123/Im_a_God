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

        static IAG_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(IAG_DefOf));
        }
    }
}
