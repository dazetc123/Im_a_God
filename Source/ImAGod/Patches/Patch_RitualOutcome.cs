using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    /// <summary>Rituals of the player's ideoligion offer faith to the god, more for better rituals and more believers.</summary>
    [HarmonyPatch(typeof(RitualOutcomeEffectWorker_FromQuality), nameof(RitualOutcomeEffectWorker_FromQuality.Apply))]
    public static class Patch_RitualOutcome
    {
        private const float BaseFaith = 15f;
        private const float FaithPerBeliever = 6f;
        private const float MinQualityFactor = 0.25f;

        private static readonly Func<RitualOutcomeEffectWorker_FromQuality, LordJob_Ritual, float, float> getQuality =
            AccessTools.MethodDelegate<Func<RitualOutcomeEffectWorker_FromQuality, LordJob_Ritual, float, float>>(
                AccessTools.Method(typeof(RitualOutcomeEffectWorker_FromQuality), "GetQuality"));

        public static void Postfix(RitualOutcomeEffectWorker_FromQuality __instance, float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            try
            {
                GameComponent_Divinity divinity = GameComponent_Divinity.Instance;
                Ideo ideo = DivinityUtility.PlayerIdeo;
                if (divinity == null || ideo == null || jobRitual?.Ritual?.ideo != ideo || totalPresence == null)
                    return;

                int believers = 0;
                foreach (Pawn pawn in totalPresence.Keys)
                {
                    if (DivinityUtility.IsFollower(pawn))
                        believers++;
                }
                if (believers == 0)
                    return;

                float quality = Mathf.Clamp01(getQuality(__instance, jobRitual, progress));
                float gained = divinity.AddFaith((BaseFaith + FaithPerBeliever * believers) * (MinQualityFactor + quality));
                if (gained > 0f)
                    Messages.Message("IAG_RitualFaith".Translate(jobRitual.Ritual.LabelCap, gained.ToString("0")), MessageTypeDefOf.PositiveEvent, false);
            }
            catch (Exception e)
            {
                Log.ErrorOnce($"[I'm a God] Failed to grant ritual faith: {e}", 0x1A6_0D);
            }
        }
    }
}
