using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    public enum DoubtLevel
    {
        Faithful,
        Murmurs,
        Crisis,
        Schism
    }

    public class DoubtEvent : IExposable
    {
        public string label;
        public float amount;
        public int tick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Values.Look(ref tick, "tick");
        }
    }

    /// <summary>
    /// Faith crisis: doubt rises when believers die, rituals fail, rival gods whisper or the god stays silent while
    /// hoarding faith, and falls with miracles, fulfilled prophecies, good rituals and prayer. High doubt weakens
    /// the god and can turn believers into heretics who worship a rival god.
    /// </summary>
    public partial class GameComponent_Divinity
    {
        public const float MaxDoubt = 100f;
        private const float NaturalRecoveryPerDay = 1f;
        private const float SilentGodDoubtPerDay = 3f;
        private const float SilentGodFaithFraction = 0.9f;
        private const float SilentGodDays = 3f;
        private const float HereticDoubtPerDay = 1f;
        private const float MinLoggedDoubt = 1f;
        private const int DoubtLogSize = 10;
        private const float HeresyCertaintyThreshold = 0.5f;
        private const float InstigatedHeresyCertaintyThreshold = 0.6f;
        private const int MinTicksBetweenHeresies = 3 * GenDate.TicksPerDay;
        private const float HeresyDoubt = 5f;
        private const float RepentDoubt = -8f;

        public float doubt;
        public List<DoubtEvent> doubtLog = new List<DoubtEvent>();
        public List<Pawn> heretics = new List<Pawn>();
        private int lastHeresyTick = -MinTicksBetweenHeresies;

        public static DoubtLevel LevelFor(float doubt) =>
            doubt >= 75f ? DoubtLevel.Schism : doubt >= 50f ? DoubtLevel.Crisis : doubt >= 25f ? DoubtLevel.Murmurs : DoubtLevel.Faithful;

        public DoubtLevel DoubtLevel => ImAGodMod.Settings.faithCrisisEnabled ? LevelFor(doubt) : DoubtLevel.Faithful;

        public float PassiveFaithFactor
        {
            get
            {
                switch (DoubtLevel)
                {
                    case DoubtLevel.Murmurs: return 0.75f;
                    case DoubtLevel.Crisis: return 0.5f;
                    case DoubtLevel.Schism: return 0.25f;
                    default: return 1f;
                }
            }
        }

        public float PrayerFaithFactor
        {
            get
            {
                switch (DoubtLevel)
                {
                    case DoubtLevel.Crisis: return 0.75f;
                    case DoubtLevel.Schism: return 0.5f;
                    default: return 1f;
                }
            }
        }

        public float CertaintyDrainPerDay
        {
            get
            {
                switch (DoubtLevel)
                {
                    case DoubtLevel.Crisis: return 0.01f;
                    case DoubtLevel.Schism: return 0.02f;
                    default: return 0f;
                }
            }
        }

        public float HeresyMtbDays
        {
            get
            {
                switch (DoubtLevel)
                {
                    case DoubtLevel.Crisis: return 10f;
                    case DoubtLevel.Schism: return 4f;
                    default: return -1f;
                }
            }
        }

        /// <summary>True while the god hoards faith without working miracles; believers wonder why it stays silent.</summary>
        public bool GodIsSilent =>
            tier != null && faith >= FaithCap * SilentGodFaithFraction &&
            Find.TickManager.TicksGame - Mathf.Max(lastMiracleTick, 0) > SilentGodDays * GenDate.TicksPerDay;

        public float DoubtChangePerDay
        {
            get
            {
                float change = -NaturalRecoveryPerDay + HereticDoubtPerDay * heretics.Count;
                if (GodIsSilent)
                    change += SilentGodDoubtPerDay;
                return change;
            }
        }

        public void AddDoubt(float amount, string reason)
        {
            if (!ImAGodMod.Settings.faithCrisisEnabled || amount == 0f || DivinityUtility.PlayerIdeo == null)
                return;
            DoubtLevel before = LevelFor(doubt);
            float old = doubt;
            doubt = Mathf.Clamp(doubt + amount, 0f, MaxDoubt);
            float actual = doubt - old;

            if (Mathf.Abs(actual) >= MinLoggedDoubt && !reason.NullOrEmpty())
            {
                doubtLog.Insert(0, new DoubtEvent { label = reason, amount = actual, tick = Find.TickManager.TicksGame });
                if (doubtLog.Count > DoubtLogSize)
                    doubtLog.RemoveRange(DoubtLogSize, doubtLog.Count - DoubtLogSize);
            }

            DoubtLevel after = LevelFor(doubt);
            if (after != before)
            {
                MessageTypeDef type = after > before ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.PositiveEvent;
                Messages.Message("IAG_DoubtLevelChanged".Translate(DoubtLevelLabel(after), DoubtLevelEffects(after)), type);
            }
        }

        public static string DoubtLevelLabel(DoubtLevel level) => ("IAG_DoubtLevel_" + level).Translate();

        public static string DoubtLevelEffects(DoubtLevel level) => ("IAG_DoubtEffects_" + level).Translate();

        private void CrisisHourlyTick()
        {
            if (!ImAGodMod.Settings.faithCrisisEnabled || DivinityUtility.PlayerIdeo == null)
                return;

            UpdateHeretics();
            if (tier == null)
                return;
            AddDoubt(DoubtChangePerDay / GenDate.HoursPerDay, null);

            float drain = CertaintyDrainPerDay / GenDate.HoursPerDay;
            if (drain > 0f)
            {
                foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction)
                {
                    if (DivinityUtility.IsFollower(pawn))
                        pawn.ideo.OffsetCertainty(-drain);
                }
            }

            float mtb = HeresyMtbDays;
            if (mtb > 0f && Find.TickManager.TicksGame - lastHeresyTick > MinTicksBetweenHeresies && Rand.MTBEventOccurs(mtb, GenDate.TicksPerDay, GenDate.TicksPerHour))
                TryStartHeresy(null, HeresyCertaintyThreshold);
        }

        private void UpdateHeretics()
        {
            Ideo ideo = DivinityUtility.PlayerIdeo;
            for (int i = heretics.Count - 1; i >= 0; i--)
            {
                Pawn pawn = heretics[i];
                if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Faction != Faction.OfPlayer)
                {
                    heretics.RemoveAt(i);
                }
                else if (pawn.Ideo == ideo)
                {
                    heretics.RemoveAt(i);
                    Messages.Message("IAG_HereticRepented".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.PositiveEvent);
                    AddDoubt(RepentDoubt, "IAG_DoubtReason_Repented".Translate(pawn.LabelShort));
                }
            }
        }

        /// <summary>
        /// Turns the least certain believer into a heretic who worships a rival god.
        /// </summary>
        /// <param name="instigator">The rival god behind the heresy, or null to pick the strongest rival.</param>
        public bool TryStartHeresy(RivalGod instigator, float certaintyThreshold)
        {
            Ideo ours = DivinityUtility.PlayerIdeo;
            if (ours == null)
                return false;
            Pawn prophet = DivinityUtility.Prophet;
            Pawn heretic = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists
                .Where(p => DivinityUtility.IsFollower(p) && p != prophet && !p.IsQuestLodger() && !p.InMentalState && p.ideo.Certainty < certaintyThreshold)
                .OrderBy(p => p.ideo.Certainty)
                .FirstOrDefault();
            if (heretic == null)
                return false;

            Ideo target = instigator?.ideo
                ?? rivals.Where(r => r.IsValid).OrderByDescending(r => r.power).FirstOrDefault()?.ideo
                ?? Find.IdeoManager.IdeosListForReading.Where(i => i != ours).RandomElementWithFallback();
            if (target == null)
                return false;

            string godName = RivalFor(target)?.NameCap ?? target.name;
            heretic.ideo.SetIdeo(target);
            heretics.Add(heretic);
            lastHeresyTick = Find.TickManager.TicksGame;
            AddDoubt(HeresyDoubt, "IAG_DoubtReason_Heresy".Translate(heretic.LabelShort));
            Find.LetterStack.ReceiveLetter("IAG_HeresyLabel".Translate(), "IAG_HeresyText".Translate(heretic.Named("PAWN"), godName, target.name), LetterDefOf.NegativeEvent, heretic);
            return true;
        }

        public void Notify_FollowerDied(Pawn pawn, bool wasProphet)
        {
            float amount = wasProphet ? 12f : 6f;
            AddDoubt(amount, "IAG_DoubtReason_Death".Translate(pawn.LabelShort));
        }

        private void ExposeCrisis()
        {
            Scribe_Values.Look(ref doubt, "doubt");
            Scribe_Values.Look(ref lastHeresyTick, "lastHeresyTick", -MinTicksBetweenHeresies);
            Scribe_Collections.Look(ref doubtLog, "doubtLog", LookMode.Deep);
            Scribe_Collections.Look(ref heretics, "heretics", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                doubtLog ??= new List<DoubtEvent>();
                heretics ??= new List<Pawn>();
                heretics.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>Believers feel the colony's faith crisis.</summary>
    public class ThoughtWorker_FaithCrisis : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            GameComponent_Divinity divinity = GameComponent_Divinity.Instance;
            if (divinity == null || !DivinityUtility.IsFollower(p) || p.Faction != Faction.OfPlayer)
                return ThoughtState.Inactive;
            switch (divinity.DoubtLevel)
            {
                case DoubtLevel.Murmurs: return ThoughtState.ActiveAtStage(0);
                case DoubtLevel.Crisis: return ThoughtState.ActiveAtStage(1);
                case DoubtLevel.Schism: return ThoughtState.ActiveAtStage(2);
                default: return ThoughtState.Inactive;
            }
        }
    }
}
