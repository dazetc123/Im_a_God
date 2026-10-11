using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ImAGod
{
    /// <summary>Deeds recorded in a follower's records (harvests, kills, crafted things...) are offerings to the god's domains.</summary>
    [HarmonyPatch(typeof(Pawn_RecordsTracker), nameof(Pawn_RecordsTracker.Increment))]
    public static class Patch_Records_Increment
    {
        public static void Postfix(Pawn_RecordsTracker __instance, RecordDef def)
        {
            if (__instance.pawn != null)
                GameComponent_Divinity.Instance?.Notify_Record(__instance.pawn, def, 1f);
        }
    }

    [HarmonyPatch(typeof(Pawn_RecordsTracker), nameof(Pawn_RecordsTracker.AddTo))]
    public static class Patch_Records_AddTo
    {
        public static void Postfix(Pawn_RecordsTracker __instance, RecordDef def, float value)
        {
            if (__instance.pawn != null)
                GameComponent_Divinity.Instance?.Notify_Record(__instance.pawn, def, value);
        }
    }

    /// <summary>Some domains delight in particular feelings, such as a night of lovin'.</summary>
    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory), typeof(Thought_Memory), typeof(Pawn))]
    public static class Patch_MemoryThoughtHandler_TryGainMemory
    {
        public static void Postfix(MemoryThoughtHandler __instance, Thought_Memory newThought)
        {
            if (__instance.pawn != null && newThought?.def != null)
                GameComponent_Divinity.Instance?.Notify_Thought(__instance.pawn, newThought.def);
        }
    }

    /// <summary>Madness feeds on mental breaks.</summary>
    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    public static class Patch_MentalStateHandler_TryStartMentalState
    {
        public static void Postfix(bool __result, Pawn ___pawn)
        {
            if (__result && ___pawn != null)
                GameComponent_Divinity.Instance?.Notify_MentalBreak(___pawn);
        }
    }
}
