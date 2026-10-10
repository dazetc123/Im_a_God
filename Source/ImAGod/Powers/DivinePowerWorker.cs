using RimWorld;
using Verse;

namespace ImAGod
{
    public abstract class DivinePowerWorker
    {
        public DivinePowerDef def;

        protected static GameComponent_Divinity Divinity => GameComponent_Divinity.Instance;
        protected static int Tier => Divinity?.TierLevel ?? 0;

        /// <summary>The power's scaled strength: amount plus amountPerTier for each tier above the minimum.</summary>
        protected float ScaledAmount => def.amount + def.amountPerTier * (Tier - def.minTier);

        public virtual TargetingParameters TargetingParameters()
        {
            switch (def.targetMode)
            {
                case DivineTargetMode.Pawn:
                    return new TargetingParameters
                    {
                        canTargetPawns = true,
                        canTargetHumans = true,
                        canTargetAnimals = true,
                        canTargetMechs = false,
                        canTargetBuildings = false,
                        canTargetLocations = false,
                        mapObjectTargetsMustBeAutoAttackable = false
                    };
                case DivineTargetMode.Corpse:
                    return new TargetingParameters
                    {
                        canTargetPawns = false,
                        canTargetBuildings = false,
                        canTargetItems = true,
                        canTargetCorpses = true,
                        onlyTargetCorpses = true,
                        mapObjectTargetsMustBeAutoAttackable = false
                    };
                default:
                    return new TargetingParameters
                    {
                        canTargetLocations = true,
                        canTargetPawns = true,
                        canTargetBuildings = true,
                        canTargetItems = true,
                        mapObjectTargetsMustBeAutoAttackable = false
                    };
            }
        }

        public virtual AcceptanceReport CanApplyOn(LocalTargetInfo target, Map map)
        {
            if (def.targetMode == DivineTargetMode.None)
                return true;
            if (map == null || !target.IsValid || !target.Cell.InBounds(map))
                return "IAG_Reason_InvalidTarget".Translate();
            if (target.Cell.Fogged(map))
                return "IAG_Reason_Fogged".Translate();
            return true;
        }

        /// <summary>Works the miracle. Returns false if nothing happened, in which case no faith is spent.</summary>
        public virtual bool Apply(LocalTargetInfo target, Map map) => false;

        public virtual AcceptanceReport CanApplyOnRival(RivalGod rival) =>
            rival != null && rival.IsValid ? (AcceptanceReport)true : "IAG_Reason_InvalidTarget".Translate();

        /// <summary>Works a miracle aimed at a rival god (target mode RivalGod).</summary>
        public virtual bool ApplyOnRival(RivalGod rival) => false;

        /// <summary>An extra line for the divinity tab describing the power's current strength.</summary>
        public virtual string EffectSummary() => null;
    }
}
