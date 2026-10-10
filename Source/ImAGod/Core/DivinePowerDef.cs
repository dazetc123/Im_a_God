using System;
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
                if (iconInt == null)
                {
                    iconInt = iconPath.NullOrEmpty() ? null : ContentFinder<Texture2D>.Get(iconPath, reportFailure: false);
                    if (iconInt == null)
                        iconInt = BaseContent.BadTex;
                }
                return iconInt;
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
