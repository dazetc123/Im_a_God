using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ImAGod
{
    /// <summary>
    /// Divine foresight hooks into the moment the storyteller fires an incident. See ProphecyUtility.
    /// </summary>
    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.TryFire))]
    public static class Patch_Storyteller_TryFire
    {
        public static bool Prefix(FiringIncident fi, bool queued, ref bool __result)
        {
            GameComponent_Divinity divinity = GameComponent_Divinity.Instance;
            if (fi == null || divinity == null)
                return true;

            if (queued || ProphecyUtility.InIncidentQueueTick)
            {
                // A foreseen incident coming due: execute it without recording it a second time.
                Prophecy prophecy = divinity.FindDueProphecy(fi);
                if (prophecy == null)
                    return true;
                __result = ProphecyUtility.Execute(prophecy, fi);
                return false;
            }

            if (!ProphecyUtility.TryForesee(fi))
                return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(IncidentQueue), nameof(IncidentQueue.IncidentQueueTick))]
    public static class Patch_IncidentQueue_IncidentQueueTick
    {
        public static void Prefix() => ProphecyUtility.InIncidentQueueTick = true;

        public static Exception Finalizer(Exception __exception)
        {
            ProphecyUtility.InIncidentQueueTick = false;
            return __exception;
        }
    }
}
