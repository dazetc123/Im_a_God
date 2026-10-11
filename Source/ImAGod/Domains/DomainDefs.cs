using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    /// <summary>Creation or Chaos: the nature of the god, chosen once and for all.</summary>
    public class DivinePathDef : Def
    {
        public string iconPath;
        public Color color = Color.white;
        public int order;

        [Unsaved] private Texture2D iconInt;

        public Texture2D Icon => iconInt ??= DivinityUtility.LoadIcon(iconPath);

        public IEnumerable<DivineDomainDef> Domains =>
            DefDatabase<DivineDomainDef>.AllDefsListForReading.Where(d => d.path == this).OrderBy(d => d.order);
    }

    public class RecordOffering
    {
        public RecordDef record;
        public float faith;
    }

    public class ThoughtOffering
    {
        public ThoughtDef thought;
        public float faith;
    }

    /// <summary>
    /// A divine domain such as harvest or madness. It blesses the god's followers, decides which deeds of theirs offer
    /// faith, and holds a tree of miracles for the god and of abilities for demigods.
    /// </summary>
    public class DivineDomainDef : Def
    {
        public DivinePathDef path;
        public string iconPath;
        public Color color = Color.white;
        public int order;

        /// <summary>Given to every follower; its severity is the god's tier.</summary>
        public HediffDef blessing;

        public List<RecordOffering> recordOfferings = new List<RecordOffering>();
        public List<ThoughtOffering> thoughtOfferings = new List<ThoughtOffering>();

        /// <summary>Faith offered whenever someone on a player home map has a mental break.</summary>
        public float mentalBreakFaith;

        [Unsaved] private Texture2D iconInt;

        public Texture2D Icon => iconInt ??= DivinityUtility.LoadIcon(iconPath);

        public IEnumerable<DivineNodeDef> GodNodes => DefDatabase<DivineNodeDef>.AllDefsListForReading.Where(n => n.domain == this && !n.forDemigod);

        public IEnumerable<DivineNodeDef> DemigodNodes => DefDatabase<DivineNodeDef>.AllDefsListForReading.Where(n => n.domain == this && n.forDemigod);

        /// <summary>A one-line summary of what offers faith to this domain.</summary>
        public string OfferingsSummary
        {
            get
            {
                var parts = new List<string>();
                parts.AddRange(recordOfferings.Select(o => o.record.LabelCap.ToString()));
                parts.AddRange(thoughtOfferings.Select(o => o.thought.stages[0].LabelCap.ToString()));
                if (mentalBreakFaith > 0f)
                    parts.Add("IAG_OfferingMentalBreak".Translate());
                return parts.ToCommaList();
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string e in base.ConfigErrors())
                yield return e;
            if (path == null)
                yield return "domain has no path";
        }
    }

    /// <summary>
    /// A node in a domain's tree. God nodes unlock a miracle with divine points; demigod nodes teach a demigod an ability.
    /// A node requires any one of its prerequisites.
    /// </summary>
    public class DivineNodeDef : Def
    {
        public DivineDomainDef domain;
        public int row;
        public int column;
        public int pointCost = 1;
        public List<DivineNodeDef> prerequisites = new List<DivineNodeDef>();

        public DivinePowerDef power;

        public bool forDemigod;
        public AbilityDef ability;
        public int minDemigodLevel = 1;

        public int MinTier => power?.minTier ?? 1;

        public Texture2D Icon => power?.Icon ?? BaseContent.BadTex;

        public override TaggedString LabelCap => power != null ? power.LabelCap : base.LabelCap;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string e in base.ConfigErrors())
                yield return e;
            if (domain == null)
                yield return "node has no domain";
            if (power == null)
                yield return "node has no power";
            if (forDemigod && ability == null)
                yield return "demigod node has no ability";
        }
    }
}
