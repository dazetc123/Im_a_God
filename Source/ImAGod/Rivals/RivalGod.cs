using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    /// <summary>The god of another faction's ideoligion. Its power grows with the settlements that worship it.</summary>
    public class RivalGod : IExposable
    {
        public const float MaxPower = 1000f;
        public const float PowerPerTier = 250f;

        public Ideo ideo;
        public float power;
        public int lastInterventionTick = -GenDate.TicksPerYear;
        public int humbledUntilTick = -1;

        public bool IsValid => ideo != null && Find.IdeoManager.IdeosListForReading.Contains(ideo);

        public string Name
        {
            get
            {
                string deity = null;
                try
                {
                    deity = ideo?.KeyDeityName;
                }
                catch (Exception)
                {
                    // Ideoligions without deities may not have a key deity.
                }
                return deity.NullOrEmpty() ? "IAG_RivalGodOf".Translate(ideo?.name ?? "?").ToString() : deity;
            }
        }

        public string NameCap => Name.CapitalizeFirst();

        public int Tier => Mathf.Clamp(Mathf.FloorToInt(power / PowerPerTier), 0, 4);

        public bool Humbled => Find.TickManager.TicksGame < humbledUntilTick;

        public IEnumerable<Faction> Factions =>
            Find.FactionManager.AllFactionsListForReading.Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.ideos?.PrimaryIdeo == ideo);

        public bool HostileToPlayer => Factions.Any(f => f.HostileTo(Faction.OfPlayer));

        public int SettlementCount => Find.WorldObjects.Settlements.Count(s => s.Faction != null && !s.Faction.IsPlayer && s.Faction.ideos?.PrimaryIdeo == ideo && !MissionUtility.IsConverted(s));

        public void Humble(float amount)
        {
            power = Mathf.Max(0f, power - amount);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref ideo, "ideo");
            Scribe_Values.Look(ref power, "power");
            Scribe_Values.Look(ref lastInterventionTick, "lastInterventionTick", -GenDate.TicksPerYear);
            Scribe_Values.Look(ref humbledUntilTick, "humbledUntilTick", -1);
        }
    }

    public class RivalInterventionDef : Def
    {
        public Type workerClass;
        public float minPower;
        public float weight = 1f;
        public float amount;
        public IncidentDef incident;

        [Unsaved] private RivalInterventionWorker workerInt;

        public RivalInterventionWorker Worker
        {
            get
            {
                if (workerInt == null)
                {
                    workerInt = (RivalInterventionWorker)Activator.CreateInstance(workerClass);
                    workerInt.def = this;
                }
                return workerInt;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string e in base.ConfigErrors())
                yield return e;
            if (workerClass == null || !typeof(RivalInterventionWorker).IsAssignableFrom(workerClass))
                yield return "workerClass must be a subclass of ImAGod.RivalInterventionWorker";
        }
    }

    public abstract class RivalInterventionWorker
    {
        public RivalInterventionDef def;

        public virtual bool CanDoNow(RivalGod rival, Map map) => true;

        public abstract bool Execute(RivalGod rival, Map map);
    }
}
