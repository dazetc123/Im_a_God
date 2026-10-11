using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    public enum DivineTargetMode
    {
        None,
        Cell,
        Pawn,
        Corpse,
        RivalGod
    }

    /// <summary>Which pawns an area miracle affects.</summary>
    public enum PawnFilter
    {
        Followers,
        Colonists,
        Allies,
        Enemies,
        Animals,
        Everyone
    }

    public class DivinePowerDef : Def
    {
        public Type workerClass;
        public string iconPath;

        public float faithCost = 50f;
        public int minTier = 1;
        public float cooldownDays = 1f;
        public DivineTargetMode targetMode = DivineTargetMode.None;
        public bool requiresProphet;

        /// <summary>Strain added to the prophet's channeling hediff when this power is used.</summary>
        public float prophetStrain = 0.35f;

        /// <summary>Radius in which believers witness the miracle. Zero disables witnessing.</summary>
        public float witnessRadius = 12f;

        /// <summary>Change in the colony's doubt when the miracle is worked. Miracles reassure believers.</summary>
        public float doubtOffset = -2f;

        // Free parameters interpreted by the worker.
        public float radius;
        public float amount;
        public float amountPerTier;
        public float amountPerFollower;
        public float durationDays;
        public ThingDef thingDef;
        public HediffDef hediffDef;
        public PawnFilter affects = PawnFilter.Allies;
        public bool wholeMap;
        public MentalStateDef mentalState;
        public DamageDef damageDef;
        public WeatherDef weather;
        public TerrainDef terrain;
        public ThoughtDef thought;
        public bool cureDiseases;
        public QualityCategory quality = QualityCategory.Normal;
        public List<GameConditionDef> conditions;
        public float fireChance;

        /// <summary>A weaker variant used by demigod abilities; never listed among the god's own miracles.</summary>
        public bool demigodOnly;

        [Unsaved] private DivinePowerWorker workerInt;
        [Unsaved] private Texture2D iconInt;

        public int order;

        public DivinePowerWorker Worker
        {
            get
            {
                if (workerInt == null)
                {
                    workerInt = (DivinePowerWorker)Activator.CreateInstance(workerClass);
                    workerInt.def = this;
                }
                return workerInt;
            }
        }

        public Texture2D Icon
        {
            get
            {
                return iconInt ??= DivinityUtility.LoadIcon(iconPath);
            }
        }

        public override System.Collections.Generic.IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors())
                yield return e;
            if (workerClass == null || !typeof(DivinePowerWorker).IsAssignableFrom(workerClass))
                yield return "workerClass must be a subclass of ImAGod.DivinePowerWorker";
        }
    }
}
