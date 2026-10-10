using Verse;

namespace ImAGod
{
    public class HediffCompProperties_AfterEffect : HediffCompProperties
    {
        public HediffDef hediff;

        public HediffCompProperties_AfterEffect()
        {
            compClass = typeof(HediffComp_AfterEffect);
        }
    }

    /// <summary>Adds another hediff when this one is removed, e.g. exhaustion after a divine incarnation ends.</summary>
    public class HediffComp_AfterEffect : HediffComp
    {
        public HediffCompProperties_AfterEffect Props => (HediffCompProperties_AfterEffect)props;

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (Pawn != null && !Pawn.Dead && Props.hediff != null)
                Pawn.health.AddHediff(Props.hediff);
        }
    }
}
