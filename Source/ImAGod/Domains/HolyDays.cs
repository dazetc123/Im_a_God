using System.Linq;
using RimWorld;
using Verse;

namespace ImAGod
{
    /// <summary>
    /// Each domain has a holy day in the year. On it rituals offer twice the faith, prayer half again as much, and
    /// believers celebrate.
    /// </summary>
    public partial class GameComponent_Divinity
    {
        public const float HolyDayRitualFactor = 2f;
        public const float HolyDayPrayerFactor = 1.5f;

        private int lastHolyDayAnnounced = -1;

        public DivineDomainDef HolyDayOn(Map map)
        {
            if (map == null)
                return null;
            int day = GenLocalDate.DayOfYear(map);
            return domains.FirstOrDefault(d => d.holyDay == day);
        }

        public DivineDomainDef CurrentHolyDay => HolyDayOn(Find.AnyPlayerHomeMap);

        public static string DayLabel(int dayOfYear) =>
            "IAG_DayOfYear".Translate(dayOfYear % GenDate.DaysPerQuadrum + 1, ((Quadrum)(dayOfYear / GenDate.DaysPerQuadrum)).Label());

        private void HolyDaysHourlyTick()
        {
            DivineDomainDef holy = CurrentHolyDay;
            int today = GenDate.DaysPassed;
            if (holy == null || lastHolyDayAnnounced == today)
                return;
            lastHolyDayAnnounced = today;
            Find.LetterStack.ReceiveLetter("IAG_HolyDayLabel".Translate(holy.HolyDayName), "IAG_HolyDayText".Translate(holy.HolyDayName, holy.label), LetterDefOf.PositiveEvent);
        }

        private void ExposeHolyDays()
        {
            Scribe_Values.Look(ref lastHolyDayAnnounced, "lastHolyDayAnnounced", -1);
        }
    }

    public class ThoughtWorker_HolyDay : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            GameComponent_Divinity divinity = GameComponent_Divinity.Instance;
            if (divinity == null || p.Faction != Faction.OfPlayer || !DivinityUtility.IsFollower(p))
                return ThoughtState.Inactive;
            return divinity.HolyDayOn(p.MapHeld ?? Find.AnyPlayerHomeMap) != null ? ThoughtState.ActiveDefault : ThoughtState.Inactive;
        }
    }
}
