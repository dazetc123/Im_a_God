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
        public static MainButtonDef IAG_Divinity;

        static IAG_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(IAG_DefOf));
        }
    }
}
