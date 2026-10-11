using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    public enum PetitionKind
    {
        Healing,
        Food,
        Protection,
        Sign,
        Weather
    }

    /// <summary>A follower's prayer for something specific, which the god may answer before the deadline.</summary>
    public class Petition : IExposable
    {
        public PetitionKind kind;
        public Pawn pawn;
        public Map map;
        public int issuedTick;
        public int deadlineTick;

        public int TicksLeft => deadlineTick - Find.TickManager.TicksGame;

        public string Label => ("IAG_Petition_" + kind).Translate(pawn?.LabelShort ?? "?");

        public string Hint => ("IAG_PetitionHint_" + kind).Translate();

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref issuedTick, "issuedTick");
            Scribe_Values.Look(ref deadlineTick, "deadlineTick");
        }
    }

    /// <summary>
    /// Petitions: now and then a believer prays for healing, food, protection, a sign or better weather. Answering
    /// with a fitting miracle in time rewards faith and certainty; ignoring it breeds doubt.
    /// </summary>
    public partial class GameComponent_Divinity
    {
        private const float PetitionMtbDays = 3f;
        private const int MaxPetitions = 2;
        private const int MinTicksBetweenPetitions = GenDate.TicksPerDay;
        private const float AnsweredFaithBase = 20f;
        private const float AnsweredFaithPerTier = 8f;
        private const float AnsweredDoubt = -8f;
        private const float IgnoredDoubt = 6f;
        private const float AnsweredCertainty = 0.1f;
        private const float IgnoredCertainty = -0.05f;

        private static readonly string[] BadConditions = { "ColdSnap", "HeatWave", "ToxicFallout", "VolcanicWinter", "Flashstorm" };

        public List<Petition> petitions = new List<Petition>();
        private int lastPetitionTick = -GenDate.TicksPerDay;
        private int lastProtectionPetitionTick = -GenDate.TicksPerYear;

        private void PetitionsHourlyTick()
        {
            if (tier == null || DivinityUtility.PlayerIdeo == null)
                return;
            int now = Find.TickManager.TicksGame;

            foreach (Petition petition in petitions.ToList())
            {
                if (petition.map == null || petition.pawn == null || petition.pawn.Dead || !DivinityUtility.IsFollower(petition.pawn))
                {
                    petitions.Remove(petition);
                }
                else if (Resolved(petition))
                {
                    // Mortals solved it (medicine, cooking, the weather turned): neither reward nor blame.
                    petitions.Remove(petition);
                }
                else if (now > petition.deadlineTick)
                {
                    Ignore(petition);
                }
            }

            if (petitions.Count >= MaxPetitions || now - lastPetitionTick < MinTicksBetweenPetitions)
                return;

            // Protection is asked for while a raid is on, other petitions now and then.
            foreach (Map map in Find.Maps.Where(m => m.IsPlayerHome))
            {
                if (now - lastProtectionPetitionTick > 2 * GenDate.TicksPerDay && EnemiesOn(map) >= 3 && Rand.Chance(0.3f))
                {
                    TryIssue(PetitionKind.Protection, map, 6);
                    return;
                }
            }
            if (!Rand.MTBEventOccurs(PetitionMtbDays, GenDate.TicksPerDay, GenDate.TicksPerHour))
                return;
            foreach (PetitionKind kind in new[] { PetitionKind.Healing, PetitionKind.Food, PetitionKind.Weather, PetitionKind.Sign }.InRandomOrder())
            {
                Map map = Find.Maps.Where(m => m.IsPlayerHome).RandomElementWithFallback();
                if (map != null && TryIssue(kind, map, kind == PetitionKind.Healing ? 24 : 48))
                    return;
            }
        }

        private static int EnemiesOn(Map map) =>
            map.mapPawns.AllPawnsSpawned.Count(p => p.RaceProps.Humanlike && !p.Downed && p.HostileTo(Faction.OfPlayer));

        private static bool HasBadCondition(Map map) =>
            map.gameConditionManager.ActiveConditions.Any(c => BadConditions.Contains(c.def.defName));

        private static bool NeedsHealing(Pawn pawn) =>
            pawn.health.summaryHealth.SummaryHealthPercent < 0.6f || pawn.health.hediffSet.hediffs.Any(h => h.def.makesSickThought || h.def.isInfection);

        private bool FoodShort(Map map) =>
            map.resourceCounter.TotalHumanEdibleNutrition < 3f * map.mapPawns.FreeColonistsSpawnedCount;

        private bool TryIssue(PetitionKind kind, Map map, int hours, bool force = false)
        {
            if (petitions.Any(p => p.kind == kind))
                return false;
            List<Pawn> followers = map.mapPawns.FreeColonistsSpawned.Where(DivinityUtility.IsFollower).ToList();
            Pawn pawn = null;
            switch (kind)
            {
                case PetitionKind.Healing:
                    pawn = followers.Where(NeedsHealing).RandomElementWithFallback();
                    break;
                case PetitionKind.Food:
                    if (FoodShort(map))
                        pawn = followers.RandomElementWithFallback();
                    break;
                case PetitionKind.Weather:
                    if (HasBadCondition(map))
                        pawn = followers.RandomElementWithFallback();
                    break;
                case PetitionKind.Sign:
                    if (DoubtLevel != DoubtLevel.Faithful)
                        pawn = followers.OrderBy(p => p.ideo.Certainty).FirstOrDefault();
                    break;
                case PetitionKind.Protection:
                    pawn = followers.Where(p => !p.Downed).RandomElementWithFallback();
                    break;
            }
            if (pawn == null && force)
                pawn = followers.RandomElementWithFallback();
            if (pawn == null)
                return false;

            int now = Find.TickManager.TicksGame;
            var petition = new Petition { kind = kind, pawn = pawn, map = map, issuedTick = now, deadlineTick = now + hours * GenDate.TicksPerHour };
            petitions.Add(petition);
            lastPetitionTick = now;
            if (kind == PetitionKind.Protection)
                lastProtectionPetitionTick = now;
            Find.LetterStack.ReceiveLetter("IAG_PetitionLabel".Translate(pawn.LabelShort),
                "IAG_PetitionText".Translate(pawn.Named("PAWN"), petition.Label, petition.Hint, (hours * GenDate.TicksPerHour).ToStringTicksToPeriod()),
                LetterDefOf.NeutralEvent, pawn);
            return true;
        }

        private bool Resolved(Petition petition)
        {
            switch (petition.kind)
            {
                case PetitionKind.Healing: return !NeedsHealing(petition.pawn);
                case PetitionKind.Food: return !FoodShort(petition.map);
                case PetitionKind.Weather: return !HasBadCondition(petition.map);
                case PetitionKind.Protection: return EnemiesOn(petition.map) == 0;
                default: return false;
            }
        }

        private void Answer(Petition petition)
        {
            petitions.Remove(petition);
            Pawn pawn = petition.pawn;
            float faithGain = AddFaith(AnsweredFaithBase + AnsweredFaithPerTier * TierLevel);
            AddDoubt(AnsweredDoubt, "IAG_DoubtReason_PetitionAnswered".Translate(pawn.LabelShort));
            pawn.ideo?.OffsetCertainty(AnsweredCertainty);
            DivinityUtility.GainMemory(pawn, IAG_DefOf.IAG_PrayerAnswered);
            Messages.Message("IAG_PetitionAnswered".Translate(pawn.Named("PAWN"), faithGain.ToString("0")), pawn, MessageTypeDefOf.PositiveEvent);
        }

        private void Ignore(Petition petition)
        {
            petitions.Remove(petition);
            Pawn pawn = petition.pawn;
            AddDoubt(IgnoredDoubt, "IAG_DoubtReason_PetitionIgnored".Translate(pawn.LabelShort));
            pawn.ideo?.OffsetCertainty(IgnoredCertainty);
            DivinityUtility.GainMemory(pawn, IAG_DefOf.IAG_PrayerIgnored);
            Messages.Message("IAG_PetitionIgnored".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>A miracle was worked on a map; it may answer petitions for food, protection or weather.</summary>
        public void Notify_MiracleWorked(Map map, DivinePowerDef power, IntVec3 cell)
        {
            if (map == null)
                return;
            Type workerClass = power.workerClass;
            foreach (Petition petition in petitions.Where(p => p.map == map).ToList())
            {
                bool answers = false;
                switch (petition.kind)
                {
                    case PetitionKind.Food:
                        answers = workerClass == typeof(DivinePowerWorker_Manna) && power.thingDef != null && power.thingDef.IsNutritionGivingIngestible;
                        break;
                    case PetitionKind.Protection:
                        answers = power.affects == PawnFilter.Enemies || workerClass == typeof(DivinePowerWorker_Smite) || workerClass == typeof(DivinePowerWorker_Explosion)
                                  || workerClass == typeof(DivinePowerWorker_Terror) || workerClass == typeof(DivinePowerWorker_Meteor) || workerClass == typeof(DivinePowerWorker_Fire);
                        break;
                    case PetitionKind.Weather:
                        answers = workerClass == typeof(DivinePowerWorker_EndConditions) || workerClass == typeof(DivinePowerWorker_Weather);
                        break;
                }
                if (answers)
                    Answer(petition);
            }
        }

        /// <summary>A miracle touched a pawn directly; it may answer their petition for healing.</summary>
        public void Notify_PawnBlessed(Pawn pawn, DivinePowerDef power)
        {
            Type workerClass = power.workerClass;
            bool heals = workerClass == typeof(DivinePowerWorker_Mend) || workerClass == typeof(DivinePowerWorker_AreaHeal) || workerClass == typeof(DivinePowerWorker_Restore);
            if (!heals)
                return;
            foreach (Petition petition in petitions.Where(p => p.kind == PetitionKind.Healing && p.pawn == pawn).ToList())
                Answer(petition);
        }

        /// <summary>A believer saw a miracle: that is the sign they asked for.</summary>
        private void Notify_Witnessed(Pawn pawn)
        {
            foreach (Petition petition in petitions.Where(p => p.kind == PetitionKind.Sign && p.pawn == pawn).ToList())
                Answer(petition);
        }

        private void ExposePetitions()
        {
            Scribe_Collections.Look(ref petitions, "petitions", LookMode.Deep);
            Scribe_Values.Look(ref lastPetitionTick, "lastPetitionTick", -GenDate.TicksPerDay);
            Scribe_Values.Look(ref lastProtectionPetitionTick, "lastProtectionPetitionTick", -GenDate.TicksPerYear);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                petitions ??= new List<Petition>();
                petitions.RemoveAll(p => p.pawn == null || p.map == null);
            }
        }
    }
}
