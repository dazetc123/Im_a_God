using HarmonyLib;
using RimWorld;
using Verse;

namespace ImAGod
{
    /// <summary>The death of a believer shakes the faith of the others.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_Pawn_Kill
    {
        private const int NotFollower = 0;
        private const int Follower = 1;
        private const int Prophet = 2;

        public static void Prefix(Pawn __instance, out int __state)
        {
            __state = NotFollower;
            if (!__instance.Dead && __instance.Faction == Faction.OfPlayerSilentFail && !__instance.IsQuestLodger() && DivinityUtility.IsFollower(__instance))
                __state = __instance == DivinityUtility.Prophet ? Prophet : Follower;
        }

        public static void Postfix(Pawn __instance, int __state)
        {
            if (__state != NotFollower && __instance.Dead)
                GameComponent_Divinity.Instance?.Notify_FollowerDied(__instance, __state == Prophet);
        }
    }
}
